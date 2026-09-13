using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlagFootballStudio.Domain;
using FlagFootballStudio.Persistence;
using Godot;

namespace FlagFootballStudio.Presentation;

public enum LipSyncPlaybackMode { GenericFallback, ManualTimestamped }

public partial class DialoguePlaybackController : Node
{
    private readonly Dictionary<Guid, ActiveLine> _activeLines = [];
    private readonly Dictionary<Guid, SpeakerSnapshot> _speakerSnapshots = [];
    private IReadOnlyDictionary<Guid, Node3D> _pawns = new Dictionary<Guid, Node3D>();
    private FootballView _football = null!;
    private Camera3D _listenerCamera = null!;
    private ProjectAudioAssetStore? _audioAssets;
    private IReadOnlyDictionary<Guid, PlayerVoiceProfile> _voiceProfiles = new Dictionary<Guid, PlayerVoiceProfile>();
    private int _generation;

    public int ActiveLineCount => _activeLines.Count;
    public int PeakConcurrentLineCount { get; private set; }
    public Camera3D ListenerCamera => _listenerCamera;
    public Vector3 ListenerPosition => _listenerCamera.GlobalPosition;

    public void Configure(IReadOnlyDictionary<Guid, Node3D> pawns, FootballView football, Camera3D listenerCamera,
        ProjectAudioAssetStore? audioAssets = null,
        IReadOnlyDictionary<Guid, PlayerVoiceProfile>? voiceProfiles = null)
    {
        StopAll();
        _pawns = pawns ?? throw new ArgumentNullException(nameof(pawns));
        _football = football ?? throw new ArgumentNullException(nameof(football));
        _listenerCamera = listenerCamera ?? throw new ArgumentNullException(nameof(listenerCamera));
        _audioAssets = audioAssets;
        _voiceProfiles = voiceProfiles ?? new Dictionary<Guid, PlayerVoiceProfile>();
        PeakConcurrentLineCount = 0;
    }

    public async Task PlaySequencesAsync(IEnumerable<DialogueSequence> sequences)
    {
        ArgumentNullException.ThrowIfNull(sequences);
        var generation = _generation;
        await Task.WhenAll(sequences.SelectMany(sequence => sequence.Lines)
            .Select(line => PlayScheduledLineAsync(line, generation)));
    }

    public Task PreviewLineAsync(DialogueLine line, double seekSeconds = 0) =>
        PlayLineAsync(line, _generation, true, true, seekSeconds);

    public void StopAll()
    {
        _generation++;
        foreach (var active in _activeLines.Values.ToArray())
        {
            active.Source.Stop();
            active.Source.QueueFree();
        }
        _activeLines.Clear();
        foreach (var snapshot in _speakerSnapshots.Values)
            snapshot.Restore();
        _speakerSnapshots.Clear();
    }

    public AudioStreamPlayer3D? SourceFor(Guid lineId) =>
        _activeLines.TryGetValue(lineId, out var active) ? active.Source : null;

    public LipSyncPlaybackMode? LipSyncModeFor(Guid lineId) =>
        _activeLines.TryGetValue(lineId, out var active) ? active.LipSyncMode : null;

    public static float StyleRangeMultiplier(SpeechStyle style) => style switch
    {
        SpeechStyle.Whisper => 0.42f,
        SpeechStyle.Quiet => 0.68f,
        SpeechStyle.Loud => 1.35f,
        SpeechStyle.Shout => 1.85f,
        _ => 1f
    };

    public static float EffectiveAudibilityRadius(DialogueLine line) =>
        line.AudibilityRadius * StyleRangeMultiplier(line.SpeechStyle);

    public static float EstimateAudibility(DialogueLine line, float distance)
    {
        var radius = EffectiveAudibilityRadius(line);
        if (distance >= radius) return 0;
        var normalized = Mathf.Clamp(1 - distance / radius, 0, 1);
        return line.Volume * normalized * normalized;
    }

    public Vector3 DirectionFromListener(Vector3 sourcePosition)
    {
        var direction = sourcePosition - _listenerCamera.GlobalPosition;
        if (direction.LengthSquared() < 0.000001f) return Vector3.Zero;
        return _listenerCamera.GlobalBasis.Inverse() * direction.Normalized();
    }

    private async Task PlayScheduledLineAsync(DialogueLine line, int generation)
    {
        if (line.StartTime > 0)
            await ToSignal(GetTree().CreateTimer(line.StartTime), SceneTreeTimer.SignalName.Timeout);
        await PlayLineAsync(line, generation, true, false, 0);
    }

