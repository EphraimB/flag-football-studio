using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FlagFootballStudio.Domain;

namespace FlagFootballStudio.Tts;

public sealed class PiperTtsProvider : ITtsProvider
{
    private readonly string _pythonExecutable;
    private readonly string _helperPath;
    private readonly string _modelsDirectory;
    private readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public PiperTtsProvider(string repositoryRoot, string? pythonExecutable = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        _helperPath = Path.Combine(repositoryRoot, "tools", "tts", "piper_service.py");
        _modelsDirectory = Path.Combine(repositoryRoot, "tools", "tts", "models");
        var venvPython = Path.Combine(repositoryRoot, "tools", "tts", ".venv", "Scripts", "python.exe");
        _pythonExecutable = pythonExecutable ?? (File.Exists(venvPython) ? venvPython : "python");
    }

    public TtsBackendType BackendType => TtsBackendType.PiperLocal;
    public string ModelsDirectory => _modelsDirectory;

    public Task<TtsProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        var voices = new PiperVoiceCatalog(_modelsDirectory).Discover()
            .Where(model => model.IsCompatible)
            .Select(model => new TtsVoiceDescriptor(model.ModelPath, model.Name, model.ModelPath))
            .ToArray();
        return Task.FromResult(new TtsProviderCapabilities("piper-local", "Piper (local ONNX)", true, true,
            false, false, true, new[] { 16000, 22050, 44100 } as IReadOnlyList<int>, voices));
    }

    public async Task<TtsGenerationResult> GenerateAsync(TtsGenerationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!File.Exists(_helperPath)) return Failed($"Piper helper is missing: {_helperPath}");
        if (!File.Exists(request.ModelIdOrPath))
            return Failed("The configured Piper .onnx model was not found. Install a voice explicitly; models are never downloaded automatically.");
        var configPath = request.ModelIdOrPath + ".json";
        if (!File.Exists(configPath)) return Failed($"The Piper model config is missing: {configPath}");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(request.OutputWavePath))!);

        var start = new ProcessStartInfo
        {
            FileName = _pythonExecutable,
            ArgumentList = { _helperPath },
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var process = new Process { StartInfo = start };
        try
        {
            process.Start();
            using var registration = cancellationToken.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(true); } catch { }
            });
            var payload = new
            {
                command = "generate",
                request_id = request.RequestId,
                text = request.Text,
                output_wave_path = request.OutputWavePath,
                model_path = request.ModelIdOrPath,
                speaker_id = request.SpeakerId,
                reference_audio_path = request.ReferenceAudioPath,
                style = request.Style,
                emotion = request.Emotion,
                volume = request.Volume,
                pitch_semitones = request.PitchSemitones,
                speaking_rate = request.SpeakingRate,
                prefer_cuda = request.PreferCuda,
                allow_cpu_fallback = request.AllowCpuFallback
            };
            await process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(payload, _json));
            process.StandardInput.Close();
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            if (process.ExitCode != 0)
                return Failed(string.IsNullOrWhiteSpace(stderr) ? "Piper generation failed." : stderr.Trim());
            var response = JsonSerializer.Deserialize<PiperResponse>(stdout, _json);
            if (response is null || !response.Success)
                return Failed(response?.Error ?? "Piper returned an invalid response.");
            var timings = response.Phonemes?.Select(item =>
                new TtsPhonemeTiming(item.Phoneme ?? string.Empty, item.StartTime, item.EndTime)).ToArray() ?? [];
            return new TtsGenerationResult(true, response.OutputWavePath, response.Duration, response.SampleRate,
                timings, response.Device ?? "unknown", null);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) { return Failed($"Unable to start local Piper: {exception.Message}"); }
    }

    private static TtsGenerationResult Failed(string message) => new(false, null, 0, 0, [], "none", message);
    private sealed class PiperResponse
    {
        public bool Success { get; set; }
        public string? OutputWavePath { get; set; }
        public double Duration { get; set; }
        public int SampleRate { get; set; }
        public string? Device { get; set; }
        public string? Error { get; set; }
        public List<PiperPhoneme>? Phonemes { get; set; }
    }
    private sealed class PiperPhoneme
    {
        public string? Phoneme { get; set; }
        public double StartTime { get; set; }
        public double EndTime { get; set; }
    }
}
