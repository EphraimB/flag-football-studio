using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FlagFootballStudio.Domain;

namespace FlagFootballStudio.Tts;

public enum TtsGenerationState { Idle, Queued, Generating, Completed, Failed, Cancelled }

public sealed record TtsVoiceDescriptor(string Id, string DisplayName, string ModelIdOrPath);

public sealed record TtsProviderCapabilities(
    string ProviderId,
    string DisplayName,
    bool SupportsCuda,
    bool SupportsCpu,
    bool SupportsReferenceAudio,
    bool SupportsStyle,
    bool SupportsTimedPhonemes,
    IReadOnlyList<int> SampleRates,
    IReadOnlyList<TtsVoiceDescriptor> Voices);

public sealed record TtsGenerationRequest(
    Guid RequestId,
    string Text,
    string OutputWavePath,
    string ModelIdOrPath,
    string? SpeakerId,
    string? ReferenceAudioPath,
    string? Style,
    string? Emotion,
    float Volume,
    float PitchSemitones,
    float SpeakingRate,
    bool PreferCuda,
    bool AllowCpuFallback);

public readonly record struct TtsPhonemeTiming(string Phoneme, double StartTime, double EndTime);

public sealed record TtsGenerationResult(
    bool Success,
    string? OutputWavePath,
    double Duration,
    int SampleRate,
    IReadOnlyList<TtsPhonemeTiming> Phonemes,
    string Device,
    string? Error)
{
    public bool HasTimedPhonemes => Phonemes.Count > 0;
}

public interface ITtsProvider
{
    TtsBackendType BackendType { get; }
    Task<TtsProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default);
    Task<TtsGenerationResult> GenerateAsync(TtsGenerationRequest request, CancellationToken cancellationToken = default);
}
