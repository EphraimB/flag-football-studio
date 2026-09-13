using System;

namespace FlagFootballStudio.Domain;

public enum TtsBackendType { None, PiperLocal }

public sealed class PlayerVoiceProfile
{
    public PlayerVoiceProfile(Guid id, Guid playerId, string displayName, string? description = null,
        float defaultSpeakingVolume = 1, float defaultPitchAdjustment = 0, float defaultSpeakingRate = 1,
        TtsBackendType backendType = TtsBackendType.None, string? modelIdOrPath = null,
        string? speakerId = null, VoiceAudioReference? referenceAudio = null,
        string? style = null, string? emotion = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("A voice profile ID is required.", nameof(id));
        if (playerId == Guid.Empty) throw new ArgumentException("A player ID is required.", nameof(playerId));
        Id = id;
        PlayerId = playerId;
        Update(displayName, description, defaultSpeakingVolume, defaultPitchAdjustment, defaultSpeakingRate);
        ConfigureTts(backendType, modelIdOrPath, speakerId, referenceAudio, style, emotion);
    }

    public Guid Id { get; }
    public Guid PlayerId { get; }
    public string DisplayName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public float DefaultSpeakingVolume { get; private set; }
    public float DefaultPitchAdjustment { get; private set; }
    public float DefaultSpeakingRate { get; private set; }
    public TtsBackendType BackendType { get; private set; }
    public string? ModelIdOrPath { get; private set; }
    public string? SpeakerId { get; private set; }
    public VoiceAudioReference? ReferenceAudio { get; private set; }
    public string? Style { get; private set; }
    public string? Emotion { get; private set; }

    public void Update(string displayName, string? description, float volume, float pitchAdjustment, float speakingRate)
    {
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("A voice display name is required.", nameof(displayName));
        if (!float.IsFinite(volume) || volume is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(volume));
        if (!float.IsFinite(pitchAdjustment) || pitchAdjustment is < -12 or > 12) throw new ArgumentOutOfRangeException(nameof(pitchAdjustment));
        if (!float.IsFinite(speakingRate) || speakingRate is < 0.5f or > 2) throw new ArgumentOutOfRangeException(nameof(speakingRate));
        DisplayName = displayName.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DefaultSpeakingVolume = volume;
        DefaultPitchAdjustment = pitchAdjustment;
        DefaultSpeakingRate = speakingRate;
    }

    public void ConfigureTts(TtsBackendType backendType, string? modelIdOrPath, string? speakerId,
        VoiceAudioReference? referenceAudio, string? style, string? emotion)
    {
        if (backendType != TtsBackendType.None && string.IsNullOrWhiteSpace(modelIdOrPath))
            throw new ArgumentException("A local model ID or path is required for the selected TTS backend.", nameof(modelIdOrPath));
        BackendType = backendType;
        ModelIdOrPath = Clean(modelIdOrPath);
        SpeakerId = Clean(speakerId);
        ReferenceAudio = referenceAudio;
        Style = Clean(style);
        Emotion = Clean(emotion);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static PlayerVoiceProfile CreateDefault(Player player) => new(Guid.NewGuid(), player.Id, $"{player.Name} Voice");
}
