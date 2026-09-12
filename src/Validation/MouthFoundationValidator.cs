using System;
using System.Threading.Tasks;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class MouthFoundationValidator : Node
{
    public async Task RunAsync()
    {
        var team = new Team(Guid.NewGuid(), "Mouth Validation");
        var player = new Player(Guid.NewGuid(), "Mouth Test", 16, PlayerPosition.Quarterback);
        team.AddPlayer(player);
        var uniform = UniformDefinition.CreateTeamDefault(team, true);
        var appearance = new PlayerAppearance(player.Id, player.JerseyNumber);
        appearance.Hair.SetParameters(HairStyle.Long, 1.2f, 1.1f, 0, 0.2f, 0.45f, 1, 1, 1);

        var rig = new HumanoidRig { Name = "MouthValidationRig", Visible = false };
        AddChild(rig);
        rig.Apply(player, appearance, uniform);
        await NextFrame();

        var topology = HumanoidFaceMesh.TopologySignature(rig.FaceMeshResource);
        var bodyMesh = rig.BodyMeshResource;
        var eyeAnchor = rig.EyeAnchor.Position;
        var hairPieces = rig.HairPieceCount;

        foreach (var maximum in new[] { false, true })
        {
            ApplyFaceExtreme(appearance.Face, maximum);
            rig.Apply(player, appearance, uniform);
            await NextFrame();
            foreach (var shape in Enum.GetValues<SpeechMouthShape>())
            {
                rig.SetMouthControls(0, 1, 1, 0, 0);
                rig.SetMouthShape(shape, 0);
                await NextFrame();
                ValidateMouthState(rig, shape, topology, $"{shape} at {(maximum ? "maximum" : "minimum")} face extreme");
            }
        }

        await ValidateRepeatedJawOpeningAsync(rig);
        await ValidateLayeredBlendingAsync(rig);
        await ValidateAnimationAndCycleAsync(rig);

        Require(ReferenceEquals(rig.BodyMeshResource, bodyMesh), "Speech preview replaced the shared skinned body mesh.");
        Require(rig.HairPieceCount == hairPieces, "Speech preview changed procedural hair geometry.");
        Require(rig.EyeAnchor.Position.IsEqualApprox(eyeAnchor), "Speech preview moved the Player POV anchor.");
        Require(rig.MouthAttachedToHead, "Mouth presentation detached from the shared head bone.");
        rig.QueueFree();
    }

    private async Task ValidateRepeatedJawOpeningAsync(HumanoidRig rig)
    {
        rig.SetFacialExpression(FacialExpressionState.Neutral, 0);
        rig.SetMouthShape(SpeechMouthShape.Rest, 0);
        for (var index = 0; index < 5; index++)
        {
            rig.SetMouthControls(1, 1, 1, 0, 0);
            await NextFrame();
            Require(rig.MouthOpening > 0.95f, $"Jaw opening {index + 1} did not reach its maximum.");
            rig.SetMouthControls(0, 1, 1, 0, 0);
            await NextFrame();
            Require(rig.MouthOpening < 0.01f, $"Jaw opening {index + 1} did not return to rest.");
        }
    }

    private async Task ValidateLayeredBlendingAsync(HumanoidRig rig)
    {
        rig.SetMouthControls(0, 1, 1, 0.25f, 0.2f);
        rig.SetFacialExpression(FacialExpressionState.Neutral, 0);
        rig.SetMouthShape(SpeechMouthShape.Rest, 0);
        rig.SetFacialExpression(FacialExpressionState.Smile, 0.3f);
        rig.SetMouthShape(SpeechMouthShape.O, 0.3f);
        await WaitSeconds(0.1);
        Require(rig.FacialPose.Smile is > 0 and < 1, "Expression did not interpolate during layered mouth preview.");
        Require(rig.MouthPose.JawOpen is > 0 and < 0.62f, "Speech shape did not interpolate during expression blending.");
        await WaitSeconds(0.25);
        Require(Mathf.IsEqualApprox(rig.FacialPose.Smile, 1), "Layered smile did not finish blending.");
        Require(rig.MouthPose.JawOpen > 0.6f, "Layered O shape did not finish blending.");
        Require(rig.MouthPose.UpperLipRaise > SpeechMouthPose.For(SpeechMouthShape.O).UpperLipRaise, "Manual upper-lip control was lost during speech blending.");
        Require(rig.MouthPose.LowerLipDrop > SpeechMouthPose.For(SpeechMouthShape.O).LowerLipDrop, "Manual lower-lip control was lost during speech blending.");
        Require(rig.MouthOpening > rig.MouthPose.JawOpen, "Expression and speech jaw opening did not layer together.");
    }

    private async Task ValidateAnimationAndCycleAsync(HumanoidRig rig)
    {
        rig.SetMouthControls(0, 1, 1, 0, 0);
        rig.SetAnimationState(HumanoidAnimationState.Jog, true);
        rig.SetMouthShape(SpeechMouthShape.A, 0.08f);
        await WaitSeconds(0.12);
        Require(rig.AnimationState == HumanoidAnimationState.Jog, "Speech preview interrupted jog animation.");
        Require(rig.MouthShape == SpeechMouthShape.A, "A pose was lost during jog animation.");

        var completedCycles = rig.CompletedSpeechCycles;
        rig.StartSpeechShapeCycle(0.12f);
        rig.SetAnimationState(HumanoidAnimationState.Sprint, true);
        await WaitSeconds(1.45);
        Require(rig.CompletedSpeechCycles == completedCycles + 1, "Speech-shape cycling did not complete deterministically.");
        Require(!rig.IsSpeechShapeCycling, "Speech-shape cycle did not return to rest.");
        Require(rig.MouthShape == SpeechMouthShape.Rest, "Speech-shape cycle did not finish on Rest.");
        Require(rig.AnimationState == HumanoidAnimationState.Sprint, "Speech-shape cycling interrupted sprint animation.");
    }

    private static void ApplyFaceExtreme(FaceAppearance face, bool maximum)
    {
        var scale = maximum ? FaceAppearance.MaximumScale : FaceAppearance.MinimumScale;
        var opposite = maximum ? FaceAppearance.MinimumScale : FaceAppearance.MaximumScale;
        var offset = maximum ? FaceAppearance.MaximumOffset : FaceAppearance.MinimumOffset;
        face.SetParameters(
            scale, opposite, scale, opposite, scale, offset, scale, opposite, scale, opposite,
            opposite, offset, scale, opposite, scale, offset, scale, opposite, scale, offset);
    }

    private static void ValidateMouthState(HumanoidRig rig, SpeechMouthShape shape, string topology, string context)
    {
        Require(rig.MouthShape == shape, $"{context} selected the wrong mouth shape.");
        Require(HumanoidFaceMesh.TopologySignature(rig.FaceMeshResource) == topology, $"{context} changed fixed face topology.");
        Require(rig.HasFiniteMouthGeometry, $"{context} generated invalid inner-mouth geometry.");
        Require(rig.MouthAttachedToHead, $"{context} detached the mouth rig.");
        Require(float.IsFinite(rig.MouthOpening) && rig.MouthOpening is >= 0 and <= 1, $"{context} produced an unsafe jaw opening.");
        ValidatePose(rig.MouthPose, context);
        var bounds = rig.FaceMeshResource.GetAabb();
        ValidateVector(bounds.Position, $"{context} face bounds");
        ValidateVector(bounds.Size, $"{context} face size");
    }

    private static void ValidatePose(SpeechMouthPose pose, string context)
    {
        Require(
            float.IsFinite(pose.JawOpen) &&
            float.IsFinite(pose.Width) &&
            float.IsFinite(pose.LipFullness) &&
            float.IsFinite(pose.Roundness) &&
            float.IsFinite(pose.UpperLipRaise) &&
            float.IsFinite(pose.LowerLipDrop) &&
            float.IsFinite(pose.CornerPull) &&
            float.IsFinite(pose.TeethExposure),
            $"{context} produced a non-finite mouth pose.");
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
