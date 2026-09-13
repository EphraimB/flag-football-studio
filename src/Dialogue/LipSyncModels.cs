using System;
using System.Linq;

namespace FlagFootballStudio.Domain;

public enum VoiceAudioFormat { Wav, OggVorbis, Mp3 }
public enum DialogueViseme { Rest, A, E, I, O, U, Mbp, Fv, L, Wq }
public enum LipSyncSource { None, AutomaticApproximate, AutomaticTimed, Manual }

public sealed record VoiceAudioReference
{
    public VoiceAudioReference(string relativePath, VoiceAudioFormat format)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) throw new ArgumentException("An audio asset path is required.", nameof(relativePath));
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        if (System.IO.Path.IsPathRooted(relativePath) || normalized.Split('/').Contains("..", StringComparer.Ordinal))
            throw new ArgumentException("Audio references must remain inside the project.", nameof(relativePath));
        var extension = System.IO.Path.GetExtension(normalized).ToLowerInvariant();
        var expected = format switch
        {
            VoiceAudioFormat.Wav => ".wav",
            VoiceAudioFormat.OggVorbis => ".ogg",
            VoiceAudioFormat.Mp3 => ".mp3",
            _ => throw new ArgumentOutOfRangeException(nameof(format))
        };
        if (extension != expected) throw new ArgumentException("Audio reference extension does not match its format.", nameof(relativePath));
        RelativePath = normalized;
        Format = format;
    }

    public string RelativePath { get; }
    public VoiceAudioFormat Format { get; }
}

public readonly record struct VisemeEvent
{
    public VisemeEvent(double startTime, double? endTime, DialogueViseme viseme, float blendStrength = 1)
    {
        if (!double.IsFinite(startTime) || startTime < 0) throw new ArgumentOutOfRangeException(nameof(startTime));
        if (endTime.HasValue && (!double.IsFinite(endTime.Value) || endTime.Value <= startTime))
            throw new ArgumentOutOfRangeException(nameof(endTime));
        if (!float.IsFinite(blendStrength) || blendStrength is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(blendStrength));
        StartTime = startTime;
        EndTime = endTime;
        Viseme = viseme;
        BlendStrength = blendStrength;
    }

    public double StartTime { get; }
    public double? EndTime { get; }
    public DialogueViseme Viseme { get; }
    public float BlendStrength { get; }
}
