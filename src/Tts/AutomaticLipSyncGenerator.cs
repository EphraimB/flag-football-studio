using System;
using System.Collections.Generic;
using System.Linq;
using FlagFootballStudio.Domain;

namespace FlagFootballStudio.Tts;

public sealed record AutomaticLipSyncResult(IReadOnlyList<VisemeEvent> Events, LipSyncSource Source);

public static class AutomaticLipSyncGenerator
{
    public static AutomaticLipSyncResult FromProviderTimings(IEnumerable<TtsPhonemeTiming> timings, double duration)
    {
        var events = timings
            .Where(item => item.EndTime > item.StartTime && item.StartTime < duration)
            .Select(item => new VisemeEvent(item.StartTime, Math.Min(item.EndTime, duration), Map(item.Phoneme), 0.9f))
            .Where(item => item.Viseme != DialogueViseme.Rest)
            .ToArray();
        return events.Length > 0
            ? new AutomaticLipSyncResult(events, LipSyncSource.AutomaticTimed)
            : new AutomaticLipSyncResult([], LipSyncSource.None);
    }

    public static AutomaticLipSyncResult Approximate(string text, double duration)
    {
        if (string.IsNullOrWhiteSpace(text) || duration < 0.1)
            return new AutomaticLipSyncResult([], LipSyncSource.None);
        var shapes = text.ToLowerInvariant()
            .Where(char.IsLetter)
            .Select(character => Map(character.ToString()))
            .Where(shape => shape != DialogueViseme.Rest)
            .Take(80)
            .ToArray();
        if (shapes.Length == 0) shapes = [DialogueViseme.A];
        var step = duration / shapes.Length;
        var events = shapes.Select((shape, index) =>
        {
            var start = index * step;
            var end = Math.Min(duration, start + Math.Min(step * 0.78, 0.16));
            return new VisemeEvent(start, end, shape, 0.72f);
        }).ToArray();
        return new AutomaticLipSyncResult(events, LipSyncSource.AutomaticApproximate);
    }

    public static DialogueViseme Map(string phoneme)
    {
        var value = phoneme.Trim().ToLowerInvariant();
        if (value.Length == 0) return DialogueViseme.Rest;
        if (value.IndexOfAny(['m', 'b', 'p']) >= 0) return DialogueViseme.Mbp;
        if (value.IndexOfAny(['f', 'v']) >= 0) return DialogueViseme.Fv;
        if (value.Contains('l')) return DialogueViseme.L;
        if (value.IndexOfAny(['w', 'q']) >= 0) return DialogueViseme.Wq;
        if (value.IndexOfAny(['o']) >= 0) return DialogueViseme.O;
        if (value.IndexOfAny(['u']) >= 0) return DialogueViseme.U;
        if (value.IndexOfAny(['e']) >= 0) return DialogueViseme.E;
        if (value.IndexOfAny(['i', 'y']) >= 0) return DialogueViseme.I;
        if (value.IndexOfAny(['a']) >= 0) return DialogueViseme.A;
        return DialogueViseme.Rest;
    }
}