    private async Task PlayLineAsync(DialogueLine line, int generation, bool honorGeneration,
        bool allowPlaceholderTone, double seekSeconds)
    {
        if (honorGeneration && generation != _generation) return;
        if (!_pawns.TryGetValue(line.SpeakerPlayerId, out var node) || node is not PlayerPawn speaker)
            throw new InvalidOperationException("Dialogue speaker is not present in the 3D scene.");

        if (!_speakerSnapshots.ContainsKey(line.SpeakerPlayerId))
            _speakerSnapshots[line.SpeakerPlayerId] = new SpeakerSnapshot(speaker);

        seekSeconds = Math.Clamp(seekSeconds, 0, Math.Max(0, line.Duration - 0.01));
        ApplyPresentation(line, speaker);
        var source = CreateSource(line, allowPlaceholderTone);
        speaker.MouthAudioAnchor.AddChild(source);
        if (source.Stream is not null)
            source.Play((float)seekSeconds);
        var lipSyncMode = line.HasManualLipSync ? LipSyncPlaybackMode.ManualTimestamped : LipSyncPlaybackMode.GenericFallback;
        _activeLines[line.Id] = new ActiveLine(line, speaker, source, lipSyncMode);
        PeakConcurrentLineCount = Math.Max(PeakConcurrentLineCount, ActiveLineCount);

        var remaining = line.Duration - seekSeconds;
        if (line.HasManualLipSync)
        {
            await Task.WhenAll(
                DriveManualTrackAsync(line, speaker, seekSeconds, generation),
                WaitSeconds(remaining));
        }
        else
        {
            while (remaining > 0 && (!honorGeneration || generation == _generation))
            {
                speaker.StartSpeechShapeCycle(0.11f);
                var slice = Math.Min(remaining, 1.05);
                await ToSignal(GetTree().CreateTimer(slice), SceneTreeTimer.SignalName.Timeout);
                remaining -= slice;
            }
        }

        if (!_activeLines.Remove(line.Id, out var active)) return;
        active.Source.Stop();
        active.Source.QueueFree();
        if (_activeLines.Values.All(candidate => candidate.Speaker.Player?.Id != line.SpeakerPlayerId) &&
            _speakerSnapshots.Remove(line.SpeakerPlayerId, out var snapshot))
            snapshot.Restore();
    }

    private async Task DriveManualTrackAsync(DialogueLine line, PlayerPawn speaker, double seekSeconds, int generation)
    {
        var cursor = seekSeconds;
        foreach (var viseme in line.LipSyncEvents)
        {
            if (viseme.EndTime.HasValue && viseme.EndTime.Value <= seekSeconds) continue;
            if (viseme.StartTime > cursor) await WaitSeconds(viseme.StartTime - cursor);
            if (generation != _generation || !_activeLines.ContainsKey(line.Id)) return;
            speaker.SetMouthShapeWeighted(MapViseme(viseme.Viseme), viseme.BlendStrength, 0.07f);
            cursor = Math.Max(cursor, viseme.StartTime);
            if (!viseme.EndTime.HasValue) continue;
            if (viseme.EndTime.Value > cursor) await WaitSeconds(viseme.EndTime.Value - cursor);
            if (generation != _generation || !_activeLines.ContainsKey(line.Id)) return;
            speaker.SetMouthShape(SpeechMouthShape.Rest, 0.07f);
            cursor = viseme.EndTime.Value;
        }
    }

    private async Task WaitSeconds(double seconds) =>
        await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    private void ApplyPresentation(DialogueLine line, PlayerPawn speaker)
    {
        if (line.Expression.HasValue)
            speaker.SetFacialExpression(MapExpression(line.Expression.Value));
        switch (line.GazeTargetKind)
        {
            case DialogueGazeTargetKind.ListenerPlayer when line.ListenerPlayerId.HasValue:
                LookAtPlayer(speaker, line.ListenerPlayerId.Value);
                break;
            case DialogueGazeTargetKind.Player when line.GazeTargetPlayerId.HasValue:
                LookAtPlayer(speaker, line.GazeTargetPlayerId.Value);
                break;
            case DialogueGazeTargetKind.Football:
                speaker.LookAtFootball(_football);
                break;
            case DialogueGazeTargetKind.WorldPoint:
                speaker.LookAtWorldPoint(new Vector3(line.GazeWorldPoint.X, line.GazeWorldPoint.Y, line.GazeWorldPoint.Z));
                break;
        }
    }

    private void LookAtPlayer(PlayerPawn speaker, Guid playerId)
    {
        if (_pawns.TryGetValue(playerId, out var target) && target is PlayerPawn targetPawn)
            speaker.LookAtPlayer(targetPawn);
    }

