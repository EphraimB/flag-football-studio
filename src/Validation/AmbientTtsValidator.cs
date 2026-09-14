using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlagFootballStudio.Domain;
using FlagFootballStudio.Persistence;
using FlagFootballStudio.Tts;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class AmbientTtsValidator : Node3D
{
    public async Task RunAsync()
    {
        var mainThread = System.Environment.CurrentManagedThreadId;
        var game = Game.CreatePrototype();
        var project = GameProject.CreatePrototype(game);
        var play = project.Plays[0];
        var simulator = new FootballPlaySimulator();
        var baseline = Snapshot(simulator.Simulate(play, game.Gold, game.Navy));
        var root = ProjectSettings.GlobalizePath($"user://ambient-tts-validation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var assets = new ProjectAudioAssetStore(Path.Combine(root, "game-project.json"));
            var provider = new FakeAmbientProvider(80);
            var service = new SpeechGenerationService(provider, assets);
            var firstPlayer = game.Gold.Roster[0];
            var secondPlayer = game.Gold.Roster[1];
            var firstProfile = Configure(project.VoiceProfileFor(firstPlayer.Id), "voice-a.onnx", "1", "calm");
            var secondProfile = Configure(project.VoiceProfileFor(secondPlayer.Id), "voice-b.onnx", "2", "focused");

            var stopwatch = Stopwatch.StartNew();
            var queued = service.ResolveOrQueueAmbient(firstProfile, "I'm open.");
            stopwatch.Stop();
            Require(queued.State == AmbientSpeechResolutionState.Queued && stopwatch.ElapsedMilliseconds < 50,
                "Ambient generation blocked the gameplay caller instead of queueing work.");
            await service.WaitForAmbientIdleAsync();
            var firstReady = service.ResolveOrQueueAmbient(firstProfile, "I'm open.");
            var firstHit = service.ResolveOrQueueAmbient(firstProfile, "I'm open.");
            Require(firstReady is { State: AmbientSpeechResolutionState.Ready, Clip: not null } &&
                    firstHit.Clip?.AudioReference.RelativePath == firstReady.Clip.AudioReference.RelativePath,
                "Identical voice and phrase did not reuse cached ambient speech.");
            var firstClip = firstReady.Clip ?? throw new InvalidOperationException("Ambient cache lost its ready clip.");
            Require(firstClip.AudioReference.RelativePath.StartsWith("audio/generated/ambient/",
                    StringComparison.Ordinal) && assets.IsValidWave(firstClip.AudioReference),
                "Ambient speech was not stored as a valid portable generated WAV.");

            service.ResolveOrQueueAmbient(secondProfile, "I'm open.");
            await service.WaitForAmbientIdleAsync();
            var secondReady = service.ResolveOrQueueAmbient(secondProfile, "I'm open.");
            Require(secondReady.Clip is not null &&
                    secondReady.Clip.AudioReference.RelativePath != firstReady.Clip.AudioReference.RelativePath,
                "Two differently configured players incorrectly shared cached speech.");
            Require(provider.Requests.Any(request => request.ModelIdOrPath == "voice-a.onnx" && request.SpeakerId == "1") &&
                    provider.Requests.Any(request => request.ModelIdOrPath == "voice-b.onnx" && request.SpeakerId == "2"),
                "Ambient phrases did not resolve the speaking players' own voice configuration.");

            var originalKey = firstReady.CacheKey;
            firstProfile.ConfigureTts(TtsBackendType.PiperLocal, "voice-a-v2.onnx", "7", null, "urgent", "alert");
            var changed = service.ResolveOrQueueAmbient(firstProfile, "I'm open.");
            Require(changed.CacheKey != originalKey && changed.State == AmbientSpeechResolutionState.Queued,
                "Changing voice configuration did not invalidate/rekey ambient cache lookup.");
            await service.WaitForAmbientIdleAsync();

            var missing = project.VoiceProfileFor(game.Navy.Roster[0].Id);
            var silent = service.ResolveOrQueueAmbient(missing, "Switch.");
            Require(silent.State == AmbientSpeechResolutionState.MissingVoiceConfiguration &&
                    silent.Clip is null,
                "A missing player voice did not remain intentionally silent.");

            service.ResolveOrQueueAmbient(secondProfile, "FAIL this phrase");
            await service.WaitForAmbientIdleAsync();
            var failed = service.ResolveOrQueueAmbient(secondProfile, "FAIL this phrase");
            Require(failed.State == AmbientSpeechResolutionState.Failed && failed.Clip is null,
                "A failed TTS subprocess was not retained as a safe skipped utterance.");

            var boundedProvider = new FakeAmbientProvider(140);
            var bounded = new SpeechGenerationService(boundedProvider, assets);
            var queueResults = Enumerable.Range(0, SpeechGenerationService.MaximumAmbientPendingRequests + 1)
                .Select(index => bounded.ResolveOrQueueAmbient(secondProfile, $"Bounded phrase {index}"))
                .ToArray();
            Require(queueResults.Count(result => result.State == AmbientSpeechResolutionState.QueueFull) == 1 &&
                    bounded.AmbientDiagnostics.Pending <= SpeechGenerationService.MaximumAmbientPendingRequests,
                "Ambient TTS pending queue was not bounded.");
            await bounded.WaitForAmbientIdleAsync();

            Require(provider.ProviderThreadIds.All(thread => thread != mainThread) &&
                    boundedProvider.ProviderThreadIds.All(thread => thread != mainThread),
                "TTS provider work ran on the Godot scene-tree thread.");
            Require(Snapshot(simulator.Simulate(play, game.Gold, game.Navy)) == baseline,
                "Ambient generation changed the authoritative football simulation.");

            await ValidateGodotPlayback(game, project, play, simulator, assets, service, mainThread);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private async Task ValidateGodotPlayback(Game game, GameProject project, PlayDefinition play,
        FootballPlaySimulator simulator, ProjectAudioAssetStore assets,
        SpeechGenerationService service, int mainThread)
    {
        foreach (var rosterProfile in project.PlayerVoiceProfiles.Values)
            if (rosterProfile.BackendType == TtsBackendType.None)
                Configure(rosterProfile, $"voice-{rosterProfile.PlayerId:N}.onnx",
                    rosterProfile.PlayerId.ToString("N"), "ambient");

        var venue = new VenueEnvironment { Name = "AmbientTtsVenue" };
        AddChild(venue);
        var football = new FootballView { Name = "AmbientTtsFootball" };
        AddChild(football);
        var camera = new Camera3D { Name = "AmbientTtsListener", Current = true };
        AddChild(camera);
        var pawns = SpawnPlayers(game, project, play);
        var dialogue = new DialoguePlaybackController { Name = "AmbientTtsDialogue" };
        AddChild(dialogue);
        dialogue.Configure(pawns, football, camera, assets, project.PlayerVoiceProfiles);
        var audio = new VenueAudioController { Name = "AmbientTtsVenueAudio" };
        AddChild(audio);
        audio.Configure(venue, pawns, football, camera, project.PlayerVoiceProfiles, service, dialogue);
        audio.ApplySettings(new AmbientAudioSettings(1, 1, 1, 1, true, true));

        var simulation = simulator.Simulate(play, game.Gold, game.Navy);
        var cue = VenueAudioController.BuildConversationSchedule(
            simulation, pawns, project.PlayerVoiceProfiles).First();
        var profile = project.VoiceProfileFor(cue.SpeakerPlayerId);
        service.ResolveOrQueueAmbient(profile, cue.Text);
        await service.WaitForAmbientIdleAsync();

        var persistentIds = audio.PersistentSourceInstanceIds.ToArray();
        var previousMouth = ((PlayerPawn)pawns[cue.SpeakerPlayerId]).MouthShape;
        audio.BeginPlay(simulation);
        audio.HandleSimulationFrame(simulation.FrameAt(cue.TimeSeconds));
        var lineId = audio.LastAmbientDialogueLineId ??
            throw new InvalidOperationException("Ready ambient speech did not start a transient dialogue line.");
        var source = dialogue.AmbientSourceFor(lineId);
        var pawn = (PlayerPawn)pawns[cue.SpeakerPlayerId];
        Require(source is not null && source.GetParent() == pawn.MouthAudioAnchor,
            "Ambient speech did not originate at the correct player's mouth/head anchor.");
        var activeSource = source ??
            throw new InvalidOperationException("Ambient speech source disappeared after playback start.");
        Require(audio.LastAmbientPlaybackThreadId == mainThread,
            "Godot audio/scene-tree playback was started from a background worker.");
        Require(dialogue.LipSyncModeFor(lineId) == LipSyncPlaybackMode.AutomaticTimed,
            "Provider timings were not reused for ambient lip sync.");

        camera.GlobalPosition = pawn.MouthAudioAnchor.GlobalPosition + Vector3.Forward;
        Require(camera.GlobalPosition.DistanceTo(activeSource.GlobalPosition) < 1.1f && activeSource.MaxDistance >= 8,
            "Player POV proximity did not retain local spatial audibility.");
        await ToSignal(GetTree().CreateTimer(0.08), SceneTreeTimer.SignalName.Timeout);
        Require(pawn.MouthShape != previousMouth,
            "Ambient timed visemes did not drive the speaker's mouth.");

        var authored = new DialogueLine(Guid.NewGuid(), cue.ListenerPlayerId, 0, 0.16,
            "Authored dialogue wins.");
        var authoredPlayback = dialogue.PreviewLineAsync(authored);
        Require(dialogue.ActiveAmbientLineCount == 0 && dialogue.ActiveAuthoredLineCount == 1,
            "Authored dialogue did not suppress an already-playing ambient phrase.");
        await authoredPlayback;
        Require(pawn.MouthShape == previousMouth,
            "Ambient mouth/expression state was not restored after suppression.");
        Require(persistentIds.SequenceEqual(audio.PersistentSourceInstanceIds) &&
                audio.PersistentSourcesPlaying == audio.PersistentSpatialSourceCount,
            "Ambient TTS changed or stopped procedural crowd/sideline chatter beds.");
        audio.Shutdown();
        dialogue.StopAll();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.08), SceneTreeTimer.SignalName.Timeout);
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    private Dictionary<Guid, Node3D> SpawnPlayers(
        Game game, GameProject project, PlayDefinition play)
    {
        var pawns = new Dictionary<Guid, Node3D>();
        foreach (var team in new[] { game.Gold, game.Navy })
        foreach (var player in team.Roster)
        {
            var pawn = new PlayerPawn { Name = $"AmbientTts_{team.Name}_{player.Name}" };
            pawn.Configure(player, project.AppearanceFor(player.Id), project.ActiveUniformFor(team.Id));
            AddChild(pawn);
            var start = play.StartingPositions[player.Id];
            pawn.Position = new Vector3(start.X, 0.08f, start.Y);
            pawns.Add(player.Id, pawn);
        }
        return pawns;
    }

    private static PlayerVoiceProfile Configure(
        PlayerVoiceProfile profile, string model, string speaker, string style)
    {
        profile.ConfigureTts(TtsBackendType.PiperLocal, model, speaker, null, style, "neutral");
        return profile;
    }

    private static string Snapshot(PlaySimulation simulation) => string.Join('|',
        simulation.Frames.SelectMany(frame => frame.Players.OrderBy(player => player.Key)
            .Select(player => $"{frame.TimeSeconds:R}:{player.Key}:{player.Value.Position}"))) +
        $"|{simulation.Outcome.Kind}:{simulation.Outcome.EndTimeSeconds:R}";

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class FakeAmbientProvider(int delayMilliseconds) : ITtsProvider
    {
        public TtsBackendType BackendType => TtsBackendType.PiperLocal;
        public ConcurrentQueue<TtsGenerationRequest> Requests { get; } = [];
        public ConcurrentBag<int> ProviderThreadIds { get; } = [];

        public Task<TtsProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new TtsProviderCapabilities("ambient-fake", "Ambient validation", true, true,
                false, false, true, [22050], []));

        public async Task<TtsGenerationResult> GenerateAsync(
            TtsGenerationRequest request, CancellationToken cancellationToken = default)
        {
            ProviderThreadIds.Add(System.Environment.CurrentManagedThreadId);
            Requests.Enqueue(request);
            await Task.Delay(delayMilliseconds, cancellationToken);
            if (request.Text.Contains("FAIL", StringComparison.Ordinal))
                return new TtsGenerationResult(false, null, 0, 0, [], "none", "Simulated Piper failure.");
            WriteWave(request.OutputWavePath, 0.42);
            IReadOnlyList<TtsPhonemeTiming> timings =
            [
                new("m", 0.02, 0.10),
                new("a", 0.12, 0.20),
                new("o", 0.22, 0.30)
            ];
            return new TtsGenerationResult(true, request.OutputWavePath, 0.42, 22050,
                timings, "validation", null);
        }

        private static void WriteWave(string path, double duration)
        {
            const int rate = 22050;
            var samples = (int)(rate * duration);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var stream = File.Create(path);
            using var writer = new BinaryWriter(stream);
            writer.Write("RIFF"u8.ToArray()); writer.Write(36 + samples * 2); writer.Write("WAVE"u8.ToArray());
            writer.Write("fmt "u8.ToArray()); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
            writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write("data"u8.ToArray()); writer.Write(samples * 2);
            for (var index = 0; index < samples; index++)
                writer.Write((short)(Math.Sin(2 * Math.PI * 190 * index / rate) * 5200));
        }
    }
}
