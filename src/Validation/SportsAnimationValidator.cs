using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class SportsAnimationValidator : Node3D
{
    public async Task RunAsync()
    {
        var game = Game.CreatePrototype();
        var project = GameProject.CreatePrototype(game);
        var simulator = new FootballPlaySimulator();
        var quality = new FootballAnimationQualityLayer();
        var receiver = game.Gold.Roster.Single(player => player.Position == PlayerPosition.Receiver);
        var quarterback = game.Gold.Roster.Single(player => player.Position == PlayerPosition.Quarterback);

        var routePlay = PlayDefinition.CreatePrototype(game, "Animation cut validation");
        routePlay.SetRoute(receiver.Id,
            [new PlayPoint(-6, 3), new PlayPoint(-10, 3), new PlayPoint(-10, 7)]);
        routePlay.SetSimulationSettings(PlaySimulationSettings.Default with
        {
            ThrowTime = 1.6,
            PassArc = PassArcPreset.Medium,
            IntendedOutcome = PlayOutcomeKind.FlagPullAfterCatch
        });
        var simulation = simulator.Simulate(routePlay, game.Gold, game.Navy);
        var before = Snapshot(simulation);
        var timeline = quality.Build(simulation);
        Require(before == Snapshot(simulation), "Animation-quality processing mutated simulation frames or outcome.");

        ValidateEventAlignment(simulation, timeline, receiver.Id, quarterback.Id);
        ValidateLocomotion(simulation, timeline, receiver.Id);
        Require(timeline.Frames.Any(frame => frame.Players[receiver.Id].State == HumanoidAnimationState.RouteCut),
            "A sharp authored waypoint did not trigger a planted route-cut pose.");
        Require(timeline.Frames.Any(frame => frame.Players.Values.Any(cue => cue.State == HumanoidAnimationState.Deceleration)),
            "Route completion did not trigger a deceleration pose.");
        Require(timeline.Frames.Any(frame => frame.Players.Values.Any(cue => cue.State == HumanoidAnimationState.Acceleration)),
            "Route release did not trigger an acceleration pose.");
        Require(timeline.Frames.Any(frame => frame.Players.Values.Any(cue => cue.State == HumanoidAnimationState.PostCatchRun)),
            "Completed catch did not transition to protected post-catch running.");

        var curvedPlay = PlayDefinition.CreatePrototype(game, "Animation curved turn validation");
        curvedPlay.SetRoute(receiver.Id,
            [new PlayPoint(-6, 2), new PlayPoint(-5, 6), new PlayPoint(-3, 10)]);
        curvedPlay.SetSimulationSettings(PlaySimulationSettings.Default with { ThrowTime = 2.2 });
        var curvedTimeline = quality.Build(simulator.Simulate(curvedPlay, game.Gold, game.Navy));
        Require(curvedTimeline.Frames.Any(frame => frame.Players[receiver.Id].State == HumanoidAnimationState.CurvedTurn),
            "A shallow multi-segment route did not trigger a curved-turn pose.");

        ValidateOutcomePose(game, simulator, quality, PlayOutcomeKind.DroppedPass,
            SimulationEventType.PassDropped, HumanoidAnimationState.DroppedCatch);
        ValidateOutcomePose(game, simulator, quality, PlayOutcomeKind.Interception,
            SimulationEventType.Intercepted, HumanoidAnimationState.InterceptionCatch);
        ValidateOutcomePose(game, simulator, quality, PlayOutcomeKind.Touchdown,
            SimulationEventType.Touchdown, HumanoidAnimationState.TouchdownCelebration);

        await ValidateRigAndPovAsync(game, project, routePlay, receiver, timeline);
    }

    private static void ValidateEventAlignment(
        PlaySimulation simulation,
        FootballAnimationTimeline timeline,
        Guid receiverId,
        Guid quarterbackId)
    {
        var snap = Event(simulation, SimulationEventType.SnapStarted);
        var received = Event(simulation, SimulationEventType.SnapReceived);
        var set = Event(simulation, SimulationEventType.QuarterbackSet);
        var release = Event(simulation, SimulationEventType.ThrowReleased);
        var window = Event(simulation, SimulationEventType.CatchWindowOpened);
        var catchEvent = Event(simulation, SimulationEventType.PassCompleted);
        var pull = Event(simulation, SimulationEventType.FlagPullAttempted);

        Require(timeline.FrameAt(snap.TimeSeconds - 0.1).Players[quarterbackId].State == HumanoidAnimationState.PreSnapReady,
            "Pre-snap readiness did not last until the snap.");
        Require(timeline.FrameAt(received.TimeSeconds).Players[quarterbackId].State == HumanoidAnimationState.Catch,
            "Quarterback did not receive the snap with a catch pose.");
        Require(timeline.FrameAt((received.TimeSeconds + set.TimeSeconds) * 0.5).Players[quarterbackId].State ==
                HumanoidAnimationState.QuarterbackDropback,
            "Quarterback dropback pose did not align with the simulated drop.");
        Require(timeline.FrameAt(set.TimeSeconds + 0.02).Players[quarterbackId].State == HumanoidAnimationState.QuarterbackSet,
            "Quarterback set pose did not align with the simulated set event.");
        Require(timeline.FrameAt(release.TimeSeconds).Players[quarterbackId].State == HumanoidAnimationState.Throw,
            "Throw pose did not align with ball release.");
        Require(timeline.FrameAt(window.TimeSeconds).Players[receiverId].State == HumanoidAnimationState.CatchPrepare,
            "Receiver hands were not prepared when the catch window opened.");
        Require(timeline.FrameAt(catchEvent.TimeSeconds).Players[receiverId].State == HumanoidAnimationState.Catch,
            "Catch pose did not align with pass completion.");
        Require(pull.PlayerId.HasValue && timeline.FrameAt(pull.TimeSeconds).Players[pull.PlayerId.Value].State ==
                HumanoidAnimationState.FlagPull,
            "Flag-pull pose did not align with the interaction event.");
        Require(pull.PlayerId.HasValue && pull.RelatedPlayerId.HasValue &&
                simulation.FrameAt(pull.TimeSeconds).Players[pull.PlayerId.Value].Position.DistanceTo(
                    simulation.FrameAt(pull.TimeSeconds).Players[pull.RelatedPlayerId.Value].Position) < 1.35f,
            "Flag-pull animation began away from the simulated interaction point.");
    }

    private static void ValidateLocomotion(
        PlaySimulation simulation,
        FootballAnimationTimeline timeline,
        Guid receiverId)
    {
        var moving = timeline.Frames.Select(frame => frame.Players[receiverId])
            .Where(cue => cue.MovementSpeed > 0.5f && cue.StrideFrequency > 0)
            .OrderBy(cue => cue.MovementSpeed)
            .ToArray();
        Require(moving.Length > 4, "Animation timeline did not contain enough receiver locomotion samples.");
        Require(moving[^1].StrideFrequency > moving[0].StrideFrequency + 0.25f,
            "Stride frequency did not increase with simulated movement speed.");
        Require(moving.All(cue => cue.NormalizedSpeed is >= 0 and <= 1.15f),
            "Locomotion speed normalization left its safe range.");
        var accelerating = timeline.Frames.Select(frame => frame.Players[receiverId])
            .First(cue => cue.State == HumanoidAnimationState.Acceleration);
        Require(accelerating.Acceleration > 0 && accelerating.BodyLean > 0.12f,
            "Acceleration did not produce a forward athletic lean.");
        var decelerating = timeline.Frames.SelectMany(frame => frame.Players.Values)
            .First(cue => cue.State == HumanoidAnimationState.Deceleration);
        Require(decelerating.Acceleration < 0 && decelerating.BodyLean < 0,
            "Deceleration did not produce a braking lean.");

    }

    private static void ValidateOutcomePose(
        Game game,
        FootballPlaySimulator simulator,
        FootballAnimationQualityLayer quality,
        PlayOutcomeKind outcome,
        SimulationEventType eventType,
        HumanoidAnimationState expectedState)
    {
        var play = PlayDefinition.CreatePrototype(game, $"Animation {outcome}");
        play.SetSimulationSettings(PlaySimulationSettings.Default with { IntendedOutcome = outcome });
        var simulation = simulator.Simulate(play, game.Gold, game.Navy);
        var timeline = quality.Build(simulation);
        var simulationEvent = Event(simulation, eventType);
        Require(simulationEvent.PlayerId.HasValue &&
                timeline.FrameAt(simulationEvent.TimeSeconds).Players[simulationEvent.PlayerId.Value].State == expectedState,
            $"{outcome} did not trigger {expectedState} at {eventType}.");
        Require(simulation.Outcome.Kind == outcome,
            $"Animation-quality processing altered the authored {outcome} outcome.");
    }

    private async Task ValidateRigAndPovAsync(
        Game game,
        GameProject project,
        PlayDefinition play,
        Player receiver,
        FootballAnimationTimeline timeline)
    {
        var pawn = new PlayerPawn { Name = "SportsAnimationValidationPawn" };
        pawn.Configure(receiver, project.AppearanceFor(receiver.Id), project.ActiveUniformFor(game.Gold.Id));
        AddChild(pawn);
        pawn.Position = new Vector3(play.StartingPositions[receiver.Id].X, 0.08f, play.StartingPositions[receiver.Id].Y);
        await NextFrame();

        var cameraHome = new Node3D { Name = "SportsAnimationCameraHome" };
        AddChild(cameraHome);
        var camera = new Camera3D { Name = "SportsAnimationPovCamera" };
        cameraHome.AddChild(camera);
        var pawns = new Dictionary<Guid, Node3D> { [receiver.Id] = pawn };
        var cameraController = new CameraDirectorController { Name = "SportsAnimationCameraController" };
        AddChild(cameraController);
        cameraController.Configure(camera, cameraHome, pawns);
        var cameraDefinition = new CameraDefinition(Guid.NewGuid(), "Sports Animation POV", CameraType.PlayerPov);
        cameraDefinition.SetPlayer(receiver.Id);
        cameraController.Preview(cameraDefinition, play);
        await NextFrame();

        var representativeStates = new[]
        {
            HumanoidAnimationState.PreSnapReady,
            HumanoidAnimationState.Acceleration,
            HumanoidAnimationState.Sprint,
            HumanoidAnimationState.Deceleration,
            HumanoidAnimationState.RouteCut,
            HumanoidAnimationState.QuarterbackDropback,
            HumanoidAnimationState.Throw,
            HumanoidAnimationState.CatchPrepare,
            HumanoidAnimationState.Catch,
            HumanoidAnimationState.DroppedCatch,
            HumanoidAnimationState.InterceptionCatch,
            HumanoidAnimationState.FlagPull,
            HumanoidAnimationState.PostCatchRun,
            HumanoidAnimationState.TouchdownCelebration
        };
        foreach (var state in representativeStates)
        {
            var sourceCue = timeline.Frames.SelectMany(frame => frame.Players.Values)
                .FirstOrDefault(cue => cue.State == state);
            var cue = sourceCue.State == state ? sourceCue : HumanoidAnimationCue.ForState(state);
            pawn.ApplyAnimationCue(cue, true);
            await WaitSeconds(0.06);
            Require(pawn.AnimationBlendProgress < 1, $"{state} skipped the smooth pose transition.");
            await WaitSeconds(0.19);
            Require(cameraController.PovTrackingError < 0.25f, $"POV stabilization lost the mount during {state}.");
            Require(pawn.BodyGeometryVisibleTo(camera), $"POV body disappeared during {state}.");
            ValidateVector(camera.GlobalPosition, $"POV camera during {state}");
        }

        var sprintCue = timeline.Frames.Select(frame => frame.Players[receiver.Id])
            .OrderByDescending(cue => cue.MovementSpeed).First();
        pawn.ApplyAnimationCue(sprintCue, true);
        await WaitSeconds(0.3);
        Require(Math.Abs(pawn.AnimationMovementSpeed - sprintCue.MovementSpeed) < 0.5f &&
                Math.Abs(pawn.AnimationStrideFrequency - sprintCue.StrideFrequency) < 0.15f,
            "Humanoid animator did not consume simulation-derived speed/cadence parameters.");

        cameraController.QueueFree();
        camera.QueueFree();
        cameraHome.QueueFree();
        pawn.QueueFree();
    }

    private static string Snapshot(PlaySimulation simulation) => string.Join('|',
        simulation.Frames.SelectMany(frame => frame.Players.OrderBy(item => item.Key)
            .Select(item => $"{frame.TimeSeconds:R}:{item.Key}:{item.Value.Position.X:R},{item.Value.Position.Y:R},{item.Value.Position.Z:R}"))) +
        $"|{simulation.Outcome}";

    private static SimulationEvent Event(PlaySimulation simulation, SimulationEventType type) =>
        simulation.Events.First(item => item.Type == type);

    private async Task NextFrame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private async Task WaitSeconds(double seconds) =>
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private static void ValidateVector(Vector3 value, string context) =>
        Require(float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z),
            $"{context} is not finite.");

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
