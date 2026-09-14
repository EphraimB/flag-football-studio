using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class FootballSimulationValidator : Node
{
    public void RunDiagnostics()
    {
        var game = Game.CreatePrototype();
        var prototype = PlayDefinition.CreatePrototype(game);
        var receiver = prototype.IntendedReceiverId;
        var slant = prototype.Duplicate("Diagnostic short slant");
        slant.SetRoute(receiver, [new PlayPoint(-4.5f, 1.5f), new PlayPoint(-3.2f, 4.4f)]);
        slant.SetSimulationSettings(PlaySimulationSettings.Default with
        {
            ThrowTime = 0.55,
            PassArc = PassArcPreset.Short,
            IntendedOutcome = PlayOutcomeKind.Completion
        });
        var go = prototype.Duplicate("Diagnostic deep go");
        go.SetRoute(receiver, [new PlayPoint(-6, 7), new PlayPoint(-6, 19)]);
        go.SetSimulationSettings(PlaySimulationSettings.Default with
        {
            ThrowTrigger = ThrowTriggerMode.RouteMilestone,
            ThrowRouteProgress = 0.72f,
            PassArc = PassArcPreset.Deep,
            IntendedOutcome = PlayOutcomeKind.Touchdown
        });

        var simulator = new FootballPlaySimulator();
        var slantSimulation = simulator.Simulate(slant, game.Gold, game.Navy);
        var goSimulation = simulator.Simulate(go, game.Gold, game.Navy);
        GD.Print(BuildTrace(slant, slantSimulation, game));
        GD.Print(BuildTrace(go, goSimulation, game));
        var slantFinal = slantSimulation.Frames[^1].Players[receiver].Position;
        var goFinal = goSimulation.Frames[^1].Players[receiver].Position;
        GD.Print($"SIMULATION COMPARISON | throw delta={Math.Abs(Event(slantSimulation, SimulationEventType.ThrowReleased).TimeSeconds - Event(goSimulation, SimulationEventType.ThrowReleased).TimeSeconds):0.00}s, receiver-final delta={slantFinal.DistanceTo(goFinal):0.00}m, duration delta={Math.Abs(slantSimulation.DurationSeconds - goSimulation.DurationSeconds):0.00}s");
    }

    public void Run()
    {
        var game = Game.CreatePrototype();
        var project = GameProject.CreatePrototype(game);
        var play = project.Plays[0];
        var simulator = new FootballPlaySimulator();
        var simulation = simulator.Simulate(play, game.Gold, game.Navy);
        var settings = play.SimulationSettings;

        var snap = Event(simulation, SimulationEventType.SnapStarted);
        var received = Event(simulation, SimulationEventType.SnapReceived);
        Require(Near(snap.TimeSeconds, settings.PreSnapDelay), "Snap did not honor the authored pre-snap delay.");
        Require(received.TimeSeconds > snap.TimeSeconds, "Center-to-quarterback snap timing is invalid.");

        var receiverAssignment = simulation.Assignments.OfType<OffensiveAssignment>()
            .Single(assignment => assignment.PlayerId == play.IntendedReceiverId);
        var receiverStart = simulation.FrameAt(receiverAssignment.ReleaseTime - FootballPlaySimulator.FixedStepSeconds)
            .Players[play.IntendedReceiverId].Position;
        var receiverReleased = simulation.FrameAt(receiverAssignment.ReleaseTime + 0.35)
            .Players[play.IntendedReceiverId].Position;
        Require(receiverStart.DistanceTo(receiverReleased) > 0.05f, "Receiver did not progress after route release.");

        var defenderAssignment = simulation.Assignments.OfType<DefensiveAssignment>()
            .First(assignment => assignment.CoverageType == DefensiveCoverageType.Man);
        var defenderStart = simulation.FrameAt(defenderAssignment.ReactionTime - FootballPlaySimulator.FixedStepSeconds)
            .Players[defenderAssignment.PlayerId].Position;
        var defenderReacted = simulation.FrameAt(defenderAssignment.ReactionTime + 0.35)
            .Players[defenderAssignment.PlayerId].Position;
        Require(defenderStart.DistanceTo(defenderReacted) > 0.02f, "Man defender ignored the reaction delay or failed to react.");

        var release = Event(simulation, SimulationEventType.ThrowReleased).TimeSeconds;
        var arrival = Event(simulation, SimulationEventType.PassCompleted).TimeSeconds;
        var ballStart = simulation.FrameAt(release).Ball.Position;
        var ballMiddle = simulation.FrameAt((release + arrival) * 0.5).Ball.Position;
        Require(ballMiddle.Y > ballStart.Y + 0.5f, "Pass trajectory did not produce an arc.");
        var arcHeights = Enum.GetValues<PassArcPreset>().Select(arc =>
        {
            play.SetSimulationSettings(settings with { PassArc = arc });
            var pass = simulator.Simulate(play, game.Gold, game.Navy);
            var passRelease = Event(pass, SimulationEventType.ThrowReleased).TimeSeconds;
            var passArrival = pass.Events.First(item => item.Type is SimulationEventType.PassCompleted
                or SimulationEventType.PassIncomplete or SimulationEventType.PassDropped or SimulationEventType.Intercepted).TimeSeconds;
            return pass.FrameAt((passRelease + passArrival) * 0.5).Ball.Position.Y;
        }).ToArray();
        Require(arcHeights[0] < arcHeights[1] && arcHeights[1] < arcHeights[2],
            "Short, medium, and deep pass presets did not produce increasing arc height.");
        play.SetSimulationSettings(settings with { ThrowTrigger = ThrowTriggerMode.RouteMilestone, ThrowRouteProgress = 0.55f });
        var milestonePass = simulator.Simulate(play, game.Gold, game.Navy);
        var milestoneRelease = Event(milestonePass, SimulationEventType.ThrowReleased).TimeSeconds;
        var milestoneAssignment = milestonePass.Assignments.OfType<OffensiveAssignment>()
            .Single(assignment => assignment.PlayerId == play.IntendedReceiverId);
        var milestoneProgress = milestonePass.FrameAt(milestoneRelease).Players[play.IntendedReceiverId].RouteProgress;
        Require(milestoneProgress is not null && milestoneProgress.DistanceTravelled > 0,
            "Route-milestone trigger released before the receiver progressed.");
        play.SetSimulationSettings(settings);
        foreach (var motion in new[]
                 {
                     SimulationMotionState.Turn, SimulationMotionState.Jog, SimulationMotionState.Sprint,
                     SimulationMotionState.Throw, SimulationMotionState.Catch, SimulationMotionState.FlagPull
                 })
            Require(simulation.Frames.Any(frame => frame.Players.Values.Any(player => player.MotionState == motion)),
                $"Simulation never emitted the {motion} animation state.");

        foreach (var kind in Enum.GetValues<PlayOutcomeKind>())
        {
            play.SetSimulationSettings(settings with { IntendedOutcome = kind });
            var authored = simulator.Simulate(play, game.Gold, game.Navy);
            Require(authored.Outcome.Kind == kind, $"Authored outcome {kind} was not deterministic.");
            Require(authored.Events.Any(item => item.Type == SimulationEventType.PlayEnded), $"{kind} did not end the play.");
            var expectedEvent = kind switch
            {
                PlayOutcomeKind.Incompletion => SimulationEventType.PassIncomplete,
                PlayOutcomeKind.Interception => SimulationEventType.Intercepted,
                PlayOutcomeKind.DroppedPass => SimulationEventType.PassDropped,
                PlayOutcomeKind.QuarterbackFlagPull => SimulationEventType.FlagPulled,
                _ => SimulationEventType.PassCompleted
            };
            Require(authored.Events.Any(item => item.Type == expectedEvent), $"{kind} emitted the wrong ball-result event.");
            if (kind == PlayOutcomeKind.FlagPullAfterCatch)
                Require(authored.Events.Any(item => item.Type == SimulationEventType.FlagPulled), "Flag-pull outcome omitted the flag-pull event.");
            if (kind == PlayOutcomeKind.Touchdown)
                Require(authored.Events.Any(item => item.Type == SimulationEventType.Touchdown), "Touchdown omitted its scoring event.");
            if (kind == PlayOutcomeKind.OutOfBounds)
                Require(authored.Events.Any(item => item.Type == SimulationEventType.OutOfBounds), "Out-of-bounds outcome omitted its boundary event.");
            if (kind == PlayOutcomeKind.QuarterbackFlagPull)
            {
                Require(!authored.Events.Any(item => item.Type == SimulationEventType.ThrowReleased),
                    "Quarterback flag-pull ending incorrectly released a pass.");
                Require(authored.Frames[^1].Ball.Phase == BallPhase.HeldByQuarterback,
                    "Quarterback flag-pull ending lost the held football.");
                Require(authored.Outcome.YardsGained < 0, "Quarterback flag pull did not record lost yardage.");
            }
        }

        ValidateDivergentFixtures(game, simulator);

        play.SetSimulationSettings(settings);
        var first = simulator.Simulate(play, game.Gold, game.Navy);
        var second = simulator.Simulate(play, game.Gold, game.Navy);
        Require(first.Frames.Count == second.Frames.Count && first.Events.SequenceEqual(second.Events),
            "Identical play data produced a different timeline.");
        for (var index = 0; index < first.Frames.Count; index += 11)
            Require(first.Frames[index].Ball == second.Frames[index].Ball,
                "Identical play data produced different ball flight.");

        var serializer = new Persistence.ProjectJsonSerializer();
        var loadedPlay = serializer.DeserializePlay(serializer.SerializePlay(play));
        Require(loadedPlay.SimulationSettings == play.SimulationSettings, "Simulation settings did not round trip.");

        var downProject = GameProject.CreatePrototype(game);
        downProject.ApplyPlayOutcome(new PlayOutcome(PlayOutcomeKind.Incompletion, 0, false, false, 2, null, "Incomplete pass"));
        Require(downProject.Down == 2 && downProject.Distance == 10, "Incompletion did not update down and distance.");
        downProject.ApplyPlayOutcome(new PlayOutcome(PlayOutcomeKind.Completion, 12, false, false, 2, play.IntendedReceiverId, "First down"));
        Require(downProject.Down == 1 && downProject.Distance == 10, "First-down completion did not reset the chains.");

        var interceptionProject = GameProject.CreatePrototype(game);
        var originalPossession = interceptionProject.Possession.Id;
        interceptionProject.ApplyPlayOutcome(new PlayOutcome(PlayOutcomeKind.Interception, 0, true, false, 3,
            game.Navy.Roster[0].Id, "Intercepted"));
        Require(interceptionProject.Possession.Id != originalPossession, "Interception did not change possession.");

        var touchdownProject = GameProject.CreatePrototype(game);
        touchdownProject.ApplyPlayOutcome(new PlayOutcome(PlayOutcomeKind.Touchdown, 20, true, true, 4,
            play.IntendedReceiverId, "Touchdown"));
        Require(touchdownProject.HomeScore == 6 && touchdownProject.Possession.Id == touchdownProject.AwayTeam.Id,
            "Touchdown did not update score and possession.");

        var sackProject = GameProject.CreatePrototype(game);
        sackProject.ApplyPlayOutcome(new PlayOutcome(PlayOutcomeKind.QuarterbackFlagPull, -3, false, false, 2.5,
            play.QuarterbackId, "Quarterback flag pulled"));
        Require(sackProject.Down == 2 && sackProject.Distance == 13,
            "Quarterback flag-pull loss did not update down and distance.");

        var camera = project.Cameras[0];
        project.AddCameraCut(new CameraCut(Guid.NewGuid(), play.Id, camera.Id, 0.5));
        var dialogue = new DialogueSequence(Guid.NewGuid(), "Simulation sync", DialogueSequenceContext.Play, play.Id);
        dialogue.AddLine(new DialogueLine(Guid.NewGuid(), game.Gold.Roster[0].Id, 0.25, 0.5, "Set, go"));
        project.AddDialogueSequence(dialogue);
        Require(project.CameraCutsFor(play.Id).All(cut => cut.TimeSeconds <= simulation.DurationSeconds),
            "A camera cut fell outside the play timeline.");
        Require(project.DialogueForPlay(play.Id).SelectMany(sequence => sequence.Lines)
                .All(line => line.StartTime <= simulation.DurationSeconds),
            "Dialogue did not share the play timeline.");

        GD.Print("Football simulation validation passed.");
    }

    private static void ValidateDivergentFixtures(Game game, FootballPlaySimulator simulator)
    {
        var prototype = PlayDefinition.CreatePrototype(game);
        var quarterback = prototype.QuarterbackId;
        var receiver = game.Gold.Roster.Single(player => player.Position == PlayerPosition.Receiver).Id;
        var slot = game.Gold.Roster.Single(player => player.Position == PlayerPosition.SlotReceiver).Id;
        var primaryDefender = game.Navy.Roster[0].Id;

        var slant = prototype.Duplicate("Fixture - short slant completion");
        slant.SetStartingPosition(quarterback, new PlayPoint(0, -4.5f));
        slant.SetStartingPosition(receiver, new PlayPoint(-5.5f, -1.5f));
        slant.SetStartingPosition(primaryDefender, new PlayPoint(-5, 1.25f));
        slant.SetRoute(receiver, [new PlayPoint(-4.5f, 1.5f), new PlayPoint(-2.7f, 4.4f)]);
        slant.SetSimulationSettings(PlaySimulationSettings.Default with
        {
            ThrowTime = 0.45,
            PassArc = PassArcPreset.Short,
            IntendedOutcome = PlayOutcomeKind.Completion
        });

        var go = prototype.Duplicate("Fixture - deep go touchdown");
        go.SetStartingPosition(quarterback, new PlayPoint(1, -7));
        go.SetStartingPosition(receiver, new PlayPoint(7, -1));
        go.SetStartingPosition(primaryDefender, new PlayPoint(7, 2));
        go.SetRoute(receiver, [new PlayPoint(7, 7), new PlayPoint(7, 19)]);
        go.SetSimulationSettings(PlaySimulationSettings.Default with
        {
            ThrowTrigger = ThrowTriggerMode.RouteMilestone,
            ThrowRouteProgress = 0.78f,
            PassArc = PassArcPreset.Deep,
            IntendedOutcome = PlayOutcomeKind.Touchdown,
            ThrowSpeed = 18
        });

        var outRoute = prototype.Duplicate("Fixture - out route incompletion");
        outRoute.SetStartingPosition(quarterback, new PlayPoint(-1, -5.5f));
        outRoute.SetStartingPosition(receiver, new PlayPoint(-7, -2));
        outRoute.SetStartingPosition(primaryDefender, new PlayPoint(-7.5f, 0.5f));
        outRoute.SetRoute(receiver, [new PlayPoint(-7.2f, 0), new PlayPoint(-10, 1)]);
        outRoute.SetSimulationSettings(PlaySimulationSettings.Default with
        {
            ThrowTime = 1.15,
            PassArc = PassArcPreset.Short,
            IntendedOutcome = PlayOutcomeKind.Incompletion,
            ThrowSpeed = 11
        });

        var post = prototype.Duplicate("Fixture - post interception");
        post.SetQuarterback(quarterback);
        post.SetIntendedReceiver(slot);
        post.AssignCoverage(primaryDefender, slot);
        post.SetStartingPosition(quarterback, new PlayPoint(0.5f, -6));
        post.SetStartingPosition(slot, new PlayPoint(5, -2));
        post.SetStartingPosition(primaryDefender, new PlayPoint(5.5f, 1.5f));
        post.SetRoute(receiver, []);
        post.SetRoute(slot, [new PlayPoint(5, 5), new PlayPoint(1, 13)]);
        post.SetSimulationSettings(PlaySimulationSettings.Default with
        {
            ThrowTime = 1.45,
            PassArc = PassArcPreset.Medium,
            IntendedOutcome = PlayOutcomeKind.Interception,
            ThrowSpeed = 15
        });

        var rusher = prototype.Duplicate("Fixture - rusher quarterback flag pull");
        rusher.SetStartingPosition(quarterback, new PlayPoint(0, -8));
        rusher.SetStartingPosition(receiver, new PlayPoint(-4, -2));
        rusher.SetRoute(receiver, []);
        rusher.SetSimulationSettings(PlaySimulationSettings.Default with
        {
            ThrowTime = 2.4,
            PassArc = PassArcPreset.Medium,
            IntendedOutcome = PlayOutcomeKind.QuarterbackFlagPull,
            RusherDelay = 0.15
        });

        var fixtures = new[]
        {
            SimulateFixture(slant, receiver, RouteClassification.Slant, simulator, game),
            SimulateFixture(go, receiver, RouteClassification.Go, simulator, game),
            SimulateFixture(outRoute, receiver, RouteClassification.Out, simulator, game),
            SimulateFixture(post, slot, RouteClassification.Post, simulator, game),
            SimulateFixture(rusher, quarterback, RouteClassification.CustomWaypoint, simulator, game)
        };

        foreach (var fixture in fixtures)
        {
            var firstFrame = fixture.Simulation.Frames[0];
            foreach (var authored in fixture.Play.StartingPositions)
            {
                var simulated = firstFrame.Players[authored.Key].Position;
                Require(Math.Abs(simulated.X - authored.Value.X) < 0.001f &&
                        Math.Abs(simulated.Z - authored.Value.Y) < 0.001f,
                    $"{fixture.Play.Name} did not map the authored formation exactly into 3D.");
            }
            Require(fixture.Simulation.Outcome.Kind == fixture.Play.SimulationSettings.IntendedOutcome,
                $"{fixture.Play.Name} did not preserve its authored outcome.");
        }

        var routed = fixtures.Take(4).ToArray();
        Require(routed.Select(item => item.RouteType).Distinct().Count() == routed.Length,
            "The deliberately different authored routes collapsed to the same classification.");
        Require(routed.Select(item => Math.Round(item.ThrowTime, 2)).Distinct().Count() == routed.Length,
            "Short, deep, out, and post fixtures collapsed to the same throw timing.");
        Require(RouteCompletionTime(fixtures[1]) > RouteCompletionTime(fixtures[0]) + 1,
            "Long and short authored routes collapsed to the same route duration.");
        var authoredOutCut = ToSimulation(outRoute.Routes[receiver][0]);
        Require(fixtures[2].Simulation.Frames.Min(frame =>
                    frame.Players[receiver].Position.DistanceTo(authoredOutCut)) < 0.12f,
            "Route movement cut a corner instead of passing through the authored waypoint.");
        for (var left = 0; left < routed.Length; left++)
        for (var right = left + 1; right < routed.Length; right++)
        {
            Require(ReceiverPathDistance(routed[left], routed[right]) > 1.0f,
                $"{routed[left].Play.Name} and {routed[right].Play.Name} produced visually similar route paths.");
            Require(BallPathDistance(routed[left], routed[right]) > 0.55f,
                $"{routed[left].Play.Name} and {routed[right].Play.Name} produced visually similar pass trajectories.");
        }

        for (var left = 0; left < fixtures.Length; left++)
        for (var right = left + 1; right < fixtures.Length; right++)
            Require(FinalFormationDistance(fixtures[left].Simulation, fixtures[right].Simulation) > 0.35f,
                $"{fixtures[left].Play.Name} and {fixtures[right].Play.Name} converged to similar final positions.");

        var slantFixture = fixtures[0];
        var goFixture = fixtures[1];
        var outFixture = fixtures[2];
        var postFixture = fixtures[3];
        var rusherFixture = fixtures[4];
        Require(MaxBallHeight(goFixture.Simulation) > MaxBallHeight(slantFixture.Simulation) + 2,
            "Deep and short pass arcs were not visually distinct.");
        Require(slantFixture.Simulation.Frames[^1].Players[receiver].Position.DistanceTo(slantFixture.CatchPoint) > 2.5f,
            "Completion did not continue visibly after the catch.");
        Require(Math.Abs(goFixture.Simulation.Frames[^1].Players[receiver].Position.Z - 20) < 0.05f,
            "Go-route touchdown did not carry into the attacking end zone.");
        Require(outFixture.Simulation.Frames[^1].Ball.Phase == BallPhase.Incomplete &&
                outFixture.Simulation.Frames[^1].Ball.Position.DistanceTo(
                    outFixture.Simulation.Frames[^1].Players[receiver].Position) > 1.5f,
            "Out-route incompletion did not visibly miss the receiver and reach the ground.");
        var interceptor = postFixture.Simulation.Outcome.BallCarrierId;
        Require(interceptor.HasValue && postFixture.Simulation.Frames[^1].Ball.Phase == BallPhase.Intercepted &&
                postFixture.Simulation.Frames[^1].Ball.PossessingPlayerId == interceptor,
            "Post interception did not transfer the ball to the defender.");
        var rusherAssignment = rusherFixture.Simulation.Assignments.OfType<DefensiveAssignment>()
            .Single(item => item.CoverageType == DefensiveCoverageType.Rusher);
        Require(!rusherFixture.Simulation.Events.Any(item => item.Type == SimulationEventType.ThrowReleased) &&
                rusherFixture.Simulation.Frames[^1].Players[rusherAssignment.PlayerId].Position.DistanceTo(
                    rusherFixture.Simulation.Frames[^1].Players[quarterback].Position) < 0.8f,
            "Rusher ending did not close on the quarterback before the flag pull.");

        var deepQuarterbackSet = goFixture.Simulation.FrameAt(
            Event(goFixture.Simulation, SimulationEventType.QuarterbackSet).TimeSeconds).Players[quarterback].Position;
        var shortQuarterbackSet = slantFixture.Simulation.FrameAt(
            Event(slantFixture.Simulation, SimulationEventType.QuarterbackSet).TimeSeconds).Players[quarterback].Position;
        Require(Math.Abs(deepQuarterbackSet.Z - go.StartingPositions[quarterback].Y) >
                Math.Abs(shortQuarterbackSet.Z - slant.StartingPositions[quarterback].Y) + 1,
            "Deep and short concepts did not produce visibly different quarterback drops.");
        var quarterbackBeforeThrow = postFixture.Simulation.FrameAt(postFixture.ThrowTime - 0.08).Players[quarterback];
        var postTarget = postFixture.Simulation.FrameAt(postFixture.ThrowTime).Players[slot].Position;
        Require(DirectionDot(quarterbackBeforeThrow.FacingDirection,
                    Direction(quarterbackBeforeThrow.Position, postTarget)) > 0.95f,
            "Quarterback did not orient toward the selected receiver before release.");

        var defenseSample = goFixture.Simulation.FrameAt(2.2);
        var man = goFixture.Simulation.Assignments.OfType<DefensiveAssignment>()
            .Single(item => item.CoverageType == DefensiveCoverageType.Man);
        var zone = goFixture.Simulation.Assignments.OfType<DefensiveAssignment>()
            .First(item => item.CoverageType == DefensiveCoverageType.Zone);
        var rush = goFixture.Simulation.Assignments.OfType<DefensiveAssignment>()
            .Single(item => item.CoverageType == DefensiveCoverageType.Rusher);
        Require(defenseSample.Players[man.PlayerId].AssignmentTarget.HasValue &&
                defenseSample.Players[zone.PlayerId].AssignmentTarget.HasValue &&
                defenseSample.Players[rush.PlayerId].AssignmentTarget.HasValue,
            "Defensive timeline omitted assignment targets needed by presentation diagnostics.");
        Require(defenseSample.Players[rush.PlayerId].AssignmentTarget!.Value.DistanceTo(
                    defenseSample.Players[quarterback].Position) < 0.01f,
            "Rusher was not pursuing the live quarterback position.");
        Require(defenseSample.Players[man.PlayerId].AssignmentTarget!.Value.DistanceTo(
                    defenseSample.Players[receiver].Position) < 2.0f,
            "Man defender was not pursuing the assigned receiver with leverage.");
        Require(defenseSample.Players[zone.PlayerId].AssignmentTarget!.Value.DistanceTo(
                    defenseSample.Players[man.PlayerId].AssignmentTarget!.Value) > 1.0f,
            "Zone defender collapsed into the same target as man coverage.");

        var idlePlayer = game.Gold.Roster.Single(player => player.Position == PlayerPosition.RunningBack).Id;
        var idleStart = ToSimulation(slant.StartingPositions[idlePlayer]);
        Require(slantFixture.Simulation.Frames.All(frame => frame.Players[idlePlayer].Position.DistanceTo(idleStart) < 0.001f),
            "An offensive player with no route was assigned a generic default path.");
    }

    private static SimulationFixture SimulateFixture(
        PlayDefinition play, Guid routePlayerId, RouteClassification expectedRoute,
        FootballPlaySimulator simulator, Game game)
    {
        var simulation = simulator.Simulate(play, game.Gold, game.Navy);
        var assignment = simulation.Assignments.OfType<OffensiveAssignment>()
            .Single(item => item.PlayerId == routePlayerId);
        Require(assignment.RouteType == expectedRoute,
            $"{play.Name} classified as {assignment.RouteType} instead of {expectedRoute}.");
        var throwEvent = simulation.Events.FirstOrDefault(item => item.Type == SimulationEventType.ThrowReleased);
        var arrivalEvent = simulation.Events.FirstOrDefault(item => item.Type is SimulationEventType.PassCompleted
            or SimulationEventType.PassIncomplete or SimulationEventType.PassDropped or SimulationEventType.Intercepted);
        var throwTime = throwEvent?.TimeSeconds ?? double.NaN;
        var arrivalTime = arrivalEvent?.TimeSeconds ?? simulation.DurationSeconds;
        var catchPoint = simulation.FrameAt(arrivalTime).Players[routePlayerId].Position;
        return new SimulationFixture(play, routePlayerId, assignment.RouteType, simulation,
            throwTime, arrivalTime, catchPoint);
    }

    private static float ReceiverPathDistance(SimulationFixture left, SimulationFixture right)
    {
        var total = 0f;
        for (var index = 0; index <= 8; index++)
        {
            var leftTime = left.Simulation.DurationSeconds * index / 8d;
            var rightTime = right.Simulation.DurationSeconds * index / 8d;
            total += left.Simulation.FrameAt(leftTime).Players[left.RoutePlayerId].Position.DistanceTo(
                right.Simulation.FrameAt(rightTime).Players[right.RoutePlayerId].Position);
        }
        return total / 9;
    }

    private static float BallPathDistance(SimulationFixture left, SimulationFixture right)
    {
        var total = 0f;
        for (var index = 0; index <= 8; index++)
        {
            var leftTime = left.ThrowTime + (left.ArrivalTime - left.ThrowTime) * index / 8d;
            var rightTime = right.ThrowTime + (right.ArrivalTime - right.ThrowTime) * index / 8d;
            total += left.Simulation.FrameAt(leftTime).Ball.Position.DistanceTo(
                right.Simulation.FrameAt(rightTime).Ball.Position);
        }
        return total / 9;
    }

    private static float FinalFormationDistance(PlaySimulation left, PlaySimulation right)
    {
        var leftFinal = left.Frames[^1];
        var rightFinal = right.Frames[^1];
        return leftFinal.Players.Keys.Average(playerId =>
            leftFinal.Players[playerId].Position.DistanceTo(rightFinal.Players[playerId].Position));
    }

    private static float MaxBallHeight(PlaySimulation simulation) =>
        simulation.Frames.Max(frame => frame.Ball.Position.Y);

    private static double RouteCompletionTime(SimulationFixture fixture)
    {
        return fixture.Simulation.Frames.First(frame =>
            frame.Players[fixture.RoutePlayerId].RouteProgress?.Complete == true).TimeSeconds;
    }

    private static float DirectionDot(SimulationVector3 left, SimulationVector3 right) =>
        left.X * right.X + left.Z * right.Z;

    private static SimulationVector3 Direction(SimulationVector3 from, SimulationVector3 to)
    {
        var x = to.X - from.X;
        var z = to.Z - from.Z;
        var length = MathF.Sqrt(x * x + z * z);
        return length < 0.0001f ? new SimulationVector3(0, 0, 1) : new SimulationVector3(x / length, 0, z / length);
    }

    private static SimulationVector3 ToSimulation(PlayPoint point) => new(point.X, 0.08f, point.Y);

    private sealed record SimulationFixture(
        PlayDefinition Play,
        Guid RoutePlayerId,
        RouteClassification RouteType,
        PlaySimulation Simulation,
        double ThrowTime,
        double ArrivalTime,
        SimulationVector3 CatchPoint);

    private static SimulationEvent Event(PlaySimulation simulation, SimulationEventType type) =>
        simulation.Events.First(item => item.Type == type);

    private static string BuildTrace(PlayDefinition play, PlaySimulation simulation, Game game)
    {
        var playerNames = game.Gold.Roster.Concat(game.Navy.Roster)
            .ToDictionary(player => player.Id, player => player.Name);
        string PlayerLabel(Guid playerId) => $"{playerNames.GetValueOrDefault(playerId, "Unknown")} [{playerId}]";
        var trace = new StringBuilder();
        trace.AppendLine($"SIMULATION TRACE | {play.Name}");
        foreach (var start in play.StartingPositions.OrderBy(item => item.Key))
            trace.AppendLine($"  formation {PlayerLabel(start.Key)}: ({start.Value.X:0.0},{start.Value.Y:0.0})");
        foreach (var assignment in simulation.Assignments.OfType<OffensiveAssignment>().Where(item => item.Waypoints.Count > 0))
        {
            trace.AppendLine($"  route {PlayerLabel(assignment.PlayerId)}: {assignment.RouteType} [{string.Join(" -> ", assignment.Waypoints.Select(point => $"({point.X:0.0},{point.Y:0.0})"))}]");
            foreach (var time in SampleTimes(simulation.DurationSeconds))
            {
                var state = simulation.FrameAt(time).Players[assignment.PlayerId];
                trace.AppendLine($"    t={time:0.0} progress=seg{state.RouteProgress?.SegmentIndex}:{state.RouteProgress?.SegmentProgress:0.00} pos=({state.Position.X:0.0},{state.Position.Z:0.0})");
            }
        }
        foreach (var assignment in simulation.Assignments.OfType<DefensiveAssignment>())
        {
            var covered = assignment.TargetPlayerId.HasValue ? PlayerLabel(assignment.TargetPlayerId.Value) : "none";
            trace.AppendLine($"  defense {PlayerLabel(assignment.PlayerId)}: {assignment.CoverageType}, player={covered}, zone=({assignment.ZoneLandmark.X:0.0},{assignment.ZoneLandmark.Y:0.0}), reacts={assignment.ReactionTime:0.00}s");
            foreach (var time in SampleTimes(simulation.DurationSeconds))
            {
                var frame = simulation.FrameAt(time);
                var target = DefensiveTarget(assignment, frame, play.QuarterbackId);
                var state = frame.Players[assignment.PlayerId];
                trace.AppendLine($"    t={time:0.0} pos=({state.Position.X:0.0},{state.Position.Z:0.0}) target=({target.X:0.0},{target.Z:0.0})");
            }
        }
        var throwEvent = Event(simulation, SimulationEventType.ThrowReleased);
        var arrivalEvent = simulation.Events.First(item => item.Type is SimulationEventType.PassCompleted or SimulationEventType.PassIncomplete or SimulationEventType.PassDropped or SimulationEventType.Intercepted);
        trace.AppendLine($"  QB={PlayerLabel(play.QuarterbackId)}, receiver={PlayerLabel(play.IntendedReceiverId)}, throw={throwEvent.TimeSeconds:0.00}s, pass-target={Format(simulation.FrameAt(arrivalEvent.TimeSeconds).Ball.Position)}");
        for (var step = 0; step <= 4; step++)
        {
            var time = throwEvent.TimeSeconds + (arrivalEvent.TimeSeconds - throwEvent.TimeSeconds) * step / 4d;
            trace.AppendLine($"  ball t={time:0.00} {Format(simulation.FrameAt(time).Ball.Position)} {simulation.FrameAt(time).Ball.Phase}");
        }
        trace.AppendLine($"  outcome={simulation.Outcome.Kind}, yards={simulation.Outcome.YardsGained}, end={simulation.DurationSeconds:0.00}s");
        return trace.ToString();
    }

    private static IEnumerable<double> SampleTimes(double duration)
    {
        for (var time = 0d; time <= duration; time += 0.75)
            yield return time;
    }

    private static SimulationVector3 DefensiveTarget(DefensiveAssignment assignment, SimulationFrame frame, Guid quarterbackId)
    {
        if (frame.Players[assignment.PlayerId].AssignmentTarget.HasValue)
            return frame.Players[assignment.PlayerId].AssignmentTarget!.Value;
        if (assignment.CoverageType == DefensiveCoverageType.Man && assignment.TargetPlayerId.HasValue)
            return frame.Players[assignment.TargetPlayerId.Value].Position;
        if (assignment.CoverageType == DefensiveCoverageType.Rusher)
            return frame.Players[quarterbackId].Position;
        return new SimulationVector3(assignment.ZoneLandmark.X, 0.08f, assignment.ZoneLandmark.Y);
    }

    private static string Format(SimulationVector3 value) => $"({value.X:0.0},{value.Y:0.0},{value.Z:0.0})";

    private static bool Near(double left, double right) => Math.Abs(left - right) < 0.0001;

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