    private AudioStreamPlayer3D CreateSource(DialogueLine line, bool allowPlaceholderTone)
    {
        AudioStream? stream = null;
        if (line.AudioReference is not null && _audioAssets is not null)
        {
            try { stream = VoiceAudioStreamLoader.Load(_audioAssets, line.AudioReference); }
            catch (Exception exception) { GD.PushWarning($"Voice asset unavailable for '{line.Text}': {exception.Message}"); }
        }
        else if (line.AudioReference is null && allowPlaceholderTone)
        {
            stream = CreateTone(line.SpeechStyle);
        }

        var profileVolume = 1f;
        var pitchSemitones = 0f;
        if (_voiceProfiles.TryGetValue(line.SpeakerPlayerId, out var profile))
        {
            profileVolume = profile.DefaultSpeakingVolume;
            pitchSemitones = profile.DefaultPitchAdjustment;
        }
        return new AudioStreamPlayer3D
        {
            Name = $"Dialogue_{line.Id:N}",
            Stream = stream,
            VolumeDb = Mathf.LinearToDb(Mathf.Max(line.Volume * profileVolume, 0.001f)),
            PitchScale = Mathf.Pow(2, pitchSemitones / 12f),
            UnitSize = 1,
            MaxDistance = EffectiveAudibilityRadius(line),
            AttenuationFilterDb = -18,
            Autoplay = false
        };
    }

    private static AudioStreamWav CreateTone(SpeechStyle style)
    {
        const int rate = 16000;
        const float seconds = 0.24f;
        var samples = (int)(rate * seconds);
        var bytes = new byte[samples * 2];
        var frequency = style switch
        {
            SpeechStyle.Whisper => 145f,
            SpeechStyle.Quiet => 165f,
            SpeechStyle.Loud => 205f,
            SpeechStyle.Shout => 225f,
            _ => 185f
        };
        for (var index = 0; index < samples; index++)
        {
            var pulse = 0.45 + 0.55 * Math.Sin(index * Math.PI * 4 / samples);
            var sample = (short)(Math.Sin(2 * Math.PI * frequency * index / rate) * pulse * 3200);
            bytes[index * 2] = (byte)(sample & 0xff);
            bytes[index * 2 + 1] = (byte)((sample >> 8) & 0xff);
        }
        return new AudioStreamWav
        {
            Format = AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = rate,
            Stereo = false,
            Data = bytes,
            LoopMode = AudioStreamWav.LoopModeEnum.Forward,
            LoopBegin = 0,
            LoopEnd = samples
        };
    }

    private static FacialExpressionState MapExpression(DialogueExpression expression) => expression switch
    {
        DialogueExpression.Smile => FacialExpressionState.Smile,
        DialogueExpression.Focused => FacialExpressionState.Focused,
        DialogueExpression.Concerned => FacialExpressionState.Concerned,
        DialogueExpression.Surprised => FacialExpressionState.Surprised,
        DialogueExpression.Frustrated => FacialExpressionState.Frustrated,
        _ => FacialExpressionState.Neutral
    };

    private static SpeechMouthShape MapViseme(DialogueViseme viseme) => viseme switch
    {
        DialogueViseme.A => SpeechMouthShape.A,
        DialogueViseme.E => SpeechMouthShape.E,
        DialogueViseme.I => SpeechMouthShape.I,
        DialogueViseme.O => SpeechMouthShape.O,
        DialogueViseme.U => SpeechMouthShape.U,
        DialogueViseme.Mbp => SpeechMouthShape.Mbp,
        DialogueViseme.Fv => SpeechMouthShape.Fv,
        DialogueViseme.L => SpeechMouthShape.L,
        DialogueViseme.Wq => SpeechMouthShape.Wq,
        _ => SpeechMouthShape.Rest
    };

    private sealed record ActiveLine(DialogueLine Line, PlayerPawn Speaker, AudioStreamPlayer3D Source, LipSyncPlaybackMode LipSyncMode);

    private sealed class SpeakerSnapshot
    {
        private readonly PlayerPawn _speaker;
        private readonly FacialExpressionState _expression;
        private readonly SpeechMouthShape _mouthShape;
        private readonly float _horizontalGaze;
        private readonly float _verticalGaze;

        public SpeakerSnapshot(PlayerPawn speaker)
        {
            _speaker = speaker;
            _expression = speaker.FacialExpression;
            _mouthShape = speaker.MouthShape;
            _horizontalGaze = speaker.TargetHorizontalGazeDegrees / HumanoidEyeRig.MaximumHorizontalGazeDegrees;
            _verticalGaze = speaker.TargetVerticalGazeDegrees / HumanoidEyeRig.MaximumVerticalGazeDegrees;
        }

        public void Restore()
        {
            if (!GodotObject.IsInstanceValid(_speaker)) return;
            _speaker.SetFacialExpression(_expression);
            _speaker.SetMouthShape(_mouthShape);
            _speaker.SetManualGaze(_horizontalGaze, _verticalGaze);
        }
    }
}
