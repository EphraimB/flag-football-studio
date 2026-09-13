using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlagFootballStudio.Domain;
using FlagFootballStudio.Persistence;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class DialogueAudioValidator : Node3D
{
    private readonly Dictionary<Guid, Node3D> _pawns = [];

    public async Task RunAsync()
    {
        var game = Game.CreatePrototype();
        var project = GameProject.CreatePrototype(game);
        var play = project.Plays[0];
        SpawnPlayers(game, project, play);
        await NextFrame();

        var speaker = game.Gold.Roster[0];
        var listener = game.Gold.Roster[1];
        var defender = game.Navy.Roster[0];
        var first = new DialogueLine(
            Guid.NewGuid(), speaker.Id, 0, 0.45, "Ready on one", 0.8f, SpeechStyle.Whisper, 12,
            listener.Id, DialogueExpression.Focused, DialogueGazeTargetKind.ListenerPlayer);
        var second = new DialogueLine(
            Guid.NewGuid(), defender.Id, 0.05, 0.45, "Watch the short route", 1, SpeechStyle.Shout, 12,
            speaker.Id, DialogueExpression.Frustrated, DialogueGazeTargetKind.Football);
        var sequence = new DialogueSequence(Guid.NewGuid(), "Pre-snap exchange", DialogueSequenceContext.Play, play.Id);
        sequence.AddLine(first);
        sequence.AddLine(second);
        project.AddDialogueSequence(sequence);

        ValidateRoundTrip(project, sequence);
        ValidateAttenuation(first, second);

        var football = new FootballView { Name = "DialogueValidationFootball", Position = new Vector3(0, 0.9f, 1) };
        AddChild(football);
        var camera = new Camera3D { Name = "DialogueValidationListener", Position = new Vector3(0, 2, 5), Current = true };
        AddChild(camera);
        var controller = new DialoguePlaybackController { Name = "DialogueValidationController" };
        AddChild(controller);
        controller.Configure(_pawns, football, camera);

        var speakerPawn = (PlayerPawn)_pawns[speaker.Id];
        var initialExpression = speakerPawn.FacialExpression;
        var initialMouth = speakerPawn.MouthShape;
        var playback = controller.PlaySequencesAsync([sequence]);
        await WaitSeconds(0.12);
        Require(controller.ActiveLineCount == 2 && controller.PeakConcurrentLineCount == 2,
            "Overlapping dialogue lines did not play concurrently.");
        var source = controller.SourceFor(first.Id);
        Require(source is not null && source.GetParent() == speakerPawn.MouthAudioAnchor,
            "Spatial dialogue source was not attached to the speaking player's mouth anchor.");
        Require(speakerPawn.FacialExpression == FacialExpressionState.Focused,
            "Dialogue expression was not applied.");
        Require(speakerPawn.IsSpeechShapeCycling, "Dialogue did not start deterministic mouth cycling.");
        Require(Mathf.Abs(speakerPawn.TargetHorizontalGazeDegrees) <= HumanoidEyeRig.MaximumHorizontalGazeDegrees,
            "Dialogue gaze exceeded the eye rig limits.");
        await playback;
        await WaitSeconds(0.25);
        Require(controller.ActiveLineCount == 0, "Dialogue sources remained active after their lines ended.");
        Require(speakerPawn.FacialExpression == initialExpression && speakerPawn.MouthShape == initialMouth,
            "Dialogue did not restore expression and mouth state.");

        ValidateListenerModes(controller, camera, speakerPawn);
        ValidatePlaySwitching(project, game);
        controller.QueueFree();
        camera.QueueFree();
        football.QueueFree();
        foreach (var pawn in _pawns.Values) pawn.QueueFree();
    }

    private void SpawnPlayers(Game game, GameProject project, PlayDefinition play)
    {
        foreach (var team in new[] { game.Gold, game.Navy })
            foreach (var player in team.Roster)
            {
                var pawn = new PlayerPawn { Name = $"Dialogue_{team.Name}_{player.Name}" };
                pawn.Configure(player, project.AppearanceFor(player.Id), project.ActiveUniformFor(team.Id));
                AddChild(pawn);
                var point = play.StartingPositions[player.Id];
                pawn.Position = new Vector3(point.X, 0.08f, point.Y);
                _pawns[player.Id] = pawn;
            }
    }

    private static void ValidateRoundTrip(GameProject project, DialogueSequence sequence)
    {
        var serializer = new ProjectJsonSerializer();
        var json = serializer.SerializeProject(project);
        var restored = serializer.DeserializeProject(json);
        var restoredSequence = restored.DialogueSequence(sequence.Id);
        Require(restoredSequence.Context == sequence.Context && restoredSequence.PlayId == sequence.PlayId,
            "Dialogue sequence attachment did not survive JSON round trip.");
        var line = restoredSequence.Lines[0];
        var expected = sequence.Lines[0];
        Require(line.SpeakerPlayerId == expected.SpeakerPlayerId && line.Text == expected.Text &&
                line.SpeechStyle == expected.SpeechStyle && line.ListenerPlayerId == expected.ListenerPlayerId &&
                line.Expression == expected.Expression && line.GazeTargetKind == expected.GazeTargetKind,
            "Dialogue line metadata did not survive JSON round trip.");
    }

    private static void ValidateAttenuation(DialogueLine whisper, DialogueLine shout)
    {
        Require(DialoguePlaybackController.EffectiveAudibilityRadius(whisper) < whisper.AudibilityRadius,
            "Whisper range was not constrained to nearby listeners.");
        Require(DialoguePlaybackController.EffectiveAudibilityRadius(shout) > shout.AudibilityRadius,
            "Shout range did not extend beyond normal speech.");
        Require(DialoguePlaybackController.EstimateAudibility(whisper, 8) == 0 &&
                DialoguePlaybackController.EstimateAudibility(shout, 8) > 0,
            "Whisper and shout attenuation did not produce distinct intelligibility ranges.");
    }

    private static void ValidateListenerModes(DialoguePlaybackController controller, Camera3D camera, PlayerPawn pawn)
    {
        camera.GlobalPosition = pawn.EyeAnchor.GlobalPosition;
        Require(controller.ListenerPosition.IsEqualApprox(pawn.EyeAnchor.GlobalPosition),
            "Player POV did not use the active preview camera listener position.");
        camera.GlobalPosition = new Vector3(14, 18, 22);
        Require(controller.ListenerPosition.IsEqualApprox(new Vector3(14, 18, 22)),
            "Broadcast preview did not use its camera position as the spatial listener.");
        var direction = controller.DirectionFromListener(pawn.MouthAudioAnchor.GlobalPosition);
        Require(direction.IsFinite() && direction.Length() > 0.9f,
            "Spatial source direction relative to the listener was invalid.");
    }

    private static void ValidatePlaySwitching(GameProject project, Game game)
    {
        var secondPlay = PlayDefinition.CreatePrototype(game, "Second play");
        project.AddPlay(secondPlay);
        Require(project.DialogueForPlay(secondPlay.Id).Count == 0 && project.DialogueForPlay(project.Plays[0].Id).Count == 1,
            "Play switching did not isolate play-attached dialogue.");
        var restored = new ProjectJsonSerializer().DeserializeProject(new ProjectJsonSerializer().SerializeProject(project));
        Require(restored.DialogueForPlay(project.Plays[0].Id).Count == 1,
            "Project reload lost play-attached dialogue.");
    }

    private async Task NextFrame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private async Task WaitSeconds(double seconds) =>
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
