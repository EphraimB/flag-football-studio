using System;
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

public partial class PlayerVoiceSetupValidator : Node
{
    public async Task RunAsync()
    {
        var root = ProjectSettings.GlobalizePath($"user://player-voice-setup-{Guid.NewGuid():N}");
        var models = Path.Combine(root, "models");
        Directory.CreateDirectory(models);
        try
        {
            var alpha = Path.Combine(models, "alpha.onnx");
            var bravo = Path.Combine(models, "bravo.onnx");
            File.WriteAllBytes(alpha, [1]);
            File.WriteAllText(alpha + ".json", "{}");
            File.WriteAllBytes(bravo, [2]);
            File.WriteAllText(Path.Combine(models, "orphan.onnx.json"), "{}");
            var before = Directory.GetFiles(models, "*", SearchOption.AllDirectories).Order().ToArray();

            var catalog = new PiperVoiceCatalog(models);
            var discovered = catalog.Discover();
            var after = Directory.GetFiles(models, "*", SearchOption.AllDirectories).Order().ToArray();
            Require(before.SequenceEqual(after), "Piper discovery modified or downloaded model files.");
            Require(discovered.Count == 3 && discovered.Count(model => model.IsCompatible) == 1,
                "Piper discovery did not distinguish complete and incomplete model pairs.");
            var incompleteModel = discovered.FirstOrDefault(model => !model.ConfigurationAvailable && model.ModelAvailable);
            var orphanConfiguration = discovered.FirstOrDefault(model => model.ConfigurationAvailable && !model.ModelAvailable);
            Require(incompleteModel is not null,
                "An .onnx without its .onnx.json was not reported as incomplete.");
            Require(orphanConfiguration is not null,
                "An orphaned .onnx.json was not reported as incomplete.");

            var game = Game.CreatePrototype();
            var project = GameProject.CreatePrototype(game);
            var panel = new PlayerStudioPanel { Name = "VoiceSetupValidationPanel", Visible = false };
            AddChild(panel);
            panel.Configure(project, game);
            panel.SetVoiceModels(discovered);
            var first = game.Gold.Roster[0];
            panel.SelectPlayer(first.Id);
            var backend = Find<OptionButton>(panel, "VoiceBackend");
            var modelOption = Find<OptionButton>(panel, "VoiceModel");
            backend.Select((int)TtsBackendType.PiperLocal);
            modelOption.Select(discovered.ToList().FindIndex(model => model.IsCompatible));
            Find<LineEdit>(panel, "VoiceSpeaker").Text = "4";
            Find<SpinBox>(panel, "VoiceRate").Value = 1.15;
            Find<SpinBox>(panel, "VoicePitch").Value = -1.5;
            Find<SpinBox>(panel, "VoiceVolume").Value = 0.8;
            Find<LineEdit>(panel, "VoiceStyle").Text = "sport";
            Find<LineEdit>(panel, "VoiceEmotion").Text = "focused";
            Require(panel.SaveSelectedVoice(), "Player Studio rejected a complete discovered Piper model.");

            var configured = project.VoiceProfileFor(first.Id);
            Require(configured.BackendType == TtsBackendType.PiperLocal && SameFile(configured.ModelIdOrPath, alpha) &&
                    configured.SpeakerId == "4" && Math.Abs(configured.DefaultSpeakingRate - 1.15f) < 0.001f,
                $"Player Studio did not update the selected PlayerVoiceProfile: backend={configured.BackendType}, model={configured.ModelIdOrPath}, speaker={configured.SpeakerId}, rate={configured.DefaultSpeakingRate}.");
            Require(panel.RosterVoiceSummary == "Voices configured: 1 / 10",
                "Roster voice configuration count was not updated.");

            PlayerVoiceProfile? testedProfile = null;
            panel.VoiceTestRequested += profile => testedProfile = profile;
            panel.RequestSelectedVoiceTest();
            Require(ReferenceEquals(testedProfile, configured),
                "Test Voice did not use the selected player's profile object.");

            var second = game.Navy.Roster[0];
            panel.SelectPlayer(second.Id);
            panel.PasteSelectedVoiceSettings();
            Require(project.VoiceProfileFor(second.Id).BackendType == TtsBackendType.None,
                "Paste without a prior copy unexpectedly assigned a generic voice.");
            panel.SelectPlayer(first.Id);
            panel.CopySelectedVoiceSettings();
            panel.SelectPlayer(second.Id);
            panel.PasteSelectedVoiceSettings();
            Require(SameFile(project.VoiceProfileFor(second.Id).ModelIdOrPath, alpha),
                "Explicit copy/paste did not transfer voice settings.");
            panel.ClearSelectedVoice();
            Require(project.VoiceProfileFor(second.Id).BackendType == TtsBackendType.None &&
                    project.VoiceProfileFor(second.Id).ModelIdOrPath is null,
                "Clear Voice did not restore intentional silence.");

            var serializer = new ProjectJsonSerializer();
            var restored = serializer.DeserializeProject(serializer.SerializeProject(project));
            var restoredProfile = restored.VoiceProfileFor(first.Id);
            Require(SameFile(restoredProfile.ModelIdOrPath, alpha) && restoredProfile.SpeakerId == "4" &&
                    restoredProfile.Style == "sport" && restoredProfile.Emotion == "focused",
                "Configured PlayerVoiceProfile did not survive project reopen/JSON round trip.");

            var assets = new ProjectAudioAssetStore(Path.Combine(root, "project", "game-project.json"));
            var provider = new DelayedFakeProvider();
            var speech = new SpeechGenerationService(provider, assets);
            var line = new DialogueLine(Guid.NewGuid(), first.Id, 0, 1, "Ready for the next play.");
            var stopwatch = Stopwatch.StartNew();
            var testTask = speech.GenerateAsync(line, configured, false);
            Require(stopwatch.ElapsedMilliseconds < 80 && !testTask.IsCompleted,
                "Voice test generation blocked the calling/UI thread.");
            var generated = await testTask;
            Require(SameFile(provider.LastRequest?.ModelIdOrPath, alpha) && generated.ProviderResult.Success,
                "Voice test generation did not use the selected model.");

            var firstKey = SpeechGenerationService.AmbientCacheKey(configured, "I'm open.", provider.BackendType);
            var identicalKey = SpeechGenerationService.AmbientCacheKey(configured, "I'm open.", provider.BackendType);
            Require(firstKey == identicalKey, "Existing ambient cache key became unstable.");
            var ambient = speech.ResolveOrQueueAmbient(configured, "I'm open.");
            Require(ambient.State == AmbientSpeechResolutionState.Queued,
                "Newly configured Player Studio profile was not recognized by ambient TTS.");
            await speech.WaitForAmbientIdleAsync();
            Require(speech.ResolveOrQueueAmbient(configured, "I'm open.").State == AmbientSpeechResolutionState.Ready,
                "Configured ambient phrase was not reusable from cache.");
            configured.Update(configured.DisplayName, configured.Description, 0.8f, -1.5f, 1.25f);
            Require(firstKey != SpeechGenerationService.AmbientCacheKey(configured, "I'm open.", provider.BackendType),
                "Changed voice settings did not re-key the existing ambient cache.");

            panel.QueueFree();
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static T Find<T>(Node root, string name) where T : Node =>
        root.FindChild(name, true, false) as T
        ?? throw new InvalidOperationException($"Player Studio voice control '{name}' was not found.");

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static bool SameFile(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left) && !string.IsNullOrWhiteSpace(right) &&
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private sealed class DelayedFakeProvider : ITtsProvider
    {
        public TtsBackendType BackendType => TtsBackendType.PiperLocal;
        public TtsGenerationRequest? LastRequest { get; private set; }
        public Task<TtsProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new TtsProviderCapabilities("fake", "Fake Piper", true, true, false, true, true,
                [22050], []));

        public async Task<TtsGenerationResult> GenerateAsync(TtsGenerationRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            await Task.Delay(120, cancellationToken);
            WriteWave(request.OutputWavePath, 0.35);
            return new TtsGenerationResult(true, request.OutputWavePath, 0.35, 22050,
                [new TtsPhonemeTiming("r", 0, 0.15)], "validation-cpu", null);
        }
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
            writer.Write((short)(Math.Sin(index * Math.PI * 2 * 180 / rate) * 2800));
    }
}
