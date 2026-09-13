using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class FacingPovValidator : Node3D
{
    private readonly Dictionary<Guid, Node3D> _pawns = [];

    public async Task RunAsync()
    {
        var game = Game.CreatePrototype();
        var project = GameProject.CreatePrototype(game);
        var play = project.Plays[0];
        SpawnPlayers(game, project);
        await NextFrame();

        ApplyFormation(play, game.Gold, game.Navy);
        ValidateTeamFacing(game.Gold, Vector3.Back, "Gold offense");
        ValidateTeamFacing(game.Navy, Vector3.Forward, "Navy defense");
        ValidatePreSnapRoles(game, play, Vector3.Back, Vector3.Forward);

        project.SetGameState(0, 0, 1, 600, 1, 10, game.Navy.Id);
        ApplyFormation(play, game.Navy, game.Gold);
        ValidateTeamFacing(game.Navy, Vector3.Forward, "Navy offense after possession reversal");
        ValidateTeamFacing(game.Gold, Vector3.Back, "Gold defense after possession reversal");

        var mirroredPlay = play.Duplicate("Mirrored Validation Play");
        foreach (var position in play.StartingPositions)
            mirroredPlay.SetStartingPosition(position.Key, new PlayPoint(position.Value.X, -position.Value.Y));
        ApplyFormation(mirroredPlay, game.Gold, game.Navy);
        ValidateTeamFacing(game.Gold, Vector3.Forward, "mirrored play offense");
        ValidateTeamFacing(game.Navy, Vector3.Back, "mirrored play defense");

        var receiver = Pawn(play.IntendedReceiverId);
        receiver.FaceToward(receiver.GlobalPosition + Vector3.Forward, 0);
        ApplyFormation(play, game.Gold, game.Navy);
        Require(receiver.FacingAlignment(Vector3.Back) > 0.99f, "Formation reset did not restore receiver facing.");

        project.SetGameState(0, 0, 1, 600, 1, 10, game.Gold.Id);
        var football = new FootballView { Name = "FacingPovValidationFootball" };
        AddChild(football);
        var status = new Label { Name = "FacingPovValidationStatus" };
        AddChild(status);
        PositionFootball(play, game.Gold, game.Navy, football);

        var sequence = new PlaySequenceController { Name = "FacingPovValidationSequence" };
        AddChild(sequence);
        sequence.Configure(play, _pawns, football, this, status, game.Gold, game.Navy);
        var playTask = sequence.RunAsync();
        await WaitSeconds(play.SimulationSettings.PreSnapDelay + play.SimulationSettings.DefensiveReactionDelay + 0.55);
        ValidateRouteFacing(play, receiver, Pawn(play.CoverageAssignments.First().Key));
        await playTask;

        var quarterback = Pawn(play.QuarterbackId);
        Require(quarterback.FacingAlignment(receiver.GlobalPosition - quarterback.GlobalPosition) > 0.8f,
            "Quarterback did not remain oriented toward the receiver for the throw.");

        await ValidatePlayerPovAsync(
            play,
            receiver,
            football,
            project.AppearanceFor(receiver.Player!.Id),
            project.ActiveUniformFor(receiver.Player.Team!.Id));

        sequence.QueueFree();
        football.QueueFree();
        status.QueueFree();
        foreach (var pawn in _pawns.Values)
            pawn.QueueFree();
    }

    private void SpawnPlayers(Game game, GameProject project)
    {
        foreach (var team in new[] { game.Gold, game.Navy })
        {
            var uniform = project.ActiveUniformFor(team.Id);
            foreach (var player in team.Roster)
            {
                var pawn = new PlayerPawn { Name = $"Validation_{team.Name}_{player.Name}" };
                pawn.Configure(player, project.AppearanceFor(player.Id), uniform);
                AddChild(pawn);
                _pawns[player.Id] = pawn;
            }
        }
    }

    private void ApplyFormation(PlayDefinition play, Team offense, Team defense)
    {
        foreach (var position in play.StartingPositions)
            _pawns[position.Key].Position = ToWorld(position.Value);
        FormationFacing.Apply(play, offense, defense, _pawns);
        foreach (var pawn in _pawns.Values.OfType<PlayerPawn>())
            pawn.ResetPresentationPose();
    }

    private static void PositionFootball(PlayDefinition play, Team offense, Team defense, Node3D football)
    {
        var direction = PlayDirectionResolver.Resolve(play, offense, defense);
        var snapper = offense.Roster
            .Where(player => play.StartingPositions.ContainsKey(player.Id))
            .OrderByDescending(player => play.StartingPositions[player.Id].Y * PlayDirectionResolver.Sign(direction))
            .First();
        var position = play.StartingPositions[snapper.Id];
        football.Position = new Vector3(position.X, 0.9f, position.Y + PlayDirectionResolver.Sign(direction) * 0.35f);
    }

    private void ValidatePreSnapRoles(Game game, PlayDefinition play, Vector3 receiverDirection, Vector3 defenderDirection)
    {
        Require(Pawn(play.IntendedReceiverId).FacingAlignment(receiverDirection) > 0.99f,
            "Receiver did not face the defensive side before the snap.");
        foreach (var defenderId in play.CoverageAssignments.Keys)
            Require(Pawn(defenderId).FacingAlignment(defenderDirection) > 0.99f,
                "Assigned defender did not face the offense before the snap.");
        Require(Pawn(play.QuarterbackId).FacingAlignment(receiverDirection) > 0.99f,
            "Quarterback did not face the attacking direction before the snap.");
    }

    private void ValidateTeamFacing(Team team, Vector3 direction, string context)
    {
        foreach (var player in team.Roster)
            Require(Pawn(player.Id).FacingAlignment(direction) > 0.99f, $"{context} facing failed for {player.Name}.");
    }

    private void ValidateRouteFacing(PlayDefinition play, PlayerPawn receiver, PlayerPawn defender)
    {
        var receiverTarget = ToWorld(play.Routes[receiver.Player!.Id][0]);
        Require(receiver.FacingAlignment(receiverTarget - receiver.GlobalPosition) > 0.85f,
            "Receiver did not turn toward the route movement direction.");

        Require(defender.FacingAlignment(receiver.GlobalPosition - defender.GlobalPosition) > 0.8f,
            "Defender did not turn toward the assigned receiver.");
    }

    private async Task ValidatePlayerPovAsync(
        PlayDefinition play,
        PlayerPawn pawn,
        FootballView football,
        PlayerAppearance appearance,
        UniformDefinition uniform)
    {
        var cameraHome = new Node3D { Name = "FacingPovCameraHome" };
        AddChild(cameraHome);
        var povCamera = new Camera3D { Name = "FacingPovCamera" };
        cameraHome.AddChild(povCamera);
        var broadcastCamera = new Camera3D { Name = "FacingPovBroadcastCheck" };
        cameraHome.AddChild(broadcastCamera);
        var controller = new CameraDirectorController { Name = "FacingPovCameraController" };
        AddChild(controller);
        controller.Configure(povCamera, cameraHome, _pawns);

        var definition = new CameraDefinition(Guid.NewGuid(), "Validation POV", CameraType.PlayerPov);
        definition.SetPlayer(pawn.Player!.Id);
        controller.Preview(definition, play);
        await NextFrame();

        Require(controller.IsPlayerPovActive && controller.ActivePovPlayerId == pawn.Player.Id,
            "Player POV did not activate for the selected player.");
        Require(Mathf.IsEqualApprox(povCamera.Near, CameraDirectorController.DefaultPlayerPovNearClip),
            "Player POV near clip was not configured.");
        Require(!pawn.HeadGeometryVisibleTo(povCamera), "The selected player's head/face/hair remained visible to the POV camera.");
        Require(pawn.HeadGeometryVisibleTo(broadcastCamera), "POV head hiding also hid the player from a broadcast camera.");
        Require(pawn.BodyGeometryVisibleTo(povCamera), "The selected player's body was hidden from the POV camera.");
        var bodyBounds = pawn.BodyBounds;
        Require(bodyBounds.Size.X > 0.8f, "First-person body bounds do not include both arms.");
        Require(bodyBounds.Size.Y > 1.8f, "First-person body bounds do not include torso and legs.");
        Require(bodyBounds.Size.Z > 0.35f, "First-person body bounds do not include the feet/chest depth.");

        pawn.ApplyPresentation(appearance, uniform);
        await NextFrame();
        Require(!pawn.HeadGeometryVisibleTo(povCamera) && pawn.HeadGeometryVisibleTo(broadcastCamera),
            "Player/Uniform Studio refresh did not preserve camera-specific head visibility.");
        Require(pawn.BodyGeometryVisibleTo(povCamera),
            "Player/Uniform Studio refresh hid the first-person body.");

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
            await WaitSeconds(0.24);
            Require(controller.PovTrackingError < 0.22f, $"POV stabilization lost the eye anchor during {state}.");
            Require(pawn.BodyGeometryVisibleTo(povCamera), $"Body became invisible during {state}.");
            ValidateTransform(povCamera.GlobalTransform, $"POV camera during {state}");
        }

        pawn.SetAnimationState(HumanoidAnimationState.Idle, true);
        await WaitSeconds(0.3);
        var stableCameraRotation = povCamera.Quaternion;
        var stableEyeAnchor = pawn.EyeAnchor.Transform;
        pawn.SetManualGaze(HumanoidEyeRig.MaximumHorizontalGazeDegrees, HumanoidEyeRig.MaximumVerticalGazeDegrees);
        pawn.SetFacialExpression(FacialExpressionState.Surprised, 0);
        pawn.TriggerBlink(0.12f);
        await WaitSeconds(0.08);
        Require(stableCameraRotation.AngleTo(povCamera.Quaternion) < 0.001f && pawn.EyeAnchor.Transform.IsEqualApprox(stableEyeAnchor),
            "Gaze, expression, or blinking disturbed the POV camera orientation.");

        football.Reparent(pawn.CatchAnchor, false);
        football.Position = Vector3.Zero;
        pawn.SetAnimationState(HumanoidAnimationState.Catch, true);
        await WaitSeconds(0.24);
        Require(football.GlobalPosition.DistanceTo(pawn.CatchAnchor.GlobalPosition) < 0.001f,
            "Caught football did not remain on the hand anchor in Player POV.");
        Require(football.GlobalPosition.DistanceTo(povCamera.GlobalPosition) is > 0.15f and < 2.5f,
            "Caught football/hand anchor was implausibly positioned from Player POV.");
        ValidateVector(pawn.CatchAnchor.GlobalPosition, "Player POV hand anchor");

        var broadcast = new CameraDefinition(Guid.NewGuid(), "Validation Broadcast", CameraType.BroadcastWide);
        controller.Preview(broadcast, play);
        Require(!controller.IsPlayerPovActive && !pawn.FirstPersonViewActive,
            "Leaving Player POV did not restore the complete player presentation.");
        Require(pawn.HeadGeometryVisibleTo(povCamera), "The head remained hidden after returning to broadcast view.");

        controller.QueueFree();
        povCamera.QueueFree();
        broadcastCamera.QueueFree();
        cameraHome.QueueFree();
    }

    private PlayerPawn Pawn(Guid playerId) => (PlayerPawn)_pawns[playerId];

    private static Vector3 ToWorld(PlayPoint point) => new(point.X, 0.08f, point.Y);

    private async Task NextFrame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    private async Task WaitSeconds(double seconds) =>
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private static void ValidateTransform(Transform3D transform, string context)
    {
        ValidateVector(transform.Origin, context);
        ValidateVector(transform.Basis.X, context);
        ValidateVector(transform.Basis.Y, context);
        ValidateVector(transform.Basis.Z, context);
    }

    private static void ValidateVector(Vector3 vector, string context) =>
        Require(float.IsFinite(vector.X) && float.IsFinite(vector.Y) && float.IsFinite(vector.Z), $"{context} is not finite.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
