using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlagFootballStudio.Domain;
using FlagFootballStudio.Persistence;
using Godot;

namespace FlagFootballStudio.Presentation;

public enum LipSyncPlaybackMode { GenericFallback, AutomaticApproximate, AutomaticTimed, ManualTimestamped }

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

    public event Action<string>? AudioDiagnosticsReported;
    public event Action<AuthoredDialoguePriority, bool>? DialogueActivityChanged;

    public int ActiveLineCount => _activeLines.Count;
    public int AudibleLineCount => _activeLines.Values.Count(line => line.Audible);
    public int ActiveAmbientLineCount => _activeLines.Values.Count(line => line.IsAmbient);
    public int ActiveAuthoredLineCount => _activeLines.Values.Count(line => !line.IsAmbient);
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
        PlayLineAsync(line, _generation, true, true, seekSeconds, AuthoredDialoguePriority.Featured);

    public Task PlayAmbientLineAsync(DialogueLine line) =>
        PlayLineAsync(line, _generation, true, false, 0, AuthoredDialoguePriority.Natural, true);

    public void StopAll()
    {
        _generation++;
        foreach (var active in _activeLines.Values.ToArray())
        {
            active.Source.Stop();
            active.Source.Stream = null;
            active.Source.QueueFree();
            if (active.Audible && !active.IsAmbient)
                DialogueActivityChanged?.Invoke(active.Priority, false);
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

    public AudioStreamPlayer3D? AmbientSourceFor(Guid lineId) =>
        _activeLines.TryGetValue(lineId, out var active) && active.IsAmbient ? active.Source : null;

    public void StopAmbientLines()
    {
        foreach (var entry in _activeLines.Where(entry => entry.Value.IsAmbient).ToArray())
        {
            var active = entry.Value;
            active.Source.Stop();
            active.Source.Stream = null;
            active.Source.QueueFree();
            _activeLines.Remove(entry.Key);
            if (_activeLines.Values.All(candidate => candidate.Speaker.Player?.Id != active.Line.SpeakerPlayerId) &&
                _speakerSnapshots.Remove(active.Line.SpeakerPlayerId, out var snapshot))
                snapshot.Restore();
        }
    }

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
        await PlayLineAsync(line, generation, true, false, 0, AuthoredDialoguePriority.Natural);
    }

    private async Task PlayLineAsync(DialogueLine line, int generation, bool honorGeneration,
        bool allowPlaceholderTone, double seekSeconds, AuthoredDialoguePriority priority,
        bool isAmbient = false)
    {
        if (honorGeneration && generation != _generation) return;
        if (isAmbient)
        {
            if (ActiveAuthoredLineCount > 0 || ActiveAmbientLineCount > 0) return;
        }
        else
        {
            // Authored dialogue always wins before its speaker snapshot is taken.
            StopAmbientLines();
        }
        if (!_pawns.TryGetValue(line.SpeakerPlayerId, out var node) || node is not PlayerPawn speaker)
            throw new InvalidOperationException("Dialogue speaker is not present in the 3D scene.");

        if (!_speakerSnapshots.ContainsKey(line.SpeakerPlayerId))
            _speakerSnapshots[line.SpeakerPlayerId] = new SpeakerSnapshot(speaker);

        seekSeconds = Math.Clamp(seekSeconds, 0, Math.Max(0, line.Duration - 0.01));
        ApplyPresentation(line, speaker);
        var source = CreateSource(line, allowPlaceholderTone);
        speaker.MouthAudioAnchor.AddChild(source);
        var playCalled = false;
        if (source.Stream is not null)
        {
            source.Play((float)seekSeconds);
            playCalled = true;
        }
        ReportSpatialStart(line, source, speaker.MouthAudioAnchor.GlobalPosition, playCalled, "Normal dialogue");
        var lipSyncMode = line.LipSyncSource switch
        {
            LipSyncSource.Manual => LipSyncPlaybackMode.ManualTimestamped,
            LipSyncSource.AutomaticTimed => LipSyncPlaybackMode.AutomaticTimed,
            LipSyncSource.AutomaticApproximate => LipSyncPlaybackMode.AutomaticApproximate,
            _ => LipSyncPlaybackMode.GenericFallback
        };
        var audible = source.Stream is not null;
        _activeLines[line.Id] = new ActiveLine(line, speaker, source, lipSyncMode, priority, audible, isAmbient);
        if (audible && !isAmbient)
            DialogueActivityChanged?.Invoke(priority, true);
        PeakConcurrentLineCount = Math.Max(PeakConcurrentLineCount, ActiveLineCount);

        var remaining = line.Duration - seekSeconds;
        if (line.HasTimedLipSync)
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
        active.Source.Stream = null;
        active.Source.QueueFree();
        if (active.Audible && !active.IsAmbient)
            DialogueActivityChanged?.Invoke(active.Priority, false);
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
            Bus = "Master",
            Autoplay = false
        };
    }

    public async Task<string> TestRawAudioAsync(DialogueLine line)
    {
        var info = RequireAssignedAudio(line);
        var player = new AudioStreamPlayer
        {
            Name = $"RawAudioTest_{line.Id:N}",
            Stream = info.Stream,
            Bus = "Master",
            VolumeDb = 0
        };
        AddChild(player);
        player.Play();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var report = FormatDiagnostics("Raw 2D test", info, player.Stream is not null, true, player.Playing,
            player.VolumeDb, player.Bus, null, null, null, null, "2D: attenuation bypassed");
        AudioDiagnosticsReported?.Invoke(report);
        _ = CleanupPlayerAsync(player, info.StreamLengthSeconds);
        return report;
    }

    public async Task<string> TestSpatialAudioAsync(DialogueLine line)
    {
        var info = RequireAssignedAudio(line);
        var source = new AudioStreamPlayer3D
        {
            Name = $"SpatialAudioTest_{line.Id:N}",
            Stream = info.Stream,
            Bus = "Master",
            VolumeDb = 0,
            UnitSize = 1,
            MaxDistance = 100,
            AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.Disabled,
            AttenuationFilterDb = 0
        };
        AddChild(source);
        source.GlobalPosition = _listenerCamera.GlobalPosition - _listenerCamera.GlobalBasis.Z.Normalized();
        source.Play();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        var distance = _listenerCamera.GlobalPosition.DistanceTo(source.GlobalPosition);
        var report = FormatDiagnostics("Spatial 3D test", info, source.Stream is not null, true, source.Playing,
            source.VolumeDb, source.Bus, _listenerCamera.GlobalPosition, source.GlobalPosition, distance,
            source.MaxDistance, $"model={source.AttenuationModel}, unit={source.UnitSize:0.00}, filter={source.AttenuationFilterDb:0.00} dB");
        AudioDiagnosticsReported?.Invoke(report);
        _ = CleanupPlayerAsync(source, info.StreamLengthSeconds);
        return report;
    }

    private VoiceAudioStreamLoader.DecodedAudioInfo RequireAssignedAudio(DialogueLine line)
    {
        if (line.AudioReference is null) throw new InvalidOperationException("The selected line has no assigned audio.");
        if (_audioAssets is null) throw new InvalidOperationException("The project audio store is unavailable.");
        return VoiceAudioStreamLoader.Inspect(_audioAssets, line.AudioReference);
    }

    private void ReportSpatialStart(DialogueLine line, AudioStreamPlayer3D source,
        Vector3 sourcePosition, bool playCalled, string context)
    {
        VoiceAudioStreamLoader.DecodedAudioInfo? info = null;
        if (line.AudioReference is not null && _audioAssets is not null)
        {
            try { info = VoiceAudioStreamLoader.Inspect(_audioAssets, line.AudioReference); }
            catch (Exception exception)
            {
                AudioDiagnosticsReported?.Invoke($"{context}\nDecode diagnostics failed: {exception.Message}");
            }
        }
        if (info is null) return;
        var listenerPosition = _listenerCamera.GlobalPosition;
        var distance = listenerPosition.DistanceTo(sourcePosition);
        var attenuation = $"model={source.AttenuationModel}, unit={source.UnitSize:0.00}, filter={source.AttenuationFilterDb:0.00} dB";
        if (distance > source.MaxDistance) attenuation += " — OUTSIDE MAX DISTANCE";
        AudioDiagnosticsReported?.Invoke(FormatDiagnostics(context, info, source.Stream is not null,
            playCalled, source.Playing, source.VolumeDb, source.Bus, listenerPosition, sourcePosition,
            distance, source.MaxDistance, attenuation));
    }

    private static string FormatDiagnostics(string context, VoiceAudioStreamLoader.DecodedAudioInfo info,
        bool streamAssigned, bool playCalled, bool playing, float volumeDb, string bus,
        Vector3? listener, Vector3? source, float? distance, float? maxDistance, string attenuation)
    {
        var busIndex = AudioServer.GetBusIndex(bus);
        var busValid = busIndex >= 0;
        var busMuted = busValid && AudioServer.IsBusMute(busIndex);
        return $"{context}\n" +
               $"path={info.ResolvedPath}\nexists={info.Exists}, bytes={info.FileSizeBytes}, format={info.Format}\n" +
               $"decodedLength={info.StreamLengthSeconds:0.000}s, sampleRate={Value(info.SampleRate)}, channels={Value(info.ChannelCount)}\n" +
               $"streamNonNull={streamAssigned}, playCalled={playCalled}, playingAfterStart={playing}\n" +
               $"volumeDb={volumeDb:0.00}, bus={bus}, busValid={busValid}, masterMuted={MasterMuted()}\n" +
               $"listener={Position(listener)}, source={Position(source)}, distance={Value(distance)}, maxDistance={Value(maxDistance)}\n" +
               $"attenuation={attenuation}";
    }

    private static bool MasterMuted()
    {
        var master = AudioServer.GetBusIndex("Master");
        return master >= 0 && AudioServer.IsBusMute(master);
    }

    private static string Position(Vector3? value) => value.HasValue ? value.Value.ToString() : "n/a";
    private static string Value(int? value) => value?.ToString() ?? "unavailable";
    private static string Value(float? value) => value.HasValue ? $"{value.Value:0.000}" : "n/a";

    private async Task CleanupPlayerAsync(Node player, double duration)
    {
        await ToSignal(GetTree().CreateTimer(Math.Max(0.25, duration + 0.1)), SceneTreeTimer.SignalName.Timeout);
        if (GodotObject.IsInstanceValid(player)) player.QueueFree();
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

    private sealed record ActiveLine(
        DialogueLine Line,
        PlayerPawn Speaker,
        AudioStreamPlayer3D Source,
        LipSyncPlaybackMode LipSyncMode,
        AuthoredDialoguePriority Priority,
        bool Audible,
        bool IsAmbient);

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
