using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using FlagFootballStudio.Domain;
using FlagFootballStudio.Persistence;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class CameraProfileValidator : Node3D
{
    private readonly Dictionary<Guid, Node3D> _pawns = [];

    public async Task RunAsync()
    {
        var game = Game.CreatePrototype();
        var project = GameProject.CreatePrototype(game);
        var play = project.Plays[0];
        SpawnPlayers(game, project, play);
        var football = new FootballView { Position = new Vector3(0, 0.9f, 0) }; AddChild(football);
        var home = new Node3D(); AddChild(home);
        var camera = new Camera3D { Current = true }; home.AddChild(camera);
        var controller = new CameraDirectorController(); AddChild(controller);
        controller.Configure(camera, home, _pawns, football);
        await NextFrame();

        var quarterback = (PlayerPawn)_pawns[play.QuarterbackId];
        var pov = new CameraDefinition(Guid.NewGuid(), "Action POV", CameraType.PlayerPov);
        pov.SetPlayer(play.QuarterbackId);
        var presetFovs = new Dictionary<PlayerPovLensPreset, (float Horizontal, float Vertical)>
        {
            [PlayerPovLensPreset.GoProWide] = (100, 75),
            [PlayerPovLensPreset.GoProLinear] = (90, 65),
            [PlayerPovLensPreset.GoProSuperView] = (120, 95),
            [PlayerPovLensPreset.Narrow] = (60, 45)
        };
        foreach (var preset in presetFovs)
        {
            pov.SetPlayerPovSettings(PlayerPovSettings.Default with
            {
                LensPreset = preset.Key,
                HorizontalFieldOfView = preset.Value.Horizontal,
                VerticalFieldOfView = preset.Value.Vertical,
                DistortionStrength = preset.Key == PlayerPovLensPreset.GoProSuperView ? 0.5f : 0
            });
            controller.Preview(pov, play, false);
            Require(Mathf.IsEqualApprox(camera.Fov, CameraDirectorController.EffectivePovVerticalFov(pov.PovSettings)),
                $"{preset.Key} did not apply its action-camera projection.");
            Require(quarterback.BodyGeometryVisibleTo(camera) && !quarterback.HeadGeometryVisibleTo(camera),
                "Action POV did not retain body-visible/head-hidden rendering.");
        }
        foreach (var mount in Enum.GetValues<PlayerPovMount>())
        {
            pov.SetPlayerPovSettings(pov.PovSettings with { Mount = mount, UpOffset = 0.03f, ForwardOffset = 0.06f });
            controller.Preview(pov, play, false);
            await NextFrame();
            Require(Finite(camera.GlobalPosition), $"{mount} wearable mount produced an invalid transform.");
        }
        var facing = quarterback.ForwardDirection;
        pov.SetPlayerPovSettings(pov.PovSettings with
        {
            Mode = PlayerPovMode.FreeLook,
            StabilizationStrength = 0.8f,
            MotionSmoothing = 0.75f,
            HorizonLeveling = 0.9f
        });
        controller.Preview(pov, play, false);
        controller.ApplyFreeLookDelta(new Vector2(-120, 60));
        Require(quarterback.ForwardDirection.IsEqualApprox(facing), "Action-camera Free Look changed player facing.");

        var target = game.Gold.Roster[2];
        var sideline = new CameraDefinition(Guid.NewGuid(), "Sideline Operator", CameraType.SidelineLow);
        sideline.SetSidelineSettings(SidelineCameraSettings.Default with
        {
            Behavior = CameraBehaviorPreset.TrackPlayer,
            TargetPlayerId = target.Id,
            Side = SidelineSide.Left,
            CameraHeight = 4,
            SidelineDistance = 3,
            TrackingStrength = 1,
            FocalLengthMm = 35,
            PanDegrees = 2,
            TiltDegrees = -1
        });
        controller.Preview(sideline, play);
        var fixedPosition = camera.GlobalPosition;
        Require(Mathf.IsEqualApprox(fixedPosition.X, -13) && Mathf.IsEqualApprox(fixedPosition.Y, 4) && Mathf.IsEqualApprox(fixedPosition.Z, 0),
            "Sideline camera was not physically placed beyond the selected sideline.");
        sideline.SetSidelineSettings(sideline.SidelineSettings with { Side = SidelineSide.Right });
        controller.Preview(sideline, play);
        Require(Mathf.IsEqualApprox(camera.GlobalPosition.X, 13), "Right sideline selection did not mirror physical placement.");
        sideline.SetSidelineSettings(sideline.SidelineSettings with { Side = SidelineSide.Left });
        controller.Preview(sideline, play);
        var playerPawn = (PlayerPawn)_pawns[target.Id];
        playerPawn.Position += new Vector3(3, 0, 8);
        await WaitSeconds(0.7);
        Require(camera.GlobalPosition.IsEqualApprox(fixedPosition), "Sideline tracking teleported the camera with the play.");
        var playerDirection = (playerPawn.GlobalPosition + Vector3.Up - camera.GlobalPosition).Normalized();
        Require((-camera.GlobalBasis.Z).Dot(playerDirection) > 0.9f, "Sideline pan/tilt did not track the player smoothly.");

        var widePosition = camera.GlobalPosition;
        var wideFov = camera.Fov;
        sideline.SetSidelineSettings(sideline.SidelineSettings with { FocalLengthMm = 70 });
        controller.Preview(sideline, play);
        Require(camera.GlobalPosition.IsEqualApprox(widePosition) && camera.Fov < wideFov,
            "Optical zoom moved the camera instead of narrowing FOV.");
        var wheelFocal = controller.ActiveFocalLengthMm;
        controller._UnhandledInput(new InputEventMouseButton { ButtonIndex = MouseButton.WheelUp, Pressed = true });
        Require(controller.ActiveFocalLengthMm > wheelFocal && camera.GlobalPosition.IsEqualApprox(widePosition),
            "Mouse-wheel zoom did not change focal length in place.");
        controller.ResetSidelineZoom();
        Require(Mathf.IsEqualApprox(controller.ActiveFocalLengthMm, SidelineCameraSettings.Default.FocalLengthMm),
            "Reset Zoom did not restore the default focal length.");

        sideline.SetSidelineSettings(sideline.SidelineSettings with
        {
            Behavior = CameraBehaviorPreset.TrackFootball,
            AutoZoom = true,
            FocalLengthMm = 35,
            TargetScreenSize = 0.45f
        });
        controller.Preview(sideline, play);
        foreach (var state in new[] { HumanoidAnimationState.Jog, HumanoidAnimationState.Catch, HumanoidAnimationState.FlagPull })
        {
            playerPawn.SetAnimationState(state, true);
            football.Position += new Vector3(0.5f, 0, 2);
            await WaitSeconds(0.35);
            var ballDirection = (football.GlobalPosition + ToGodot(sideline.SidelineSettings.FramingOffset) - camera.GlobalPosition).Normalized();
            Require((-camera.GlobalBasis.Z).Dot(ballDirection) > 0.82f, $"Sideline tracking lost the football during {state}.");
            Require(camera.GlobalPosition.IsEqualApprox(widePosition), $"Auto zoom moved the sideline camera during {state}.");
        }

        project.AddCamera(pov); project.AddCamera(sideline);
        project.AddCameraCut(new CameraCut(Guid.NewGuid(), play.Id, pov.Id, 0));
        project.AddCameraCut(new CameraCut(Guid.NewGuid(), play.Id, sideline.Id, 0.05));
        await controller.PlayCutsAsync(project, play);
        Require(controller.IsSidelineActive, "Camera cuts did not transition from action POV to sideline.");
        ValidatePersistence(project, pov, sideline);

        controller.QueueFree(); camera.QueueFree(); home.QueueFree(); football.QueueFree();
        foreach (var pawn in _pawns.Values) pawn.QueueFree();
    }

    private void SpawnPlayers(Game game, GameProject project, PlayDefinition play)
    {
        foreach (var team in new[] { game.Gold, game.Navy })
            foreach (var player in team.Roster)
            {
                var pawn = new PlayerPawn();
                pawn.Configure(player, project.AppearanceFor(player.Id), project.ActiveUniformFor(team.Id));
                AddChild(pawn);
                var point = play.StartingPositions[player.Id];
                pawn.Position = new Vector3(point.X, 0.08f, point.Y);
                _pawns[player.Id] = pawn;
            }
        FormationFacing.Apply(play, game.Gold, game.Navy, _pawns);
    }

    private static void ValidatePersistence(GameProject project, CameraDefinition pov, CameraDefinition sideline)
    {
        var serializer = new ProjectJsonSerializer();
        var restored = serializer.DeserializeProject(serializer.SerializeProject(project));
        Require(restored.Camera(pov.Id).PovSettings == pov.PovSettings,
            "Action-camera settings did not survive JSON round trip.");
        Require(restored.Camera(sideline.Id).SidelineSettings == sideline.SidelineSettings,
            "Sideline lens/tracking settings did not survive JSON round trip.");

        var legacyRoot = JsonNode.Parse(serializer.SerializeProject(project))!.AsObject();
        foreach (var cameraNode in legacyRoot["cameras"]!.AsArray())
        {
            var cameraObject = cameraNode!.AsObject();
            cameraObject.Remove("sidelineCameraSettings");
            if (cameraObject["playerPovSettings"] is not JsonObject povObject) continue;
            foreach (var key in new[]
                     {
                         "horizontalFieldOfView", "verticalFieldOfView", "lensPreset", "distortionStrength",
                         "horizonLeveling", "mount", "upOffset", "motionSmoothing"
                     })
                povObject.Remove(key);
        }
        var legacy = serializer.DeserializeProject(legacyRoot.ToJsonString());
        Require(legacy.Camera(pov.Id).PovSettings.HorizontalFieldOfView == PlayerPovSettings.Default.HorizontalFieldOfView &&
                legacy.Camera(sideline.Id).SidelineSettings == SidelineCameraSettings.Default,
            "Older camera JSON did not receive sensible action/sideline defaults.");
    }

    private async Task NextFrame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private async Task WaitSeconds(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static Vector3 ToGodot(CameraVector value) => new(value.X, value.Y, value.Z);
    private static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
