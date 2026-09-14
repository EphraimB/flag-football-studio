using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FlagFootballStudio.Domain;

namespace FlagFootballStudio.Tts;

public enum AmbientSpeechResolutionState
{
    Ready,
    Queued,
    Pending,
    MissingVoiceConfiguration,
    QueueFull,
    Failed
}

public sealed record AmbientSpeechClip(
    string CacheKey,
    VoiceAudioReference AudioReference,
    double DurationSeconds,
    IReadOnlyList<VisemeEvent> LipSyncEvents,
    LipSyncSource LipSyncSource);

public readonly record struct AmbientSpeechResolution(
    AmbientSpeechResolutionState State,
    string CacheKey,
    AmbientSpeechClip? Clip,
    string? Reason);

public readonly record struct AmbientSpeechCacheDiagnostics(
    int Hits,
    int Misses,
    int Pending,
    int Ready,
    int Failures,
    int MaximumPending);

public sealed partial class SpeechGenerationService
{
    public const int MaximumAmbientPendingRequests = 8;
    public const int MaximumAmbientGenerationConcurrency = 2;
    private static readonly TimeSpan AmbientGenerationTimeout = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions AmbientJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly object _ambientGate = new();
    private readonly SemaphoreSlim _ambientSlots = new(
        MaximumAmbientGenerationConcurrency, MaximumAmbientGenerationConcurrency);
    private readonly Dictionary<string, Task<AmbientSpeechClip>> _ambientPending = [];
    private readonly Dictionary<string, AmbientSpeechClip> _ambientReady = [];
    private readonly Dictionary<string, string> _ambientFailures = [];
    private int _ambientHits;
    private int _ambientMisses;

    public AmbientSpeechCacheDiagnostics AmbientDiagnostics
    {
        get
        {
            lock (_ambientGate)
                return new AmbientSpeechCacheDiagnostics(_ambientHits, _ambientMisses,
                    _ambientPending.Count, _ambientReady.Count, _ambientFailures.Count,
                    MaximumAmbientPendingRequests);
        }
    }

    public AmbientSpeechResolution ResolveOrQueueAmbient(PlayerVoiceProfile profile, string phrase)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (string.IsNullOrWhiteSpace(phrase))
            return new AmbientSpeechResolution(AmbientSpeechResolutionState.Failed, string.Empty, null,
                "Ambient phrase text is empty.");
        if (profile.BackendType == TtsBackendType.None || string.IsNullOrWhiteSpace(profile.ModelIdOrPath))
            return new AmbientSpeechResolution(AmbientSpeechResolutionState.MissingVoiceConfiguration,
                string.Empty, null, "The speaking player has no local TTS voice configured.");
        if (profile.BackendType != _provider.BackendType)
            return new AmbientSpeechResolution(AmbientSpeechResolutionState.MissingVoiceConfiguration,
                string.Empty, null, "The speaking player's voice uses a different TTS provider.");

