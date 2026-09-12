using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlagFootballStudio.Domain;
using FlagFootballStudio.Persistence;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class PovFreeLookValidator : Node3D
{
    private readonly Dictionary<Guid, Node3D> _pawns = [];

    public async Task RunAsync()
    {
        ValidateInputActions();
        var game = Game.CreatePrototype();
        var project = GameProject.CreatePrototype(game);
        var play = project.Plays[0];
        SpawnPlayers(game, project, play);
        await NextFrame();

        var football = new FootballView { Name = "PovFreeLookFootball", Position = new Vector3(2, 0.9f, 3) };
        AddChild(football);
        var cameraHome = new Node3D { Name = "PovFreeLookCameraHome" };
        AddChild(cameraHome);
        var camera = new Camera3D { Name = "PovFreeLookCamera" };
        cameraHome.AddChild(camera);
        var broadcastCamera = new Camera3D { Name = "PovFreeLookBroadcastCamera" };
        cameraHome.AddChild(broadcastCamera);
        var controller = new CameraDirectorController { Name = "PovFreeLookController" };
        AddChild(controller);
        controller.Configure(camera, cameraHome, _pawns, football);

        var quarterback = Pawn(play.QuarterbackId);
        var definition = new CameraDefinition(Guid.NewGuid(), "Interactive POV", CameraType.PlayerPov);
        definition.SetPlayer(quarterback.Player!.Id);
        controller.Preview(definition, play);
        await NextFrame();
        ValidateLockedForward(controller, camera, quarterback);
        ValidateVisibility(camera, broadcastCamera, quarterback);

        var freeLook = PlayerPovSettings.Default with
        {
            Mode = PlayerPovMode.FreeLook,
            MouseSensitivity = 0.2f,
            ControllerSensitivity = 140,
            StabilizationStrength = 0.8f,
            HeadBobStrength = 0.65f,
            PitchOffsetDegrees = -4,
            ForwardOffset = 0.06f,
            NearClip = 0.02f
        };
        definition.SetPlayerPovSettings(freeLook);
        var playerForward = quarterback.ForwardDirection;
        var playerYaw = quarterback.Rotation.Y;
        var gazeHorizontal = quarterback.TargetHorizontalGazeDegrees;
        var gazeVertical = quarterback.TargetVerticalGazeDegrees;
        controller.Preview(definition, play);
        Require(controller.MouseCaptureActive, "Entering Free Look did not request mouse capture.");
        controller._UnhandledInput(new InputEventMouseMotion { Relative = new Vector2(-10000, -10000) });
        Require(Mathf.IsEqualApprox(controller.FreeLookYawDegrees, CameraDirectorController.MaximumFreeLookYawDegrees),
            "Free Look yaw did not clamp at its positive limit.");
        Require(Mathf.IsEqualApprox(controller.FreeLookPitchDegrees, CameraDirectorController.MaximumFreeLookPitchDegrees),
            "Free Look pitch did not clamp at its upper limit.");
        controller.ApplyFreeLookDelta(new Vector2(20000, 20000));
        Require(Mathf.IsEqualApprox(controller.FreeLookYawDegrees, CameraDirectorController.MinimumFreeLookYawDegrees),
            "Free Look yaw did not clamp at its negative limit.");
        Require(Mathf.IsEqualApprox(controller.FreeLookPitchDegrees, CameraDirectorController.MinimumFreeLookPitchDegrees),
            "Free Look pitch did not clamp at its lower limit.");
        Require(Mathf.IsEqualApprox(quarterback.Rotation.Y, playerYaw) && quarterback.ForwardDirection.IsEqualApprox(playerForward),
            "Free Look changed football-driven player facing.");
        Require(Mathf.IsEqualApprox(quarterback.TargetHorizontalGazeDegrees, gazeHorizontal) &&
                Mathf.IsEqualApprox(quarterback.TargetVerticalGazeDegrees, gazeVertical),
            "Free Look changed the independent eye-gaze state.");

        controller._UnhandledInput(new InputEventAction { Action = "pov_recenter", Pressed = true });
        await WaitSeconds(1.15);
        Require(!controller.IsRecentering && Mathf.Abs(controller.FreeLookYawDegrees) < 0.05f &&
                Mathf.Abs(controller.FreeLookPitchDegrees - freeLook.PitchOffsetDegrees) < 0.05f,
            "Free Look recenter did not smoothly return to player forward.");

        ValidateMouseReleaseAndRecapture(controller);
        await ValidateLookTargetsAsync(controller, definition, play, camera, quarterback, game, football);
        await ValidateAnimationStabilityAsync(controller, camera, quarterback);
        ValidateVisibility(camera, broadcastCamera, quarterback);
        ValidatePersistence(project, definition);

        var broadcast = new CameraDefinition(Guid.NewGuid(), "Validation Broadcast", CameraType.BroadcastWide);
        controller.Preview(broadcast, play);
        Require(!controller.MouseCaptureActive && Input.MouseMode != Input.MouseModeEnum.Captured,
            "Mouse capture remained active outside Player POV Free Look.");
        Require(quarterback.HeadGeometryVisibleTo(camera), "Leaving Player POV did not restore full character visibility.");

        controller.QueueFree();
        camera.QueueFree();
        broadcastCamera.QueueFree();
        cameraHome.QueueFree();
        football.QueueFree();
        foreach (var pawn in _pawns.Values)
            pawn.QueueFree();
    }

    private void SpawnPlayers(Game game, GameProject project, PlayDefinition play)
    {
        foreach (var team in new[] { game.Gold, game.Navy })
        {
            var uniform = project.ActiveUniformFor(team.Id);
            foreach (var player in team.Roster)
            {
                var pawn = new PlayerPawn { Name = $"PovFreeLook_{team.Name}_{player.Name}" };
                pawn.Configure(player, project.AppearanceFor(player.Id), uniform);
                AddChild(pawn);
                pawn.Position = ToWorld(play.StartingPositions[player.Id]);
                _pawns[player.Id] = pawn;
            }
        }
        FormationFacing.Apply(play, game.Gold, game.Navy, _pawns);
    }

    private static void ValidateLockedForward(CameraDirectorController controller, Camera3D camera, PlayerPawn pawn)
    {
        Require(controller.ActivePovMode == PlayerPovMode.LockedForward,
            "Default Player POV mode is not Locked Forward.");
        Require(Mathf.Abs(controller.FreeLookYawDegrees) < 0.001f,
            "Locked Forward added an unexpected yaw offset.");
        Require(Mathf.IsEqualApprox(controller.FreeLookPitchDegrees, PlayerPovSettings.Default.PitchOffsetDegrees),
            "Locked Forward did not preserve the established pitch offset.");
        Require(camera.GetParent() != pawn.EyeAnchor,
            "Locked Forward bypassed the stabilized eye mount.");
    }

    private static void ValidateVisibility(Camera3D camera, Camera3D broadcastCamera, PlayerPawn pawn)
    {
        Require(!pawn.HeadGeometryVisibleTo(camera), "POV camera rendered the selected head/face/hair.");
        Require(pawn.HeadGeometryVisibleTo(broadcastCamera), "POV hiding affected the broadcast camera.");
        Require(pawn.BodyGeometryVisibleTo(camera), "POV camera hid the selected player's body.");
    }

    private static void ValidateMouseReleaseAndRecapture(CameraDirectorController controller)
    {
        controller._UnhandledInput(new InputEventAction { Action = "ui_cancel", Pressed = true });
        Require(!controller.MouseCaptureActive, "Escape did not release Free Look mouse capture.");
        controller._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });
        Require(controller.MouseCaptureActive, "Clicking back into Free Look did not recapture the mouse.");
        controller._UnhandledInput(new InputEventAction { Action = "ui_cancel", Pressed = true });
        Require(!controller.MouseCaptureActive, "Second Escape did not return mouse control to normal UI.");
    }

    private async Task ValidateLookTargetsAsync(
        CameraDirectorController controller,
        CameraDefinition definition,
        PlayDefinition play,
        Camera3D camera,
        PlayerPawn quarterback,
        Game game,
        Node3D football)
    {
        var gaze = new Vector2(quarterback.TargetHorizontalGazeDegrees, quarterback.TargetVerticalGazeDegrees);
        var targetPlayer = Pawn(game.Navy.Roster.Single(player => player.JerseyNumber == 14).Id);
        foreach (var target in new[]
                 {
                     (PlayerPovTargetKind.Player, (Guid?)targetPlayer.Player!.Id, CameraVectorFor(Vector3.Zero), targetPlayer.EyeAnchor.GlobalPosition, "player"),
                     (PlayerPovTargetKind.Football, (Guid?)null, CameraVectorFor(Vector3.Zero), football.GlobalPosition, "football"),
                     (PlayerPovTargetKind.WorldPoint, (Guid?)null, new CameraVector(-3, 2.2f, 6), new Vector3(-3, 2.2f, 6), "world point")
                 })
        {
            definition.SetPlayerPovSettings(PlayerPovSettings.Default with
            {
                Mode = PlayerPovMode.LookAtTarget,
                TargetKind = target.Item1,
                TargetPlayerId = target.Item2,
                WorldTarget = target.Item3
            });
            controller.Preview(definition, play);
            await WaitSeconds(0.8);
            var direction = (target.Item4 - camera.GlobalPosition).Normalized();
            Require((-camera.GlobalBasis.Z).Dot(direction) > 0.96f, $"Look At {target.Item5} did not converge on its target.");
            Require(Mathf.IsEqualApprox(quarterback.TargetHorizontalGazeDegrees, gaze.X) &&
                    Mathf.IsEqualApprox(quarterback.TargetVerticalGazeDegrees, gaze.Y),
                $"Look At {target.Item5} overwrote eye gaze.");
        }
    }

    private async Task ValidateAnimationStabilityAsync(
        CameraDirectorController controller,
        Camera3D camera,
        PlayerPawn pawn)
    {
        foreach (var state in new[]
                 {
                     HumanoidAnimationState.Idle,
                     HumanoidAnimationState.Jog,
                     HumanoidAnimationState.Sprint,
                     HumanoidAnimationState.Throw,
                     HumanoidAnimationState.Catch,
                     HumanoidAnimationState.FlagPull
                 })
        {
            pawn.SetAnimationState(state, true);
            await WaitSeconds(0.25);
            Require(controller.PovTrackingError < 0.25f, $"Interactive POV lost the mount during {state}.");
            Require(pawn.BodyGeometryVisibleTo(camera), $"Interactive POV body disappeared during {state}.");
            ValidateVector(camera.GlobalPosition, $"Interactive POV camera during {state}");
        }
    }

    private static void ValidatePersistence(GameProject project, CameraDefinition definition)
    {
        definition.SetPlayerPovSettings(PlayerPovSettings.Default with
        {
            Mode = PlayerPovMode.LookAtTarget,
            MouseSensitivity = 0.21f,
            ControllerSensitivity = 155,
            StabilizationStrength = 0.83f,
            HeadBobStrength = 0.42f,
            PitchOffsetDegrees = -9,
            ForwardOffset = 0.075f,
            NearClip = 0.018f,
            TargetKind = PlayerPovTargetKind.WorldPoint,
            WorldTarget = new CameraVector(4, 2, 8)
        });
        project.AddCamera(definition);
        var serializer = new ProjectJsonSerializer();
        var json = serializer.SerializeProject(project);
        var restored = serializer.DeserializeProject(json).Camera(definition.Id);
        Require(restored.PovSettings == definition.PovSettings,
            "Saved Player POV configuration did not survive JSON round trip.");
        Require(!json.Contains("freeLookYaw", StringComparison.OrdinalIgnoreCase) &&
                !json.Contains("freeLookPitch", StringComparison.OrdinalIgnoreCase),
            "Transient Free Look angles leaked into project JSON.");
    }

    private static void ValidateInputActions()
    {
        foreach (var action in new[] { "pov_recenter", "pov_look_left", "pov_look_right", "pov_look_up", "pov_look_down" })
            Require(InputMap.HasAction(action), $"Input action {action} is missing.");
        foreach (var action in new[] { "pov_look_left", "pov_look_right", "pov_look_up", "pov_look_down" })
            Require(InputMap.ActionGetEvents(action).Any(input => input is InputEventJoypadMotion),
                $"Input action {action} has no right-stick binding.");
        Require(InputMap.ActionGetEvents("pov_recenter").Any(input => input is InputEventKey) &&
                InputMap.ActionGetEvents("pov_recenter").Any(input => input is InputEventJoypadButton),
            "POV recenter is missing its keyboard or controller binding.");
    }

    private PlayerPawn Pawn(Guid playerId) => (PlayerPawn)_pawns[playerId];

    private static Vector3 ToWorld(PlayPoint point) => new(point.X, 0.08f, point.Y);
    private static CameraVector CameraVectorFor(Vector3 point) => new(point.X, point.Y, point.Z);

    private async Task NextFrame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private async Task WaitSeconds(double seconds) =>
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private static void ValidateVector(Vector3 value, string context) =>
        Require(float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z), $"{context} is not finite.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
