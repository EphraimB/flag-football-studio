using System;
using System.Collections.Generic;
using Godot;

namespace FlagFootballStudio.Presentation;

public readonly record struct ProceduralAudioMetrics(
    int SampleCount,
    int NonZeroSampleCount,
    float PeakAmplitude,
    float RmsAmplitude,
    double DurationSeconds);

/// <summary>Shared, deterministic, developer-safe placeholder audio with no external assets.</summary>
public static class ProceduralVenueAudio
{
    private static readonly Dictionary<string, AudioStreamWav> Cache = [];
    private static int _ownerCount;

    public static int CachedStreamCount => Cache.Count;

    public static void Acquire() => _ownerCount++;

    public static void Release()
    {
        _ownerCount = Math.Max(0, _ownerCount - 1);
        if (_ownerCount != 0) return;
        foreach (var stream in Cache.Values)
            stream.Dispose();
        Cache.Clear();
    }

    public static AudioStreamWav CrowdLoop(int variation) => Cached($"crowd-{variation}", () =>
        Build(3.2f, true, (sample, rate) =>
        {
            var time = sample / (float)rate;
            var noise = Noise(sample, 1103 + variation * 97) * 0.14f;
            var voices = MathF.Sin(MathF.Tau * (92 + variation * 7) * time) * 0.13f +
                         MathF.Sin(MathF.Tau * (137 + variation * 11) * time) * 0.08f +
                         MathF.Sin(MathF.Tau * 61 * time) * 0.07f;
            return (noise + voices) * SlowEnvelope(time, 3.2f);
        }));

    public static AudioStreamWav ChatterLoop(int variation) => Cached($"chatter-{variation}", () =>
        Build(2.4f, true, (sample, rate) =>
        {
            var time = sample / (float)rate;
            var cadence = MathF.Max(0, MathF.Sin(MathF.Tau * (1.5f + variation * 0.08f) * time));
            var voice = MathF.Sin(MathF.Tau * (150 + variation * 19) * time) * 0.12f +
                        MathF.Sin(MathF.Tau * (225 + variation * 13) * time) * 0.055f;
            return voice * cadence * (0.7f + Noise(sample / 5, 701 + variation) * 0.3f) * 1.65f;
        }));

    public static AudioStreamWav Reaction(CrowdReactionKind kind) => Cached($"reaction-{kind}", () =>
    {
        var duration = kind switch
        {
            CrowdReactionKind.TouchdownCelebration => 2.2f,
            CrowdReactionKind.Interception => 1.65f,
            CrowdReactionKind.Cheer => 1.25f,
            CrowdReactionKind.Disappointment => 1.4f,
            _ => 0.85f
        };
        return Build(duration, false, (sample, rate) =>
        {
            var time = sample / (float)rate;
            var normalized = time / duration;
            var envelope = MathF.Sin(MathF.PI * Math.Clamp(normalized, 0, 1));
            var pitch = kind == CrowdReactionKind.Disappointment ? 92 : 155;
            var sweep = kind == CrowdReactionKind.Disappointment ? 1 - normalized * 0.35f : 1 + normalized * 0.22f;
            var voice = MathF.Sin(MathF.Tau * pitch * sweep * time) * 0.18f +
                        MathF.Sin(MathF.Tau * (pitch * 1.47f) * time) * 0.1f +
                        Noise(sample, 3001 + (int)kind * 113) * 0.16f;
            return voice * envelope;
        });
    });

