using System;
using System.Linq;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class FootballSimulationValidator : Node
{
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
                _ => SimulationEventType.PassCompleted
            };
            Require(authored.Events.Any(item => item.Type == expectedEvent), $"{kind} emitted the wrong ball-result event.");
            if (kind == PlayOutcomeKind.FlagPullAfterCatch)
                Require(authored.Events.Any(item => item.Type == SimulationEventType.FlagPulled), "Flag-pull outcome omitted the flag-pull event.");
            if (kind == PlayOutcomeKind.Touchdown)
                Require(authored.Events.Any(item => item.Type == SimulationEventType.Touchdown), "Touchdown omitted its scoring event.");
            if (kind == PlayOutcomeKind.OutOfBounds)
                Require(authored.Events.Any(item => item.Type == SimulationEventType.OutOfBounds), "Out-of-bounds outcome omitted its boundary event.");
        }

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

    private static SimulationEvent Event(PlaySimulation simulation, SimulationEventType type) =>
        simulation.Events.First(item => item.Type == type);

    private static bool Near(double left, double right) => Math.Abs(left - right) < 0.0001;

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
