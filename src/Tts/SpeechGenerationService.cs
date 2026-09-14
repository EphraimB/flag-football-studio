using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FlagFootballStudio.Domain;
using FlagFootballStudio.Persistence;

namespace FlagFootballStudio.Tts;

public sealed record SpeechGenerationOutcome(DialogueLine Line, TtsGenerationResult ProviderResult);

public sealed partial class SpeechGenerationService
{
    private readonly ITtsProvider _provider;
    private readonly ProjectAudioAssetStore _assets;

    public SpeechGenerationService(ITtsProvider provider, ProjectAudioAssetStore assets)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _assets = assets ?? throw new ArgumentNullException(nameof(assets));
    }

    public async Task<SpeechGenerationOutcome> GenerateAsync(DialogueLine line, PlayerVoiceProfile profile,
        bool replaceExisting, CancellationToken cancellationToken = default)
    {
        if (profile.BackendType != _provider.BackendType)
            throw new InvalidOperationException("The selected voice profile is not configured for this local TTS provider.");
        if (line.AudioReference is not null && !replaceExisting)
            throw new InvalidOperationException("This line already has audio. Use Regenerate Speech to replace its assignment.");
        var requestId = Guid.NewGuid();
        var reference = _assets.CreateGeneratedWaveReference(line.Id, requestId);
        var output = _assets.Resolve(reference);
        var referencePath = profile.ReferenceAudio is null ? null : _assets.Resolve(profile.ReferenceAudio);
        var request = new TtsGenerationRequest(requestId, line.Text, output, profile.ModelIdOrPath!, profile.SpeakerId,
            referencePath, profile.Style, profile.Emotion, profile.DefaultSpeakingVolume,
            profile.DefaultPitchAdjustment, profile.DefaultSpeakingRate, true, true);
        TtsGenerationResult result;
        try
        {
            result = await _provider.GenerateAsync(request, cancellationToken);
        }
        catch
        {
            if (File.Exists(output)) File.Delete(output);
            throw;
        }
        if (!result.Success)
        {
            if (File.Exists(output)) File.Delete(output);
            throw new InvalidOperationException(result.Error ?? "Local speech generation failed.");
        }
        var duration = Math.Clamp(result.Duration, 0.1, 120);
        if (line.HasManualLipSync) duration = Math.Max(duration, line.Duration);
        var lipSync = line.HasManualLipSync
            ? new AutomaticLipSyncResult(line.LipSyncEvents, LipSyncSource.Manual)
            : AutomaticLipSyncGenerator.FromProviderTimings(result.Phonemes, duration);
        if (lipSync.Events.Count == 0 && !line.HasManualLipSync)
            lipSync = AutomaticLipSyncGenerator.Approximate(line.Text, duration);
        var replacement = Clone(line, duration, reference, lipSync.Events, lipSync.Source);
        return new SpeechGenerationOutcome(replacement, result);
    }

    public DialogueLine GenerateLipSync(DialogueLine line, double clipDuration, bool replaceManual)
    {
        if (line.HasManualLipSync && !replaceManual)
            throw new InvalidOperationException("Manual lip sync is preserved. Use Regenerate Lip Sync to replace it explicitly.");
        var result = AutomaticLipSyncGenerator.Approximate(line.Text, Math.Clamp(clipDuration, 0.1, 120));
        return Clone(line, line.Duration, line.AudioReference, result.Events, result.Source);
    }

    public static DialogueLine Clone(DialogueLine line, double duration, VoiceAudioReference? audio,
        System.Collections.Generic.IEnumerable<VisemeEvent> events, LipSyncSource source) =>
        new(line.Id, line.SpeakerPlayerId, line.StartTime, duration, line.Text, line.Volume, line.SpeechStyle,
            line.AudibilityRadius, line.ListenerPlayerId, line.Expression, line.GazeTargetKind,
            line.GazeTargetPlayerId, line.GazeWorldPoint, audio, events, source);
}