        var key = AmbientCacheKey(profile, phrase, _provider.BackendType);
        lock (_ambientGate)
        {
            if (_ambientReady.TryGetValue(key, out var memoryClip) &&
                _assets.IsValidWave(memoryClip.AudioReference))
            {
                _ambientHits++;
                return new AmbientSpeechResolution(AmbientSpeechResolutionState.Ready, key, memoryClip, null);
            }

            if (TryLoadAmbientClip(key, out var diskClip))
            {
                _ambientReady[key] = diskClip;
                _ambientHits++;
                return new AmbientSpeechResolution(AmbientSpeechResolutionState.Ready, key, diskClip, null);
            }

            if (_ambientPending.ContainsKey(key))
            {
                _ambientHits++;
                return new AmbientSpeechResolution(AmbientSpeechResolutionState.Pending, key, null,
                    "Speech generation is already pending; this occurrence was skipped.");
            }

            if (_ambientFailures.TryGetValue(key, out var failure))
                return new AmbientSpeechResolution(AmbientSpeechResolutionState.Failed, key, null, failure);
            if (_ambientPending.Count >= MaximumAmbientPendingRequests)
                return new AmbientSpeechResolution(AmbientSpeechResolutionState.QueueFull, key, null,
                    $"Ambient TTS queue is full ({MaximumAmbientPendingRequests}).");

            _ambientMisses++;
            var task = GenerateAmbientCoreAsync(key, phrase.Trim(), profile);
            _ambientPending[key] = task;
            _ = ObserveAmbientGenerationAsync(key, task);
            return new AmbientSpeechResolution(AmbientSpeechResolutionState.Queued, key, null,
                "Speech generation queued; this occurrence was skipped.");
        }
    }

    public async Task WaitForAmbientIdleAsync()
    {
        Task[] pending;
        lock (_ambientGate) pending = _ambientPending.Values.Cast<Task>().ToArray();
        try { await Task.WhenAll(pending); }
        catch { /* Failure state is retained and reported by ResolveOrQueueAmbient. */ }
        await Task.Yield();
    }

    public void InvalidateAmbientClip(string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey)) return;
        lock (_ambientGate)
        {
            _ambientReady.Remove(cacheKey);
            _ambientFailures.Remove(cacheKey);
        }
        try
        {
            var reference = _assets.CreateAmbientGeneratedWaveReference(cacheKey);
            var wave = _assets.Resolve(reference);
            var metadata = _assets.AmbientMetadataPath(reference);
            if (File.Exists(wave)) File.Delete(wave);
            if (File.Exists(metadata)) File.Delete(metadata);
        }
        catch
        {
            // A later lookup still fails safely if an invalid cache artifact cannot be removed.
        }
    }

    public static string AmbientCacheKey(PlayerVoiceProfile profile, string phrase, TtsBackendType provider)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var invariant = CultureInfo.InvariantCulture;
        var canonical = string.Join('\n',
            profile.Id.ToString("N"),
            profile.PlayerId.ToString("N"),
            provider.ToString(),
            profile.BackendType.ToString(),
            profile.ModelIdOrPath ?? string.Empty,
            profile.SpeakerId ?? string.Empty,
            profile.ReferenceAudio?.RelativePath ?? string.Empty,
            profile.Style ?? string.Empty,
            profile.Emotion ?? string.Empty,
            profile.DefaultSpeakingVolume.ToString("R", invariant),
            profile.DefaultPitchAdjustment.ToString("R", invariant),
            profile.DefaultSpeakingRate.ToString("R", invariant),
            phrase.Trim());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private async Task<AmbientSpeechClip> GenerateAmbientCoreAsync(
        string key, string phrase, PlayerVoiceProfile profile)
    {
        await Task.Yield();
        await _ambientSlots.WaitAsync();
        try
        {
            if (TryLoadAmbientClip(key, out var cached)) return cached;
            var reference = _assets.CreateAmbientGeneratedWaveReference(key);
            RemoveCorruptAmbientArtifacts(reference);
            var requestId = Guid.NewGuid();
            var referencePath = profile.ReferenceAudio is null ? null : _assets.Resolve(profile.ReferenceAudio);
            var request = new TtsGenerationRequest(requestId, phrase, _assets.Resolve(reference),
                profile.ModelIdOrPath!, profile.SpeakerId, referencePath, profile.Style, profile.Emotion,
                profile.DefaultSpeakingVolume, profile.DefaultPitchAdjustment, profile.DefaultSpeakingRate,
                true, true);
            using var timeout = new CancellationTokenSource(AmbientGenerationTimeout);
            TtsGenerationResult result;
            try
            {
                // Providers are engine-independent. Process startup and I/O stay off the Godot frame thread.
                result = await Task.Run(() => _provider.GenerateAsync(request, timeout.Token), timeout.Token);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("Ambient local TTS generation timed out after 30 seconds.");
            }
            if (!result.Success)
                throw new InvalidOperationException(result.Error ?? "Ambient local TTS generation failed.");
            if (!_assets.IsValidWave(reference))
                throw new InvalidDataException("Ambient TTS produced a missing or corrupt WAV file.");

            var duration = Math.Clamp(result.Duration, 0.1, 120);
            var lipSync = AutomaticLipSyncGenerator.FromProviderTimings(result.Phonemes, duration);
            if (lipSync.Events.Count == 0)
                lipSync = AutomaticLipSyncGenerator.Approximate(phrase, duration);
            var clip = new AmbientSpeechClip(key, reference, duration, lipSync.Events, lipSync.Source);
            WriteAmbientMetadata(clip);
            return clip;
        }
        finally
        {
            _ambientSlots.Release();
        }
    }

    private async Task ObserveAmbientGenerationAsync(string key, Task<AmbientSpeechClip> task)
    {
        try
        {
            var clip = await task;
            lock (_ambientGate)
            {
                _ambientPending.Remove(key);
                _ambientFailures.Remove(key);
                _ambientReady[key] = clip;
            }
        }
        catch (Exception exception)
        {
            lock (_ambientGate)
            {
                _ambientPending.Remove(key);
                _ambientFailures[key] = $"Ambient TTS skipped: {exception.Message}";
            }
        }
    }

    private bool TryLoadAmbientClip(string key, out AmbientSpeechClip clip)
    {
        var reference = _assets.CreateAmbientGeneratedWaveReference(key);
        var metadataPath = _assets.AmbientMetadataPath(reference);
        try
        {
            if (!_assets.IsValidWave(reference) || !File.Exists(metadataPath))
            {
                clip = null!;
                return false;
            }
            var metadata = JsonSerializer.Deserialize<AmbientSpeechMetadata>(
                File.ReadAllText(metadataPath), AmbientJson);
            if (metadata is null || metadata.CacheKey != key || metadata.DurationSeconds is < 0.1 or > 120)
            {
                clip = null!;
                return false;
            }
            var events = metadata.Visemes
                .Select(item => new VisemeEvent(item.StartTime, item.EndTime, item.Viseme, item.BlendStrength))
                .OrderBy(item => item.StartTime).ToArray();
            clip = new AmbientSpeechClip(key, reference, metadata.DurationSeconds, events,
                metadata.LipSyncSource);
            return true;
        }
        catch
        {
            clip = null!;
            return false;
        }
    }

    private void WriteAmbientMetadata(AmbientSpeechClip clip)
    {
        var metadata = new AmbientSpeechMetadata
        {
            CacheKey = clip.CacheKey,
            DurationSeconds = clip.DurationSeconds,
            LipSyncSource = clip.LipSyncSource,
            Visemes = clip.LipSyncEvents.Select(item => new AmbientVisemeMetadata
            {
                StartTime = item.StartTime,
                EndTime = item.EndTime,
                Viseme = item.Viseme,
                BlendStrength = item.BlendStrength
            }).ToList()
        };
        File.WriteAllText(_assets.AmbientMetadataPath(clip.AudioReference),
            JsonSerializer.Serialize(metadata, AmbientJson));
    }

    private void RemoveCorruptAmbientArtifacts(VoiceAudioReference reference)
    {
        var wave = _assets.Resolve(reference);
        var metadata = _assets.AmbientMetadataPath(reference);
        if (File.Exists(wave) && !_assets.IsValidWave(reference)) File.Delete(wave);
        if (!File.Exists(wave) && File.Exists(metadata)) File.Delete(metadata);
    }

    private sealed class AmbientSpeechMetadata
    {
        public string CacheKey { get; set; } = string.Empty;
        public double DurationSeconds { get; set; }
        public LipSyncSource LipSyncSource { get; set; }
        public List<AmbientVisemeMetadata> Visemes { get; set; } = [];
    }

    private sealed class AmbientVisemeMetadata
    {
        public double StartTime { get; set; }
        public double? EndTime { get; set; }
        public DialogueViseme Viseme { get; set; }
        public float BlendStrength { get; set; }
    }
}
