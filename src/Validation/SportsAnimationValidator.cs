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

        ValidateFootballAttachmentTiming(game, simulator, quality, simulation, timeline, receiver.Id, quarterback.Id);
        await ValidateRigAndPovAsync(game, project, routePlay, receiver, timeline, simulation);
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
        FootballAnimationTimeline timeline,
        PlaySimulation simulation)
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

        await ValidateContactAndHandsAsync(pawn, timeline);

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

    private async Task ValidateContactAndHandsAsync(PlayerPawn pawn, FootballAnimationTimeline timeline)
    {
        pawn.ApplyAnimationCue(HumanoidAnimationCue.ForState(HumanoidAnimationState.Idle), true);
        await WaitSeconds(0.3);
        Require(pawn.LeftFootContactError < 0.08f && pawn.RightFootContactError < 0.08f,
            "Idle soles did not settle on the turf.");

        pawn.ApplyAnimationCue(HumanoidAnimationCue.ForState(HumanoidAnimationState.PreSnapReady), true);
        await WaitSeconds(0.35);
        Require(pawn.LeftFootIkWeight > 0.8f && pawn.RightFootIkWeight > 0.8f,
            "Grounded pre-snap stance did not blend both feet into contact.");
        Require(pawn.LeftFootContactError < 0.08f && pawn.RightFootContactError < 0.08f,
            $"Idle/pre-snap soles did not settle on the turf (left={pawn.LeftFootContactError:0.000}@{pawn.LeftSoleAnchor.GlobalPosition.Y:0.000}, right={pawn.RightFootContactError:0.000}@{pawn.RightSoleAnchor.GlobalPosition.Y:0.000}, root={pawn.ContactRootHeightOffset:0.000}).");

        await ValidatePlantedLocomotionAsync(pawn, HumanoidAnimationState.Jog, 0.08f, true);
        await ValidatePlantedLocomotionAsync(pawn, HumanoidAnimationState.Sprint, 0.58f, false);

        var cutCue = timeline.Frames.Select(frame => frame.Players.Values)
            .SelectMany(cues => cues)
            .First(cue => cue.State == HumanoidAnimationState.RouteCut);
        pawn.ApplyAnimationCue(cutCue, true);
        await WaitSeconds(0.24);
        var expectedLocked = cutCue.LeftFootPlantWeight > cutCue.RightFootPlantWeight
            ? pawn.LeftFootLocked : pawn.RightFootLocked;
        Require(expectedLocked && Math.Max(pawn.LeftFootIkWeight, pawn.RightFootIkWeight) > 0.75f,
            "Sharp route cut did not establish a dominant planted foot.");

        pawn.ApplyAnimationCue(HumanoidAnimationCue.ForState(HumanoidAnimationState.QuarterbackSet), true);
        pawn.SetFootballInteractionMode(FootballInteractionMode.QuarterbackHold);
        await WaitSeconds(0.3);
        ValidateTwoHandAnchor(pawn.QuarterbackHoldAnchor, pawn, "quarterback hold");

        pawn.ApplyAnimationCue(HumanoidAnimationCue.ForState(HumanoidAnimationState.CatchPrepare), true);
        pawn.SetFootballInteractionMode(FootballInteractionMode.CatchHands);
        await WaitSeconds(0.3);
        ValidateTwoHandAnchor(pawn.CatchAnchor, pawn, "catch preparation");

        pawn.ApplyAnimationCue(HumanoidAnimationCue.ForState(HumanoidAnimationState.InterceptionCatch), true);
        pawn.SetFootballInteractionMode(FootballInteractionMode.CatchHands);
        await WaitSeconds(0.3);
        ValidateTwoHandAnchor(pawn.CatchAnchor, pawn, "interception catch");

        pawn.ApplyAnimationCue(HumanoidAnimationCue.ForState(HumanoidAnimationState.DroppedCatch), true);
        pawn.SetFootballInteractionMode(FootballInteractionMode.CatchHands);
        await WaitSeconds(0.16);
        var initialSeparation = pawn.LeftHandAnchor.GlobalPosition.DistanceTo(pawn.RightHandAnchor.GlobalPosition);
        await WaitSeconds(0.34);
        var droppedSeparation = pawn.LeftHandAnchor.GlobalPosition.DistanceTo(pawn.RightHandAnchor.GlobalPosition);
        Require(droppedSeparation > initialSeparation + 0.08f,
            "Dropped-pass pose did not separate the hands after the failed catch.");

        pawn.ApplyAnimationCue(HumanoidAnimationCue.ForState(HumanoidAnimationState.PostCatchRun), true);
        pawn.SetFootballInteractionMode(FootballInteractionMode.Carry);
        await WaitSeconds(0.3);
        Require(pawn.CarryAnchor.GlobalPosition.DistanceTo(pawn.RightHandAnchor.GlobalPosition) < 0.3f,
            "Post-catch carry anchor separated from the carrying hand.");
        Require(pawn.CarryAnchor.GlobalPosition.DistanceTo(pawn.EyeAnchor.GlobalPosition) > 0.35f,
            "Post-catch ball carry anchor is too close to the Player POV camera mount.");
    }

    private async Task ValidatePlantedLocomotionAsync(
        PlayerPawn pawn, HumanoidAnimationState state, float gaitPhase, bool leftPlant)
    {
        var cue = HumanoidAnimationCue.ForState(state) with
        {
            GaitPhase = gaitPhase,
            LeftFootPlantWeight = leftPlant ? 1 : 0,
            RightFootPlantWeight = leftPlant ? 0 : 1
        };
        pawn.ApplyAnimationCue(cue, true);
        await WaitSeconds(0.2);
        var sole = leftPlant ? pawn.LeftSoleAnchor : pawn.RightSoleAnchor;
        var soleStart = sole.GlobalPosition;
        var rootStart = pawn.GlobalPosition;
        for (var index = 0; index < 3; index++)
        {
            pawn.GlobalPosition += new Vector3(0, 0, 0.1f);
            pawn.ApplyAnimationCue(cue);
            await NextFrame();
        }
        var rootTravel = PlanarDistance(rootStart, pawn.GlobalPosition);
        var soleTravel = PlanarDistance(soleStart, sole.GlobalPosition);
        var locked = leftPlant ? pawn.LeftFootLocked : pawn.RightFootLocked;
        Require(locked && soleTravel < rootTravel * 0.75f,
            $"Planted {state} foot slid with the root instead of retaining turf contact " +
            $"(locked={locked}, sole={soleTravel:0.000}, root={rootTravel:0.000}, " +
            $"weight={(leftPlant ? pawn.LeftFootIkWeight : pawn.RightFootIkWeight):0.00}).");
    }

    private static void ValidateFootballAttachmentTiming(
        Game game,
        FootballPlaySimulator simulator,
        FootballAnimationQualityLayer quality,
        PlaySimulation simulation,
        FootballAnimationTimeline timeline,
        Guid receiverId,
        Guid quarterbackId)
    {
        var release = Event(simulation, SimulationEventType.ThrowReleased).TimeSeconds;
        var heldFrame = simulation.FrameAt(release - 0.05);
        var heldCue = timeline.FrameAt(release - 0.05).Players[quarterbackId];
        Require(heldFrame.Ball.Phase == BallPhase.HeldByQuarterback &&
                FootballInteractionResolver.Resolve(heldFrame.Ball, heldCue.State) == FootballInteractionMode.ThrowingHand,
            "Football did not transition from two-hand QB hold to the throwing hand before release.");
        var flightFrame = simulation.FrameAt(release + 0.05);
        Require(flightFrame.Ball.Phase == BallPhase.PassFlight &&
                FootballInteractionResolver.Resolve(flightFrame.Ball,
                    timeline.FrameAt(release + 0.05).Players[quarterbackId].State) == FootballInteractionMode.None,
            "Football remained hand-attached after the authored throw release timestamp.");

        var catchTime = Event(simulation, SimulationEventType.PassCompleted).TimeSeconds;
        var catchFrame = simulation.FrameAt(catchTime);
        Require(FootballInteractionResolver.Resolve(catchFrame.Ball,
                    timeline.FrameAt(catchTime).Players[receiverId].State) == FootballInteractionMode.CatchHands,
            "Completed pass did not attach between the catch hands.");
        var carryTime = Math.Min(simulation.DurationSeconds - 0.05, catchTime + 0.55);
        Require(FootballInteractionResolver.Resolve(simulation.FrameAt(carryTime).Ball,
                    timeline.FrameAt(carryTime).Players[receiverId].State) == FootballInteractionMode.Carry,
            "Caught football did not transition to the post-catch carry anchor.");

        var interceptionPlay = PlayDefinition.CreatePrototype(game, "Attachment interception");
        interceptionPlay.SetSimulationSettings(PlaySimulationSettings.Default with
        {
            IntendedOutcome = PlayOutcomeKind.Interception
        });
        var interception = simulator.Simulate(interceptionPlay, game.Gold, game.Navy);
        var interceptionTimeline = quality.Build(interception);
        var interceptionEvent = Event(interception, SimulationEventType.Intercepted);
        Require(interceptionEvent.PlayerId.HasValue &&
                FootballInteractionResolver.Resolve(interception.FrameAt(interceptionEvent.TimeSeconds).Ball,
                    interceptionTimeline.FrameAt(interceptionEvent.TimeSeconds)
                        .Players[interceptionEvent.PlayerId.Value].State) == FootballInteractionMode.CatchHands,
            "Interception did not use the two-hand catch attachment.");
    }

    private static void ValidateTwoHandAnchor(Node3D anchor, PlayerPawn pawn, string context)
    {
        var midpoint = pawn.LeftHandAnchor.GlobalPosition.Lerp(pawn.RightHandAnchor.GlobalPosition, 0.5f);
        Require(anchor.GlobalPosition.DistanceTo(midpoint) < 0.025f,
            $"{context} football anchor was not centered between both hands.");
        Require(anchor.GlobalPosition.DistanceTo(pawn.LeftHandAnchor.GlobalPosition) < 0.32f &&
                anchor.GlobalPosition.DistanceTo(pawn.RightHandAnchor.GlobalPosition) < 0.32f,
            $"{context} hands did not converge around the football " +
            $"(left={anchor.GlobalPosition.DistanceTo(pawn.LeftHandAnchor.GlobalPosition):0.000}, " +
            $"right={anchor.GlobalPosition.DistanceTo(pawn.RightHandAnchor.GlobalPosition):0.000}).");
    }

    private static float PlanarDistance(Vector3 left, Vector3 right) =>
        new Vector2(left.X - right.X, left.Z - right.Z).Length();

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
