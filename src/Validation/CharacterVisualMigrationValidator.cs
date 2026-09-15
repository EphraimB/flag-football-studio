using System;
using System.Linq;
using System.Threading.Tasks;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class CharacterVisualMigrationValidator : Node
{
    public async Task RunAsync()
    {
        Require(ImportedCharacterAssetContract.DefaultAssetPath.EndsWith(".glb", StringComparison.OrdinalIgnoreCase),
            "The default production-character contract is not an open GLB asset.");
        Require(ImportedCharacterAssetContract.RequiredRenderBones.Count > HumanoidSkeletonDefinition.Bones.Count,
            "The imported render skeleton does not improve on the 18-bone control rig.");
        Require(ImportedCharacterAssetContract.LegacyControlToRenderBone.Count == HumanoidSkeletonDefinition.Bones.Count,
            "The compatibility boundary does not map every legacy control bone.");
        foreach (var bone in HumanoidSkeletonDefinition.Bones)
            Require(ImportedCharacterAssetContract.LegacyControlToRenderBone.ContainsKey(bone.Name),
                $"Legacy control bone {bone.Name} has no render-skeleton role.");

        var missingPath = $"res://assets/characters/imported/validation-missing-{Guid.NewGuid():N}.glb";
        var missing = ImportedCharacterAssetValidator.Validate(missingPath);
        Require(!missing.AssetExists && !missing.IsCompatible && missing.MissingBones.Count > 0,
            "A missing imported asset did not fail contract validation cleanly.");

        var importedRequest = new CharacterVisualController { Name = "ImportedSlotValidation", Visible = false };
        importedRequest.Configure(new CharacterVisualRequest(CharacterVisualBackend.ImportedModular, missingPath));
        AddChild(importedRequest);
        Require(importedRequest.RequestedBackend == CharacterVisualBackend.ImportedModular &&
                importedRequest.ActiveBackend == CharacterVisualBackend.ProceduralLegacy &&
                importedRequest.Status.UsedFallback,
            "Missing imported visual did not fall back to the procedural implementation.");

        var game = Game.CreatePrototype();
        var project = GameProject.CreatePrototype(game);
        var player = game.Gold.Roster[0];
        var appearance = project.AppearanceFor(player.Id);
        var uniform = project.ActiveUniformFor(game.Gold.Id);
        var pawn = new PlayerPawn { Name = "CharacterAdapterValidation", Visible = false };
        pawn.Configure(player, appearance, uniform);
        AddChild(pawn);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        Require(pawn.RequestedVisualBackend == CharacterVisualBackend.ProceduralLegacy &&
                pawn.ActiveVisualBackend == CharacterVisualBackend.ProceduralLegacy &&
                !pawn.VisualStatus.UsedFallback,
            "Normal players no longer default to the stable procedural visual.");
        var eyeBefore = pawn.EyeAnchor.GlobalPosition;
        appearance.SetHeight(2.3f);
        appearance.SetBodyProportions(1.25f, 1.2f, 0.9f, 1.1f, 1.2f, 1.2f);
        pawn.ApplyAppearance(appearance);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Require(pawn.EyeAnchor.GlobalPosition.Y > eyeBefore.Y && Finite(pawn.MouthAudioAnchor.GlobalPosition) &&
                Finite(pawn.LeftHandAnchor.GlobalPosition) && Finite(pawn.RightHandAnchor.GlobalPosition),
            "The visual adapter broke body morphs or stable anchors.");

        pawn.SetAnimationState(HumanoidAnimationState.Sprint, true);
        pawn.SetFacialExpression(FacialExpressionState.Focused, 0);
        pawn.SetManualGaze(1, -1);
        pawn.SetMouthShape(SpeechMouthShape.A, 0);
        pawn.TriggerBlink();
        pawn.SetFootballInteractionMode(FootballInteractionMode.CatchHands);
        await ToSignal(GetTree().CreateTimer(0.08), SceneTreeTimer.SignalName.Timeout);
        Require(pawn.AnimationState == HumanoidAnimationState.Sprint &&
                pawn.FacialExpression == FacialExpressionState.Focused &&
                pawn.FootballInteractionMode == FootballInteractionMode.CatchHands,
            "The adapter did not preserve animation, face, or football-contact control paths.");

        var broadcast = new Camera3D { Name = "MigrationBroadcast", CullMask = uint.MaxValue };
        AddChild(broadcast);
        var pov = new Camera3D
        {
            Name = "MigrationPov",
            CullMask = uint.MaxValue & ~HumanoidRig.FirstPersonHeadLayerMask
        };
        AddChild(pov);
        pawn.SetFirstPersonView(true);
        Require(!pawn.HeadGeometryVisibleTo(pov) && pawn.BodyGeometryVisibleTo(pov),
            "POV head hiding no longer preserves body visibility through the adapter.");
        Require(pawn.HeadGeometryVisibleTo(broadcast) && pawn.BodyGeometryVisibleTo(broadcast),
            "Broadcast visibility lost the complete character through the adapter.");

        pawn.QueueFree();
        importedRequest.QueueFree();
        broadcast.QueueFree();
        pov.QueueFree();
    }

    private static bool Finite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
