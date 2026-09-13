using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FlagFootballStudio.Domain;
using FlagFootballStudio.Persistence;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class VoiceLipSyncValidator : Node3D
{
    private readonly Dictionary<Guid, Node3D> _pawns = [];

    public async Task RunAsync()
    {
        var game = Game.CreatePrototype();
        var project = GameProject.CreatePrototype(game);
        var play = project.Plays[0];
        var speakerA = game.Gold.Roster[0];
        var speakerB = game.Navy.Roster[0];
        var speakerC = game.Gold.Roster[1];
        project.VoiceProfileFor(speakerA.Id).Update("Low QB", "Validation voice", 0.75f, -3, 1.1f);

        var validationDirectory = ProjectSettings.GlobalizePath($"user://voice-validation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(validationDirectory);
        var sourceWav = Path.Combine(validationDirectory, "source.wav");
        WriteTestWav(sourceWav, 0.5);
        var assetStore = new ProjectAudioAssetStore(Path.Combine(validationDirectory, "game-project.json"));
        var audioReference = assetStore.Import(sourceWav);

        var silentLine = new DialogueLine(Guid.NewGuid(), speakerA.Id, 0, 0.5, "Silent script line");
        var genericLine = new DialogueLine(Guid.NewGuid(), speakerB.Id, 0, 0.5, "Recorded generic line",
            audioReference: audioReference);
        var events = new[]
        {
            new VisemeEvent(0.03, 0.16, DialogueViseme.A, 0.8f),
            new VisemeEvent(0.20, null, DialogueViseme.O, 1)
        };
        var manualLine = new DialogueLine(Guid.NewGuid(), speakerC.Id, 0, 0.55, "Timestamped line", 1,
            SpeechStyle.Normal, 14, speakerA.Id, DialogueExpression.Smile,
            DialogueGazeTargetKind.Football, audioReference: audioReference, lipSyncEvents: events);
        var sequence = new DialogueSequence(Guid.NewGuid(), "Voice validation", DialogueSequenceContext.Play, play.Id);
        sequence.AddLine(silentLine); sequence.AddLine(genericLine); sequence.AddLine(manualLine);
        project.AddDialogueSequence(sequence);

        ValidateDomainBounds(speakerA.Id);
        ValidatePersistence(project, sequence, audioReference);
        SpawnPlayers(game, project, play);
        await NextFrame();

        var football = new FootballView { Name = "VoiceValidationFootball", Position = new Vector3(0, 0.9f, 1) };
        AddChild(football);
        var cameraHome = new Node3D { Name = "VoiceValidationCameraHome" }; AddChild(cameraHome);
        var camera = new Camera3D { Name = "VoiceValidationCamera", Current = true }; cameraHome.AddChild(camera);
        var cameraController = new CameraDirectorController { Name = "VoiceValidationCameraDirector" }; AddChild(cameraController);
        cameraController.Configure(camera, cameraHome, _pawns, football);
        cameraController.Preview(project.Cameras[0], play);

        var playback = new DialoguePlaybackController { Name = "VoiceValidationPlayback" }; AddChild(playback);
        var diagnostics = new List<string>();
        playback.AudioDiagnosticsReported += diagnostics.Add;
        playback.Configure(_pawns, football, camera, assetStore, project.PlayerVoiceProfiles);
        var task = playback.PlaySequencesAsync([sequence]);
        await WaitSeconds(0.09);
        Require(playback.ActiveLineCount == 3 && playback.PeakConcurrentLineCount == 3,
            "Multiple speaking players were not supported concurrently.");
        Require(playback.SourceFor(silentLine.Id)?.Stream is null,
            "A line without audio unexpectedly played the developer tone during normal playback.");
        Require(playback.SourceFor(genericLine.Id)?.Stream is AudioStreamWav &&
                playback.LipSyncModeFor(genericLine.Id) == LipSyncPlaybackMode.GenericFallback,
            "Audio without timestamps did not use real audio plus clearly identified generic mouth motion.");
        Require(playback.LipSyncModeFor(manualLine.Id) == LipSyncPlaybackMode.ManualTimestamped,
            "Timestamped dialogue was not identified as manual lip sync.");
        Require(diagnostics.Any(report => report.Contains("streamNonNull=True", StringComparison.Ordinal) &&
                                          report.Contains("playCalled=True", StringComparison.Ordinal) &&
                                          report.Contains("playingAfterStart=True", StringComparison.Ordinal)),
            $"Playback diagnostics did not confirm stream assignment and a running player. Reports: {string.Join(" | ", diagnostics)}");
        Require(diagnostics.Any(report => report.Contains("OUTSIDE MAX DISTANCE", StringComparison.Ordinal)),
            "The broadcast-listener/max-distance failure condition was not diagnosed.");
        var manualPawn = (PlayerPawn)_pawns[speakerC.Id];
        Require(manualPawn.FacialExpression == FacialExpressionState.Smile,
            "Expression did not layer with lip sync.");
        Require(manualPawn.HasSceneGazeTarget,
            "Dialogue gaze target did not remain layered with lip sync.");
        await WaitSeconds(0.16);
        await WaitSeconds(0.08);
        Require(manualPawn.MouthShape == SpeechMouthShape.O,
            $"Timestamped viseme did not drive the existing mouth controller (actual {manualPawn.MouthShape}).");

        var sourceBeforeCut = playback.SourceFor(manualLine.Id);
        var sideline = project.Cameras.First(cameraDefinition => cameraDefinition.Type == CameraType.SidelineLow);
        cameraController.Preview(sideline, play);
        await NextFrame();
        Require(playback.SourceFor(manualLine.Id) == sourceBeforeCut,
            "A camera cut interrupted spatial speech playback.");
        var pov = project.Cameras.First(cameraDefinition => cameraDefinition.Type == CameraType.PlayerPov);
        pov.SetPlayer(speakerC.Id);
        cameraController.Preview(pov, play);
        await NextFrame();
        Require(playback.ListenerCamera == camera && manualPawn.FirstPersonViewActive,
            "Player POV did not retain the active speech listener or body presentation.");
        await task;

        var rawReport = await playback.TestRawAudioAsync(genericLine);
        Require(rawReport.Contains("Raw 2D test", StringComparison.Ordinal) &&
                rawReport.Contains("bus=Master, busValid=True, masterMuted=False", StringComparison.Ordinal) &&
                rawReport.Contains("playingAfterStart=True", StringComparison.Ordinal),
            "The raw non-spatial diagnostic did not prove Master-bus playback.");
        var spatialReport = await playback.TestSpatialAudioAsync(genericLine);
        Require(spatialReport.Contains("Spatial 3D test", StringComparison.Ordinal) &&
                spatialReport.Contains("distance=1.000", StringComparison.Ordinal) &&
                spatialReport.Contains("playingAfterStart=True", StringComparison.Ordinal),
            "The forced-near spatial diagnostic did not prove 3D playback.");

        var previewTask = playback.PreviewLineAsync(silentLine);
        await WaitSeconds(0.05);
        Require(playback.SourceFor(silentLine.Id)?.Stream is AudioStreamWav,
            "Explicit preview did not provide the developer placeholder fallback for a line without audio.");
        playback.StopAll();
        await previewTask;

        cameraController.QueueFree(); playback.QueueFree(); cameraHome.QueueFree(); football.QueueFree();
        foreach (var pawn in _pawns.Values) pawn.QueueFree();
        Directory.Delete(validationDirectory, true);
    }

    private void SpawnPlayers(Game game, GameProject project, PlayDefinition play)
    {
        foreach (var team in new[] { game.Gold, game.Navy })
            foreach (var player in team.Roster)
            {
                var pawn = new PlayerPawn { Name = $"Voice_{team.Name}_{player.Name}" };
                pawn.Configure(player, project.AppearanceFor(player.Id), project.ActiveUniformFor(team.Id));
                AddChild(pawn);
                var point = play.StartingPositions[player.Id];
                pawn.Position = new Vector3(point.X, 0.08f, point.Y);
                _pawns[player.Id] = pawn;
            }
    }

    private static void ValidateDomainBounds(Guid playerId)
    {
        RequireThrows(() => new DialogueLine(Guid.NewGuid(), playerId, 0, 1, "Out of order",
            lipSyncEvents: [new VisemeEvent(0.7, null, DialogueViseme.A), new VisemeEvent(0.2, null, DialogueViseme.E)]));
        RequireThrows(() => new DialogueLine(Guid.NewGuid(), playerId, 0, 1, "Out of bounds",
            lipSyncEvents: [new VisemeEvent(1.1, null, DialogueViseme.A)]));
        RequireThrows(() => new DialogueLine(Guid.NewGuid(), playerId, 0, 1, "Overlap",
            lipSyncEvents: [new VisemeEvent(0.1, 0.5, DialogueViseme.A), new VisemeEvent(0.4, null, DialogueViseme.E)]));
    }

    private static void ValidatePersistence(GameProject project, DialogueSequence sequence, VoiceAudioReference reference)
    {
        var serializer = new ProjectJsonSerializer();
        var restored = serializer.DeserializeProject(serializer.SerializeProject(project));
        var profile = restored.VoiceProfileFor(sequence.Lines[0].SpeakerPlayerId);
        Require(profile.DisplayName == "Low QB" && profile.Description == "Validation voice" &&
                Mathf.IsEqualApprox(profile.DefaultSpeakingVolume, 0.75f) &&
                Mathf.IsEqualApprox(profile.DefaultPitchAdjustment, -3) &&
                Mathf.IsEqualApprox(profile.DefaultSpeakingRate, 1.1f),
            "Player voice profile did not survive JSON round trip.");
        var restoredManual = restored.DialogueSequence(sequence.Id).Lines[2];
        Require(restoredManual.AudioReference == reference,
            "Project-relative audio reference did not survive JSON round trip.");
        Require(restoredManual.LipSyncEvents.SequenceEqual(sequence.Lines[2].LipSyncEvents),
            "Timestamped visemes did not survive JSON round trip.");
        Require(restored.DialogueForPlay(sequence.PlayId!.Value).Single().Id == sequence.Id,
            "Play switching/project reload lost the voice-enabled dialogue sequence.");
    }

    private static void WriteTestWav(string path, double duration)
    {
        const int rate = 16000;
        var samples = (int)(rate * duration);
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8.ToArray()); writer.Write(36 + samples * 2); writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray()); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
        writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
        writer.Write("data"u8.ToArray()); writer.Write(samples * 2);
        for (var index = 0; index < samples; index++)
            writer.Write((short)(Math.Sin(2 * Math.PI * 180 * index / rate) * 1800));
    }

    private async Task NextFrame() => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    private async Task WaitSeconds(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
    private static void RequireThrows(Action action)
    {
        try { action(); }
        catch (ArgumentException) { return; }
        throw new InvalidOperationException("Invalid viseme data was accepted.");
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
