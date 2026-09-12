using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FlagFootballStudio.Domain;
using FlagFootballStudio.Persistence;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class HairFoundationValidator : Node
{
    private static readonly HairStyle[] RequiredStyles =
    [
        HairStyle.BuzzCut,
        HairStyle.Short,
        HairStyle.Medium,
        HairStyle.Long,
        HairStyle.Curly,
        HairStyle.Ponytail,
        HairStyle.Bun
    ];

    public async Task RunAsync()
    {
        ValidateParameterLimits();
        ValidatePersistence();

        var team = new Team(Guid.NewGuid(), "Hair Validation");
        var player = new Player(Guid.NewGuid(), "Hair Test", 27, PlayerPosition.Receiver);
        team.AddPlayer(player);
        var uniform = UniformDefinition.CreateTeamDefault(team, true);
        var appearance = new PlayerAppearance(player.Id, player.JerseyNumber);
        appearance.SetAccessories(PlayerAccessories.Headband | PlayerAccessories.Visor);

        var rig = new HumanoidRig { Name = "HairValidationRig", Visible = false };
        AddChild(rig);
        rig.Apply(player, appearance, uniform);
        await NextFrame();
        var bodyMesh = rig.BodyMeshResource;
        var faceTopology = HumanoidFaceMesh.TopologySignature(rig.FaceMeshResource);
        var eyeAnchor = rig.EyeAnchor.Position;
        rig.SetFacialExpression(FacialExpressionState.Smile, 0);
        rig.SetAnimationState(HumanoidAnimationState.Jog, true);

        var pieceCounts = new Dictionary<HairStyle, int>();
        foreach (var headExtreme in new[] { false, true })
        {
            ApplyHeadExtreme(appearance.Face, headExtreme);
            foreach (var style in RequiredStyles)
            {
                ApplyHairExtreme(appearance.Hair, style, headExtreme);
                rig.Apply(player, appearance, uniform);
                await NextFrame();
                ValidateRig(rig, style, bodyMesh, faceTopology, eyeAnchor);
                if (pieceCounts.TryGetValue(style, out var expectedCount))
                    Require(rig.HairPieceCount == expectedCount, $"{style} changed piece topology between head extremes.");
                else
                    pieceCounts[style] = rig.HairPieceCount;
            }
        }

        rig.SetAnimationState(HumanoidAnimationState.Sprint, true);
        await WaitSeconds(0.12);
        ValidateVector(rig.HairGlobalPosition, "hair while sprinting");
        rig.SetAnimationState(HumanoidAnimationState.Throw, true);
        await WaitSeconds(0.12);
        ValidateVector(rig.HairGlobalPosition, "hair while throwing");
        Require(rig.HairAttachedToHead, "Hair detached from the head during animation.");
        Require(rig.FacialExpression == FacialExpressionState.Smile, "Hair rebuilding reset facial expression state.");

        rig.QueueFree();
    }

    private static void ValidateParameterLimits()
    {
        var hair = new HairAppearance();
        hair.SetParameters(HairStyle.Long, -10, -10, -10, -10, -10, -10, -10, -10);
        Require(hair.Length == HairAppearance.MinimumLength, "Hair length minimum did not clamp.");
        Require(hair.Volume == HairAppearance.MinimumVolume, "Hair volume minimum did not clamp.");
        Require(hair.HairlineHeight == HairAppearance.MinimumHairlineHeight, "Hairline minimum did not clamp.");
        Require(hair.PartPosition == HairAppearance.MinimumPartPosition, "Part position minimum did not clamp.");
        Require(hair.CurlAmount == HairAppearance.MinimumCurlAmount, "Curl minimum did not clamp.");
        Require(hair.PonytailLength == HairAppearance.MinimumPonytailLength, "Ponytail length minimum did not clamp.");
        Require(hair.PonytailVolume == HairAppearance.MinimumPonytailVolume, "Ponytail volume minimum did not clamp.");
        Require(hair.BunSize == HairAppearance.MinimumBunSize, "Bun size minimum did not clamp.");

        hair.SetParameters(HairStyle.Ponytail, 10, 10, 10, 10, 10, 10, 10, 10);
        Require(hair.Length == HairAppearance.MaximumLength, "Hair length maximum did not clamp.");
        Require(hair.Volume == HairAppearance.MaximumVolume, "Hair volume maximum did not clamp.");
        Require(hair.HairlineHeight == HairAppearance.MaximumHairlineHeight, "Hairline maximum did not clamp.");
        Require(hair.PartPosition == HairAppearance.MaximumPartPosition, "Part position maximum did not clamp.");
        Require(hair.CurlAmount == HairAppearance.MaximumCurlAmount, "Curl maximum did not clamp.");
        Require(hair.PonytailLength == HairAppearance.MaximumPonytailLength, "Ponytail length maximum did not clamp.");
        Require(hair.PonytailVolume == HairAppearance.MaximumPonytailVolume, "Ponytail volume maximum did not clamp.");
        Require(hair.BunSize == HairAppearance.MaximumBunSize, "Bun size maximum did not clamp.");
    }

    private static void ValidatePersistence()
    {
        var game = Game.CreatePrototype();
        var project = GameProject.CreatePrototype(game);
        var player = game.Gold.Roster[0];
        var hair = project.AppearanceFor(player.Id).Hair;
        hair.SetParameters(HairStyle.Ponytail, 1.37f, 1.22f, -0.11f, 0.63f, 0.78f, 1.29f, 1.18f, 1.31f);
        hair.SetColor(new AppearanceColor(92, 43, 27));

        var serializer = new ProjectJsonSerializer();
        var restored = serializer.DeserializeProject(serializer.SerializeProject(project)).AppearanceFor(player.Id).Hair;
        Require(restored.Style == hair.Style, "Hair style did not survive JSON round trip.");
        Require(restored.Length == hair.Length && restored.Volume == hair.Volume, "Hair size did not survive JSON round trip.");
        Require(restored.HairlineHeight == hair.HairlineHeight && restored.PartPosition == hair.PartPosition, "Hairline/part did not survive JSON round trip.");
        Require(restored.CurlAmount == hair.CurlAmount, "Curl amount did not survive JSON round trip.");
        Require(restored.Color == hair.Color, "Hair color did not survive JSON round trip.");
        Require(restored.PonytailLength == hair.PonytailLength && restored.PonytailVolume == hair.PonytailVolume, "Ponytail settings did not survive JSON round trip.");
        Require(restored.BunSize == hair.BunSize, "Bun size did not survive JSON round trip.");
    }

    private static void ApplyHeadExtreme(FaceAppearance face, bool maximum)
    {
        var scale = maximum ? FaceAppearance.MaximumScale : FaceAppearance.MinimumScale;
        var opposite = maximum ? FaceAppearance.MinimumScale : FaceAppearance.MaximumScale;
        var offset = maximum ? FaceAppearance.MaximumOffset : FaceAppearance.MinimumOffset;
        face.SetParameters(
            scale, opposite, scale, opposite, scale, offset, scale, opposite, scale, opposite,
            opposite, offset, scale, opposite, scale, offset, scale, opposite, scale, offset);
    }

    private static void ApplyHairExtreme(HairAppearance hair, HairStyle style, bool maximum)
    {
        hair.SetParameters(
            style,
            maximum ? HairAppearance.MaximumLength : HairAppearance.MinimumLength,
            maximum ? HairAppearance.MaximumVolume : HairAppearance.MinimumVolume,
            maximum ? HairAppearance.MaximumHairlineHeight : HairAppearance.MinimumHairlineHeight,
            maximum ? HairAppearance.MaximumPartPosition : HairAppearance.MinimumPartPosition,
            maximum ? HairAppearance.MaximumCurlAmount : HairAppearance.MinimumCurlAmount,
            maximum ? HairAppearance.MaximumPonytailLength : HairAppearance.MinimumPonytailLength,
            maximum ? HairAppearance.MaximumPonytailVolume : HairAppearance.MinimumPonytailVolume,
            maximum ? HairAppearance.MaximumBunSize : HairAppearance.MinimumBunSize);
    }

    private static void ValidateRig(
        HumanoidRig rig,
        HairStyle style,
        Mesh bodyMesh,
        string faceTopology,
        Vector3 eyeAnchor)
    {
        Require(rig.HairStyle == style, $"The hair rig did not apply {style}.");
        Require(rig.HairPieceCount > 0, $"{style} generated no hair geometry.");
        Require(rig.HasFiniteHairGeometry, $"{style} generated invalid geometry.");
        Require(rig.HairAttachedToHead, $"{style} is not attached to the shared head bone.");
        Require(rig.HairUsesAccessoryClearance, $"{style} did not account for headband/visor clearance.");
        Require(ReferenceEquals(rig.BodyMeshResource, bodyMesh), $"{style} replaced the shared body mesh.");
        Require(HumanoidFaceMesh.TopologySignature(rig.FaceMeshResource) == faceTopology, $"{style} changed face topology.");
        Require(rig.EyeAnchor.Position.IsEqualApprox(eyeAnchor), $"{style} moved the Player POV anchor.");
        ValidateVector(rig.HairGlobalPosition, $"{style} hair root");
    }

    private static void ValidateVector(Vector3 value, string label) =>
        Require(float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z), $"{label} is not finite.");

    private async Task NextFrame() =>
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    private async Task WaitSeconds(double seconds) =>
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
