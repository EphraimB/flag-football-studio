using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace FlagFootballStudio.Domain;

public enum SpeechStyle { Whisper, Quiet, Normal, Loud, Shout }
public enum DialogueSequenceContext { Play, PrePlayHuddle, PostPlay, Sideline }
public enum DialogueExpression { Neutral, Smile, Focused, Concerned, Surprised, Frustrated }
public enum DialogueGazeTargetKind { None, ListenerPlayer, Player, Football, WorldPoint }

public readonly record struct DialoguePoint(float X, float Y, float Z)
{
    public bool IsFinite => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z);
}

public sealed class DialogueLine
{
    public DialogueLine(
        Guid id,
        Guid speakerPlayerId,
        double startTime,
        double duration,
        string text,
        float volume = 1,
        SpeechStyle speechStyle = SpeechStyle.Normal,
        float audibilityRadius = 14,
        Guid? listenerPlayerId = null,
        DialogueExpression? expression = null,
        DialogueGazeTargetKind gazeTargetKind = DialogueGazeTargetKind.None,
        Guid? gazeTargetPlayerId = null,
        DialoguePoint gazeWorldPoint = default,
        VoiceAudioReference? audioReference = null,
        IEnumerable<VisemeEvent>? lipSyncEvents = null,
        LipSyncSource lipSyncSource = LipSyncSource.None)
    {
        if (id == Guid.Empty) throw new ArgumentException("A dialogue line ID is required.", nameof(id));
        if (speakerPlayerId == Guid.Empty) throw new ArgumentException("A speaker is required.", nameof(speakerPlayerId));
        if (!double.IsFinite(startTime) || startTime < 0) throw new ArgumentOutOfRangeException(nameof(startTime));
        if (!double.IsFinite(duration) || duration is < 0.1 or > 120) throw new ArgumentOutOfRangeException(nameof(duration));
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Dialogue text is required.", nameof(text));
        if (!float.IsFinite(volume) || volume is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(volume));
        if (!float.IsFinite(audibilityRadius) || audibilityRadius is < 0.5f or > 100) throw new ArgumentOutOfRangeException(nameof(audibilityRadius));
        if (gazeTargetKind == DialogueGazeTargetKind.WorldPoint && !gazeWorldPoint.IsFinite)
            throw new ArgumentException("The gaze world point must be finite.", nameof(gazeWorldPoint));
        var events = (lipSyncEvents ?? []).ToArray();
        for (var index = 0; index < events.Length; index++)
        {
            var viseme = events[index];
            if (viseme.StartTime > duration || (viseme.EndTime.HasValue && viseme.EndTime.Value > duration))
                throw new ArgumentOutOfRangeException(nameof(lipSyncEvents), "Viseme timing must fit inside the dialogue line.");
            if (index > 0 && viseme.StartTime < events[index - 1].StartTime)
                throw new ArgumentException("Viseme events must be ordered by start time.", nameof(lipSyncEvents));
            if (index > 0 && events[index - 1].EndTime.HasValue && viseme.StartTime < events[index - 1].EndTime.GetValueOrDefault())
                throw new ArgumentException("Viseme events with end times cannot overlap.", nameof(lipSyncEvents));
        }

        Id = id;
        SpeakerPlayerId = speakerPlayerId;
        StartTime = startTime;
        Duration = duration;
        Text = text.Trim();
        Volume = volume;
        SpeechStyle = speechStyle;
        AudibilityRadius = audibilityRadius;
        ListenerPlayerId = listenerPlayerId;
        Expression = expression;
        GazeTargetKind = gazeTargetKind;
        GazeTargetPlayerId = gazeTargetPlayerId;
        GazeWorldPoint = gazeWorldPoint;
        AudioReference = audioReference;
        LipSyncEvents = Array.AsReadOnly(events);
        LipSyncSource = events.Length > 0 && lipSyncSource == LipSyncSource.None
            ? LipSyncSource.Manual
            : lipSyncSource;
        if (events.Length == 0 && LipSyncSource != LipSyncSource.None)
            throw new ArgumentException("A lip-sync source requires at least one viseme event.", nameof(lipSyncSource));
    }

    public Guid Id { get; }
    public Guid SpeakerPlayerId { get; }
    public double StartTime { get; }
    public double Duration { get; }
    public string Text { get; }
    public float Volume { get; }
    public SpeechStyle SpeechStyle { get; }
    public float AudibilityRadius { get; }
    public Guid? ListenerPlayerId { get; }
    public DialogueExpression? Expression { get; }
    public DialogueGazeTargetKind GazeTargetKind { get; }
    public Guid? GazeTargetPlayerId { get; }
    public DialoguePoint GazeWorldPoint { get; }
    public VoiceAudioReference? AudioReference { get; }
    public IReadOnlyList<VisemeEvent> LipSyncEvents { get; }
    public LipSyncSource LipSyncSource { get; }
    public bool HasManualLipSync => LipSyncSource == LipSyncSource.Manual;
    public bool HasTimedLipSync => LipSyncEvents.Count > 0;
}

public sealed class DialogueSequence
{
    private readonly List<DialogueLine> _lines = [];
    private readonly ReadOnlyCollection<DialogueLine> _readOnlyLines;

    public DialogueSequence(Guid id, string name, DialogueSequenceContext context, Guid? playId = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("A dialogue sequence ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A dialogue sequence name is required.", nameof(name));
        if (context != DialogueSequenceContext.Sideline && (!playId.HasValue || playId == Guid.Empty))
            throw new ArgumentException("Play, pre-play, and post-play dialogue must reference a play.", nameof(playId));
        Id = id;
        Name = name.Trim();
        Context = context;
        PlayId = playId;
        _readOnlyLines = _lines.AsReadOnly();
    }

    public Guid Id { get; }
    public string Name { get; private set; }
    public DialogueSequenceContext Context { get; }
    public Guid? PlayId { get; }
    public IReadOnlyList<DialogueLine> Lines => _readOnlyLines;

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("A dialogue sequence name is required.", nameof(name));
        Name = name.Trim();
    }

    public void AddLine(DialogueLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (_lines.Any(existing => existing.Id == line.Id)) throw new InvalidOperationException("The dialogue line already exists.");
        _lines.Add(line);
    }

    public void ReplaceLine(DialogueLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var index = _lines.FindIndex(existing => existing.Id == line.Id);
        if (index < 0) throw new KeyNotFoundException("The dialogue line is not in this sequence.");
        _lines[index] = line;
    }

    public bool RemoveLine(Guid lineId) => _lines.RemoveAll(line => line.Id == lineId) > 0;

    public void MoveLine(Guid lineId, int offset)
    {
        var index = _lines.FindIndex(line => line.Id == lineId);
        if (index < 0) throw new KeyNotFoundException("The dialogue line is not in this sequence.");
        var destination = Math.Clamp(index + offset, 0, _lines.Count - 1);
        if (destination == index) return;
        var line = _lines[index];
        _lines.RemoveAt(index);
        _lines.Insert(destination, line);
    }
}