    public static AudioStreamWav Action(string kind) => Cached($"action-{kind}", () => kind switch
    {
        "whistle" => Build(0.38f, false, (sample, rate) =>
        {
            var time = sample / (float)rate;
            var envelope = MathF.Sin(MathF.PI * Math.Clamp(time / 0.38f, 0, 1));
            return MathF.Sin(MathF.Tau * (2250 + MathF.Sin(time * 45) * 80) * time) * envelope * 0.34f;
        }),
        "footstep" => Build(0.11f, false, (sample, rate) =>
        {
            var time = sample / (float)rate;
            return (Noise(sample, 1889) * 0.24f + MathF.Sin(MathF.Tau * 72 * time) * 0.2f) *
                   MathF.Exp(-time * 28);
        }),
        "throw" => Build(0.22f, false, (sample, rate) =>
        {
            var time = sample / (float)rate;
            return Noise(sample, 4051) * MathF.Exp(-time * 15) * 0.22f;
        }),
        "catch" => Build(0.18f, false, (sample, rate) =>
        {
            var time = sample / (float)rate;
            return (MathF.Sin(MathF.Tau * 105 * time) * 0.28f + Noise(sample, 5113) * 0.12f) *
                   MathF.Exp(-time * 24);
        }),
        "flag" => Build(0.25f, false, (sample, rate) =>
        {
            var time = sample / (float)rate;
            return Noise(sample, 6221) * MathF.Sin(MathF.PI * time / 0.25f) * 0.18f;
        }),
        _ => Build(0.15f, false, (sample, rate) =>
        {
            var time = sample / (float)rate;
            return MathF.Sin(MathF.Tau * 120 * time) * MathF.Exp(-time * 20) * 0.2f;
        })
    });

    public static AudioStreamWav ConversationFallback(int variation) => Cached($"conversation-{variation}", () =>
        Build(0.65f, false, (sample, rate) =>
        {
            var time = sample / (float)rate;
            var syllables = MathF.Max(0, MathF.Sin(MathF.Tau * 4.5f * time));
            return (MathF.Sin(MathF.Tau * (145 + variation * 17) * time) * 0.15f +
                    MathF.Sin(MathF.Tau * (220 + variation * 9) * time) * 0.05f) * syllables;
        }));

    public static ProceduralAudioMetrics Inspect(AudioStreamWav stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var data = stream.Data;
        var sampleCount = data.Length / 2;
        if (sampleCount == 0)
            return new ProceduralAudioMetrics(0, 0, 0, 0, 0);
        var nonZero = 0;
        var peak = 0f;
        double squareSum = 0;
        for (var index = 0; index < sampleCount; index++)
        {
            var pcm = (short)(data[index * 2] | data[index * 2 + 1] << 8);
            var amplitude = pcm / (float)short.MaxValue;
            if (pcm != 0) nonZero++;
            peak = Math.Max(peak, Math.Abs(amplitude));
            squareSum += amplitude * amplitude;
        }
        return new ProceduralAudioMetrics(sampleCount, nonZero, peak,
            (float)Math.Sqrt(squareSum / sampleCount), sampleCount / (double)stream.MixRate);
    }

    private static AudioStreamWav Cached(string key, Func<AudioStreamWav> create)
    {
        if (Cache.TryGetValue(key, out var stream)) return stream;
        stream = create();
        Cache.Add(key, stream);
        return stream;
    }

    private static AudioStreamWav Build(float seconds, bool loop, Func<int, int, float> sampleValue)
    {
        const int rate = 16000;
        var sampleCount = Math.Max(1, (int)(seconds * rate));
        var data = new byte[sampleCount * 2];
        for (var sample = 0; sample < sampleCount; sample++)
        {
            var value = Math.Clamp(sampleValue(sample, rate), -0.92f, 0.92f);
            var pcm = (short)(value * short.MaxValue);
            data[sample * 2] = (byte)(pcm & 0xff);
            data[sample * 2 + 1] = (byte)((pcm >> 8) & 0xff);
        }
        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = rate,
            Stereo = false,
            Data = data,
            LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled,
            LoopBegin = 0,
            LoopEnd = sampleCount
        };
    }

    private static float Noise(int sample, int seed)
    {
        var value = unchecked((uint)(sample * 1664525 + seed * 1013904223));
        value ^= value >> 13;
        value *= 1274126177;
        return ((value & 0xffff) / 32767.5f) - 1;
    }

    private static float SlowEnvelope(float time, float duration) =>
        0.72f + MathF.Sin(MathF.Tau * time / duration) * 0.12f + MathF.Sin(MathF.Tau * 0.37f * time) * 0.08f;
}
