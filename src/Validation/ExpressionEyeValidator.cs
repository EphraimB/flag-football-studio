using System;
using System.Threading.Tasks;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class ExpressionEyeValidator : Node
{
    public async Task RunAsync()
    {
        var team = new Team(Guid.NewGuid(), "Expression Validation");
        var player = new Player(Guid.NewGuid(), "Expression Test", 12, PlayerPosition.Receiver);
        team.AddPlayer(player);
        var uniform = UniformDefinition.CreateTeamDefault(team, true);
        var appearance = new PlayerAppearance(player.Id, player.JerseyNumber);
        appearance.Face.SetEyeColor(new AppearanceColor(42, 128, 176));
        SetExtremeEyes(appearance.Face);

        var rig = new HumanoidRig { Name = "ExpressionEyeValidationRig", Visible = false };
        AddChild(rig);
        rig.Apply(player, appearance, uniform);
        await NextFrame();

        var eyeAnchorLocal = rig.EyeAnchor.Position;
        var topologySignature = HumanoidFaceMesh.TopologySignature(rig.FaceMeshResource);
        ValidateExtremeEyes(rig, appearance.Face);
        await ValidateGazeLimitsAsync(rig);
        await ValidateGazeTargetsAsync(rig);
        await ValidateRepeatedBlinkingAsync(rig);
        await ValidateExpressionsDuringRunningAsync(rig, topologySignature);

        Require(rig.EyeAnchor.Position.IsEqualApprox(eyeAnchorLocal), "Facial presentation moved the stable Player POV anchor.");
        var camera = new Camera3D { Name = "ExpressionValidationPovCamera" };
        AddChild(camera);
        camera.Reparent(rig.EyeAnchor, false);
        Require(camera.GetParent() == rig.EyeAnchor, "Player POV camera did not remain attachable after expression and gaze changes.");
        ValidateVector(camera.GlobalPosition, "Player POV camera");

        camera.QueueFree();
        rig.QueueFree();
    }

    private static void SetExtremeEyes(FaceAppearance face) => face.SetParameters(
        FaceAppearance.MinimumScale,
        1,
        1,
        1,
        1,
        0,
        1,
        1,
        1,
        FaceAppearance.MinimumScale,
        FaceAppearance.MaximumScale,
        FaceAppearance.MaximumOffset,
        1,
        FaceAppearance.MaximumScale,
        1,
        0,
        1,
        1,
        1,
        0);

    private static void ValidateExtremeEyes(HumanoidRig rig, FaceAppearance face)
    {
        Require(face.EyeSpacing == FaceAppearance.MinimumScale, "Minimum eye spacing was not preserved.");
        Require(face.EyeSize <= 0.65f + face.EyeSpacing * 0.45f, "Extreme eye size was not safely constrained by spacing.");
        Require(Mathf.IsEqualApprox(rig.EyeSize, face.EyeSize), "The eye rig did not use the safely clamped eye size.");
        Require(rig.EyeSeparation > rig.EyeSize * 0.1f, "Extreme eyes collapsed into the same position.");
    }

    private async Task ValidateGazeLimitsAsync(HumanoidRig rig)
    {
        rig.SetManualGaze(-10, 10);
        Require(Mathf.IsEqualApprox(rig.TargetHorizontalGazeDegrees, -HumanoidEyeRig.MaximumHorizontalGazeDegrees), "Negative horizontal gaze did not clamp.");
        Require(Mathf.IsEqualApprox(rig.TargetVerticalGazeDegrees, HumanoidEyeRig.MaximumVerticalGazeDegrees), "Positive vertical gaze did not clamp.");
        await WaitSeconds(0.3);
        Require(Mathf.IsEqualApprox(rig.HorizontalGazeDegrees, -HumanoidEyeRig.MaximumHorizontalGazeDegrees), "Eyes did not reach the negative horizontal gaze limit.");
        Require(Mathf.IsEqualApprox(rig.VerticalGazeDegrees, HumanoidEyeRig.MaximumVerticalGazeDegrees), "Eyes did not reach the positive vertical gaze limit.");

        rig.SetManualGaze(10, -10);
        Require(Mathf.IsEqualApprox(rig.TargetHorizontalGazeDegrees, HumanoidEyeRig.MaximumHorizontalGazeDegrees), "Positive horizontal gaze did not clamp.");
        Require(Mathf.IsEqualApprox(rig.TargetVerticalGazeDegrees, -HumanoidEyeRig.MaximumVerticalGazeDegrees), "Negative vertical gaze did not clamp.");
        await WaitSeconds(0.5);
        Require(Mathf.IsEqualApprox(rig.HorizontalGazeDegrees, HumanoidEyeRig.MaximumHorizontalGazeDegrees), "Eyes did not reach the positive horizontal gaze limit.");
        Require(Mathf.IsEqualApprox(rig.VerticalGazeDegrees, -HumanoidEyeRig.MaximumVerticalGazeDegrees), "Eyes did not reach the negative vertical gaze limit.");
    }

    private async Task ValidateGazeTargetsAsync(HumanoidRig rig)
    {
        var otherPlayer = new Node3D { Name = "OtherPlayerGazeTarget", Position = new Vector3(4, 2, -5) };
        var football = new Node3D { Name = "FootballGazeTarget", Position = new Vector3(-3, 1, -4) };
        AddChild(otherPlayer);
        AddChild(football);

        rig.LookAtGazeTarget(otherPlayer);
        await WaitSeconds(0.1);
        ValidateGaze(rig, "another-player target");
        rig.LookAtGazeTarget(football);
        await WaitSeconds(0.1);
        ValidateGaze(rig, "football target");
        rig.LookAtWorldPoint(new Vector3(0, 6, -8));
        await WaitSeconds(0.1);
        ValidateGaze(rig, "world-space target");

        otherPlayer.QueueFree();
        football.QueueFree();
    }

    private async Task ValidateRepeatedBlinkingAsync(HumanoidRig rig)
    {
        var initialCount = rig.BlinkCount;
        for (var index = 0; index < 3; index++)
        {
            rig.TriggerBlink(0.12f);
            await WaitSeconds(0.04);
            Require(rig.BlinkAmount > 0, $"Blink {index + 1} never closed the eyelids.");
            Require(rig.EyelidClosure > 0.5f, $"Blink {index + 1} did not visibly close both eyelids.");
            await WaitSeconds(0.12);
            Require(rig.BlinkAmount < 0.05f, $"Blink {index + 1} did not reopen the eyelids.");
        }
        Require(rig.BlinkCount == initialCount + 3, "Repeated manual blinks were not tracked deterministically.");

        var beforeAutomatic = rig.BlinkCount;
        rig.SetAutomaticBlink(true, 0.2f);
        await WaitSeconds(0.26);
        rig.SetAutomaticBlink(false);
        Require(rig.BlinkCount == beforeAutomatic + 1, "Deterministic automatic blinking did not trigger at the configured interval.");
    }

    private async Task ValidateExpressionsDuringRunningAsync(HumanoidRig rig, string topologySignature)
    {
        rig.SetAnimationState(HumanoidAnimationState.Jog, true);
        rig.SetFacialExpression(FacialExpressionState.Neutral, 0);
        rig.SetEyebrowControl(0.4f, -0.6f);
        await NextFrame();
        Require(Mathf.IsEqualApprox(rig.FacialPose.BrowRaise, 0.4f), "Independent eyebrow raise control was not applied.");
        Require(Mathf.IsEqualApprox(rig.FacialPose.BrowTilt, -0.6f), "Independent eyebrow tilt control was not applied.");
        rig.SetEyebrowControl(0, 0);
        rig.SetFacialExpression(FacialExpressionState.Smile, 0.3f);
        await WaitSeconds(0.1);
        Require(rig.FacialPose.Smile is > 0 and < 1, "Smile did not interpolate smoothly.");
        await WaitSeconds(0.25);
        Require(Mathf.IsEqualApprox(rig.FacialPose.Smile, 1), "Smile did not finish blending.");

        foreach (var expression in Enum.GetValues<FacialExpressionState>())
        {
            rig.SetFacialExpression(expression, 0.06f);
            await WaitSeconds(0.08);
            Require(rig.FacialExpression == expression, $"Expression did not switch to {expression}.");
            Require(rig.AnimationState == HumanoidAnimationState.Jog, $"Switching to {expression} interrupted the running animation.");
            Require(HumanoidFaceMesh.TopologySignature(rig.FaceMeshResource) == topologySignature, $"{expression} changed the fixed face topology.");
            ValidatePose(rig.FacialPose, expression.ToString());
        }
    }

    private static void ValidateGaze(HumanoidRig rig, string context)
    {
        Require(Mathf.Abs(rig.TargetHorizontalGazeDegrees) <= HumanoidEyeRig.MaximumHorizontalGazeDegrees, $"Horizontal {context} exceeded its limit.");
        Require(Mathf.Abs(rig.TargetVerticalGazeDegrees) <= HumanoidEyeRig.MaximumVerticalGazeDegrees, $"Vertical {context} exceeded its limit.");
        Require(float.IsFinite(rig.TargetHorizontalGazeDegrees) && float.IsFinite(rig.TargetVerticalGazeDegrees), $"{context} produced a non-finite gaze.");
    }

    private static void ValidatePose(FacialExpressionPose pose, string context)
    {
        Require(
            float.IsFinite(pose.BrowRaise) &&
            float.IsFinite(pose.BrowTilt) &&
            float.IsFinite(pose.Smile) &&
            float.IsFinite(pose.MouthOpen) &&
            float.IsFinite(pose.EyelidClosure),
            $"{context} produced a non-finite expression pose.");
    }

    private static void ValidateVector(Vector3 vector, string label) =>
        Require(float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z), $"{label} is not finite.");

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
