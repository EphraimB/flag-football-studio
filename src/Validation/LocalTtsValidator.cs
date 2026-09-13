using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FlagFootballStudio.Domain;
using FlagFootballStudio.Persistence;
using FlagFootballStudio.Tts;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class LocalTtsValidator : Node
{
    public async Task RunAsync()
    {
        var game = Game.CreatePrototype();
        var project = GameProject.CreatePrototype(game);
        var player = game.Gold.Roster[0];
        var profile = project.VoiceProfileFor(player.Id);
        profile.Update("Local QB", "Piper validation", 0.8f, 1.5f, 1.1f);
        profile.ConfigureTts(TtsBackendType.PiperLocal, "validation.onnx", "2", null, "calm", "focused");

        var root = ProjectSettings.GlobalizePath($"user://tts-validation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var assets = new ProjectAudioAssetStore(Path.Combine(root, "game-project.json"));
            var provider = new FakeProvider();
            var service = new SpeechGenerationService(provider, assets);
            var line = new DialogueLine(Guid.NewGuid(), player.Id, 0, 1, "Break left and look for the ball");
            var generated = await service.GenerateAsync(line, profile, false);
            Require(provider.LastRequest is { PreferCuda: true, AllowCpuFallback: true },
                "Generation did not prefer CUDA with explicit CPU fallback.");
            Require(generated.Line.AudioReference is not null && assets.Exists(generated.Line.AudioReference),
                "Generated WAV was not placed in the project-relative audio store.");
            var generatedReference = generated.Line.AudioReference
                ?? throw new InvalidOperationException("Generated line lost its audio reference.");
            Require(generatedReference.RelativePath.StartsWith("audio/generated/", StringComparison.Ordinal),
                "Generated audio reference was not portable/project-relative.");
            Require(generated.Line.LipSyncSource == LipSyncSource.AutomaticTimed && generated.Line.LipSyncEvents.Count == 2,
                "Provider phoneme timestamps did not receive highest lip-sync priority.");

            var approximate = service.GenerateLipSync(new DialogueLine(Guid.NewGuid(), player.Id, 0, 1,
                "Imported recording", audioReference: generated.Line.AudioReference), 1, false);
            Require(approximate.LipSyncSource == LipSyncSource.AutomaticApproximate && approximate.LipSyncEvents.Count > 0,
                "Imported audio did not get deterministic approximate lip sync.");
            var manual = new DialogueLine(Guid.NewGuid(), player.Id, 0, 1, "Manual",
                lipSyncEvents: [new VisemeEvent(0, 0.2, DialogueViseme.Mbp)]);
            RequireThrows(() => service.GenerateLipSync(manual, 1, false),
                "Automatic generation overwrote manual visemes without explicit regeneration.");
            var replaced = service.GenerateLipSync(manual, 1, true);
            Require(replaced.LipSyncSource == LipSyncSource.AutomaticApproximate,
                "Explicit lip-sync regeneration did not replace the manual source.");

            var sequence = new DialogueSequence(Guid.NewGuid(), "TTS validation", DialogueSequenceContext.Play, project.Plays[0].Id);
            sequence.AddLine(generated.Line);
            project.AddDialogueSequence(sequence);
            var serializer = new ProjectJsonSerializer();
            var restored = serializer.DeserializeProject(serializer.SerializeProject(project));
            var restoredProfile = restored.VoiceProfileFor(player.Id);
            Require(restoredProfile.BackendType == TtsBackendType.PiperLocal &&
                    restoredProfile.ModelIdOrPath == "validation.onnx" && restoredProfile.SpeakerId == "2" &&
                    restoredProfile.Style == "calm" && restoredProfile.Emotion == "focused",
                "Voice backend/profile metadata did not survive JSON round trip.");
            var restoredLine = restored.DialogueSequence(sequence.Id).Lines.Single();
            Require(restoredLine.LipSyncSource == LipSyncSource.AutomaticTimed &&
                    restoredLine.LipSyncEvents.SequenceEqual(generated.Line.LipSyncEvents),
                "Automatic lip-sync source/timestamps did not survive JSON round trip.");

            var missing = new PiperTtsProvider(ProjectSettings.GlobalizePath("res://"));
            var unavailable = await missing.GenerateAsync(provider.LastRequest! with { ModelIdOrPath = Path.Combine(root, "missing.onnx") });
            Require(!unavailable.Success && unavailable.Error!.Contains("never downloaded", StringComparison.OrdinalIgnoreCase),
                "Missing local model did not produce a clear non-downloading failure.");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed class FakeProvider : ITtsProvider
    {
        public TtsBackendType BackendType => TtsBackendType.PiperLocal;
        public TtsGenerationRequest? LastRequest { get; private set; }
        public Task<TtsProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new TtsProviderCapabilities("fake", "Validation provider", true, true, false, false, true,
                [22050], [new TtsVoiceDescriptor("fake", "Fake Voice", "validation.onnx")]));
        public Task<TtsGenerationResult> GenerateAsync(TtsGenerationRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            WriteWave(request.OutputWavePath, 0.9);
            IReadOnlyList<TtsPhonemeTiming> timings =
            [
                new("b", 0.02, 0.14), new("r", 0.18, 0.30), new("e", 0.34, 0.52)
            ];
            return Task.FromResult(new TtsGenerationResult(true, request.OutputWavePath, 0.9, 22050,
                timings, "cuda", null));
        }
    }

    private static void WriteWave(string path, double duration)
    {
        const int rate = 22050;
        var samples = (int)(rate * duration);
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8.ToArray()); writer.Write(36 + samples * 2); writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray()); writer.Write(16); writer.Write((short)1); writer.Write((short)1);
        writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
        writer.Write("data"u8.ToArray()); writer.Write(samples * 2);
        for (var index = 0; index < samples; index++) writer.Write((short)0);
    }

    private static void RequireThrows(Action action, string message)
    {
        try { action(); } catch (InvalidOperationException) { return; }
        throw new InvalidOperationException(message);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
