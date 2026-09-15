using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlagFootballStudio.Domain;
using FlagFootballStudio.Tts;
using Godot;

namespace FlagFootballStudio.Presentation;

public readonly record struct CrowdReactionRecord(
    double SimulationTime,
    CrowdReactionKind Reaction,
    SimulationEventType Trigger);

public readonly record struct VenueOneShotRecord(
    double SimulationTime,
    VenueAudioLayer Layer,
    string Sound,
    Vector3 Position);

public readonly record struct AmbientConversationCue(
    double TimeSeconds,
    AmbientConversationContext Context,
    Guid SpeakerPlayerId,
    Guid ListenerPlayerId,
    string Text,
    bool HasConfiguredVoice);

public readonly record struct VenueAudioDiagnosticReport(
    VenueAudioTestKind Kind,
    VenueAudioTestMode Mode,
    bool StreamNonNull,
    double StreamDuration,
    ProceduralAudioMetrics Metrics,
    Vector3? SourcePosition,
    Vector3 ListenerPosition,
    float? Distance,
    float? MaxDistance,
    string Attenuation,
    float VolumeLinear,
    float VolumeDb,
    string Bus,
    bool MasterMuted,
    bool PlayCalled,
    bool Playing,
    bool ExpectedAudible)
{
    public override string ToString() =>
        $"{Kind} / {Mode}: {(ExpectedAudible ? "ENGINE EXPECTED AUDIBLE" : "NOT EXPECTED AUDIBLE")}\n" +
        $"streamNonNull={StreamNonNull}, duration={StreamDuration:0.000}s, samples={Metrics.SampleCount}, " +
        $"nonZero={Metrics.NonZeroSampleCount}, peak={Metrics.PeakAmplitude:0.0000}, rms={Metrics.RmsAmplitude:0.0000}\n" +
        $"source={Position(SourcePosition)}, listener={ListenerPosition}, distance={Value(Distance)}, maxDistance={Value(MaxDistance)}\n" +
        $"attenuation={Attenuation}, volumeLinear={VolumeLinear:0.000}, volumeDb={VolumeDb:0.00}\n" +
        $"bus={Bus}, masterMuted={MasterMuted}, playCalled={PlayCalled}, playingAfterStart={Playing}";

    private static string Position(Vector3? value) => value?.ToString() ?? "n/a (2D non-spatial)";
    private static string Value(float? value) => value.HasValue ? $"{value.Value:0.000}" : "n/a";
}

/// <summary>
/// Presentation-only spatial venue mixer. It consumes simulator frames/events, but cannot write
/// back to the simulator, play definition, game project, or authored dialogue data.
/// </summary>
public partial class VenueAudioController : Node
{
    private static readonly string[] Phrases =
    [
        "I'm open.", "Watch the inside.", "Nice route.",
        "I've got him.", "Good catch.", "Switch."
    ];

    private readonly Dictionary<VenueAudioLayer, float> _currentGains = [];
    private readonly List<RegisteredSource> _sources = [];
    private readonly List<AudioStreamPlayer3D> _oneShots = [];
    private readonly Dictionary<AudioStreamPlayer3D, double> _oneShotRemaining = [];
    private readonly Dictionary<Node, double> _diagnosticRemaining = [];
    private readonly List<CrowdReactionRecord> _reactions = [];
    private readonly List<VenueOneShotRecord> _oneShotHistory = [];
    private readonly Dictionary<Guid, double> _nextFootstepTimes = [];
    private readonly Dictionary<string, ulong> _ambientSkipLogTimes = [];
    private IReadOnlyDictionary<Guid, Node3D> _pawns = new Dictionary<Guid, Node3D>();
    private IReadOnlyDictionary<Guid, PlayerVoiceProfile> _voiceProfiles =
        new Dictionary<Guid, PlayerVoiceProfile>();
    private SpeechGenerationService? _speechGeneration;
    private DialoguePlaybackController? _dialoguePlayback;
    private VenueEnvironment _venue = null!;
    private FootballView _football = null!;
    private Camera3D _listener = null!;
    private IReadOnlyList<AmbientConversationCue> _conversationSchedule = [];
    private int _nextConversationIndex;
    private int _featuredDialogueCount;
    private int _naturalDialogueCount;
    private int _persistentSourceCount;
    private bool _audioCacheAcquired;

    public AmbientAudioSettings Settings { get; private set; } = AmbientAudioSettings.Default;
    public IReadOnlyList<CrowdReactionRecord> ReactionHistory => _reactions;
    public IReadOnlyList<VenueOneShotRecord> OneShotHistory => _oneShotHistory;
    public IReadOnlyList<AmbientConversationCue> ConversationSchedule => _conversationSchedule;
    public int PersistentSpatialSourceCount => _persistentSourceCount;
    public int ActiveOneShotCount => _oneShots.Count(source => GodotObject.IsInstanceValid(source));
    public int PeakConcurrentSpatialSources { get; private set; }
    public int PlayedAmbientConversationCount { get; private set; }
    public int SuppressedAmbientConversationCount { get; private set; }
    public Guid? LastAmbientDialogueLineId { get; private set; }
    public int LastAmbientPlaybackThreadId { get; private set; }
    public string LastAmbientSkippedReason { get; private set; } = "No ambient phrase requested yet.";
    public bool DevelopmentConversationFallbackEnabled { get; set; }
    public Func<AmbientConversationCue, AudioStream?>? ConfiguredVoiceResolver { get; set; }
    public bool AuthoredDialogueActive => _featuredDialogueCount + _naturalDialogueCount > 0;
    public AmbientSpeechCacheDiagnostics AmbientTtsDiagnostics =>
        _speechGeneration?.AmbientDiagnostics ?? default;
    public event Action<string>? AmbientTtsDiagnosticsChanged;
    public Camera3D ListenerCamera => _listener;
    public int PersistentSourcesPlaying => _sources.Take(_persistentSourceCount)
        .Count(source => GodotObject.IsInstanceValid(source.Source) && source.Source.Playing);
    public IReadOnlyList<ulong> PersistentSourceInstanceIds => _sources.Take(_persistentSourceCount)
        .Select(source => source.Source.GetInstanceId()).ToArray();
    public int PersistentLoopingSourceCount => _sources.Take(_persistentSourceCount).Count(source =>
        source.Source.Stream is AudioStreamWav wav && wav.LoopMode != AudioStreamWav.LoopModeEnum.Disabled);
    public IReadOnlyList<Vector3> PersistentSourcePositions => _sources.Take(_persistentSourceCount)
        .Select(source => source.Source.GlobalPosition).ToArray();

    public override void _Process(double delta)
    {
        AdvanceMix(delta);
        UpdateOneShots(delta);
        UpdateDiagnosticPlayers(delta);
        ApplyDebugBoostPlacement();
    }

    public override void _ExitTree()
    {
        Shutdown();
    }

    public void StopAll() => StopAllSources();

    public void Shutdown()
    {
        StopAllSources();
        if (!_audioCacheAcquired) return;
        ProceduralVenueAudio.Release();
        _audioCacheAcquired = false;
    }

    public void Configure(
        VenueEnvironment venue,
        IReadOnlyDictionary<Guid, Node3D> pawns,
        FootballView football,
        Camera3D listener,
        IReadOnlyDictionary<Guid, PlayerVoiceProfile>? voiceProfiles = null,
        SpeechGenerationService? speechGeneration = null,
        DialoguePlaybackController? dialoguePlayback = null)
    {
        StopAllSources();
        if (!_audioCacheAcquired)
        {
            ProceduralVenueAudio.Acquire();
            _audioCacheAcquired = true;
        }
        _venue = venue ?? throw new ArgumentNullException(nameof(venue));
        _pawns = pawns ?? throw new ArgumentNullException(nameof(pawns));
        _football = football ?? throw new ArgumentNullException(nameof(football));
        _listener = listener ?? throw new ArgumentNullException(nameof(listener));
        _voiceProfiles = voiceProfiles ?? new Dictionary<Guid, PlayerVoiceProfile>();
        _speechGeneration = speechGeneration;
        _dialoguePlayback = dialoguePlayback;
        foreach (var layer in Enum.GetValues<VenueAudioLayer>())
            _currentGains[layer] = TargetGain(layer);
        BuildPersistentLayers();
        _persistentSourceCount = _sources.Count;
    }

    public void ApplySettings(AmbientAudioSettings settings)
    {
        Settings = settings.Validated();
        ApplyDebugBoostPlacement();
        UpdateSourceVolumes();
    }

    public void BeginPlay(PlaySimulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        _reactions.Clear();
        _oneShotHistory.Clear();
        _nextFootstepTimes.Clear();
        PlayedAmbientConversationCount = 0;
        SuppressedAmbientConversationCount = 0;
        LastAmbientDialogueLineId = null;
        LastAmbientSkippedReason = "Waiting for the next scheduled ambient phrase.";
        _conversationSchedule = BuildConversationSchedule(simulation, _pawns, _voiceProfiles);
        _nextConversationIndex = 0;
    }

    public void HandleSimulationFrame(SimulationFrame frame)
    {
        while (_nextConversationIndex < _conversationSchedule.Count &&
               _conversationSchedule[_nextConversationIndex].TimeSeconds <= frame.TimeSeconds)
            TryPlayAmbientConversation(_conversationSchedule[_nextConversationIndex++]);

        foreach (var state in frame.Players.Values)
        {
            if (state.Speed < 0.75f || state.MotionState is SimulationMotionState.Idle or
                SimulationMotionState.Throw or SimulationMotionState.Catch)
                continue;
            var interval = Math.Clamp(0.62 - state.Speed * 0.045, 0.27, 0.58);
            if (_nextFootstepTimes.TryGetValue(state.PlayerId, out var next) && frame.TimeSeconds + 0.0001 < next)
                continue;
            _nextFootstepTimes[state.PlayerId] = frame.TimeSeconds + interval;
            if (_pawns.TryGetValue(state.PlayerId, out var pawn))
                PlayOneShot(pawn, VenueAudioLayer.ActionSfx, ProceduralVenueAudio.Action("footstep"),
                    0.22f, 16, frame.TimeSeconds, "Footstep");
        }
    }

    public void HandleSimulationEvent(SimulationEvent simulationEvent)
    {
        var anchor = AnchorFor(simulationEvent.PlayerId) ?? _football;
        switch (simulationEvent.Type)
        {
            case SimulationEventType.SnapStarted:
                PlayOneShot(_football, VenueAudioLayer.ActionSfx, ProceduralVenueAudio.Action("catch"),
                    0.38f, 18, simulationEvent.TimeSeconds, "Snap impact");
                break;
            case SimulationEventType.ThrowReleased:
                PlayOneShot(anchor, VenueAudioLayer.ActionSfx, ProceduralVenueAudio.Action("throw"),
                    0.55f, 24, simulationEvent.TimeSeconds, "Throw release");
                break;
            case SimulationEventType.PassCompleted:
                PlayImpactAndReaction(anchor, simulationEvent, CrowdReactionKind.Cheer, "Catch impact");
                break;
            case SimulationEventType.PassDropped:
                PlayImpactAndReaction(anchor, simulationEvent, CrowdReactionKind.Disappointment, "Dropped-pass impact");
                break;
            case SimulationEventType.PassIncomplete:
                TriggerCrowdReaction(simulationEvent, CrowdReactionKind.Disappointment);
                break;
            case SimulationEventType.Intercepted:
                PlayImpactAndReaction(anchor, simulationEvent, CrowdReactionKind.Interception, "Interception catch");
                break;
            case SimulationEventType.FlagPullAttempted:
                PlayOneShot(anchor, VenueAudioLayer.ActionSfx, ProceduralVenueAudio.Action("flag"),
                    0.48f, 16, simulationEvent.TimeSeconds, "Flag/equipment movement");
                break;
            case SimulationEventType.FlagPulled:
                PlayWhistle(simulationEvent.TimeSeconds, "Flag-pull whistle");
                TriggerCrowdReaction(simulationEvent, CrowdReactionKind.Moderate);
                break;
            case SimulationEventType.OutOfBounds:
                PlayWhistle(simulationEvent.TimeSeconds, "Boundary whistle");
                TriggerCrowdReaction(simulationEvent, CrowdReactionKind.Moderate);
                break;
            case SimulationEventType.Touchdown:
                PlayWhistle(simulationEvent.TimeSeconds, "Touchdown whistle");
                TriggerCrowdReaction(simulationEvent, CrowdReactionKind.TouchdownCelebration);
                break;
        }
    }

    public void SetAuthoredDialogueActivity(AuthoredDialoguePriority priority, bool active)
    {
        if (active) _dialoguePlayback?.StopAmbientLines();
        if (priority == AuthoredDialoguePriority.Featured)
            _featuredDialogueCount = Math.Max(0, _featuredDialogueCount + (active ? 1 : -1));
        else
            _naturalDialogueCount = Math.Max(0, _naturalDialogueCount + (active ? 1 : -1));
    }

    public void AdvanceMix(double delta)
    {
        var weight = 1f - MathF.Exp(-(float)Math.Max(0, delta) / 0.18f);
        foreach (var layer in Enum.GetValues<VenueAudioLayer>())
        {
            var current = _currentGains.GetValueOrDefault(layer, TargetGain(layer));
            _currentGains[layer] = Mathf.Lerp(current, TargetGain(layer), weight);
        }
        UpdateSourceVolumes();
    }

    public float CurrentLayerGain(VenueAudioLayer layer) => _currentGains.GetValueOrDefault(layer, TargetGain(layer));
    public float TargetLayerGain(VenueAudioLayer layer) => TargetGain(layer);

    public static float EstimateSpatialAudibility(Vector3 listener, Vector3 source, float maxDistance)
    {
        if (maxDistance <= 0) return 0;
        var distance = listener.DistanceTo(source);
        if (distance >= maxDistance) return 0;
        var normalized = 1 - distance / maxDistance;
        return normalized * normalized;
    }

    public static float EstimateInverseDistanceGain(float distance, float unitSize, float maxDistance)
    {
        if (distance >= maxDistance) return 0;
        return distance <= unitSize ? 1 : unitSize / Math.Max(unitSize, distance);
    }

    public async Task<VenueAudioDiagnosticReport> TestAsync(
        VenueAudioTestKind kind,
        VenueAudioTestMode mode)
    {
        var spec = DiagnosticSpecFor(kind);
        var metrics = spec.Stream is AudioStreamWav wav
            ? ProceduralVenueAudio.Inspect(wav)
            : default;
        var listenerPosition = _listener.GlobalPosition;
        var bus = "Master";
        var playCalled = false;
        var volumeLinear = mode == VenueAudioTestMode.ProductionSpatial
            ? Math.Max(0.001f, spec.BaseGain * CurrentLayerGain(spec.Layer))
            : 1;
        var volumeDb = ToDb(volumeLinear);
        Vector3? sourcePosition = null;
        float? distance = null;
        float? maxDistance = null;
        string attenuation;
        bool playing;

        if (mode == VenueAudioTestMode.Raw2D)
        {
            var raw = new AudioStreamPlayer
            {
                Name = $"Diagnostic_{kind}_Raw2D",
                Stream = spec.Stream,
                Bus = bus,
                VolumeDb = volumeDb
            };
            AddChild(raw);
            raw.Play();
            playCalled = true;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            playing = raw.Playing;
            attenuation = "2D non-spatial; attenuation bypassed";
            _diagnosticRemaining[raw] = DiagnosticLifetime(spec.Stream);
        }
        else
        {
            var forcedNear = mode == VenueAudioTestMode.ForcedNearSpatial;
            var spatial = NewSpatialSource(spec.Stream,
                forcedNear ? 100 : spec.MaxDistance,
                forcedNear ? 1 : spec.UnitSize,
                forcedNear ? AudioStreamPlayer3D.AttenuationModelEnum.Disabled :
                    AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance);
            spatial.Name = $"Diagnostic_{kind}_{mode}";
            spatial.VolumeDb = volumeDb;
            if (forcedNear)
            {
                AddChild(spatial);
                spatial.GlobalPosition = listenerPosition - _listener.GlobalBasis.Z.Normalized();
            }
            else
            {
                spec.Anchor.AddChild(spatial);
                spatial.Position = Vector3.Zero;
            }
            sourcePosition = spatial.GlobalPosition;
            distance = listenerPosition.DistanceTo(sourcePosition.Value);
            maxDistance = spatial.MaxDistance;
            spatial.Play();
            playCalled = true;
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            playing = spatial.Playing;
            attenuation = $"model={spatial.AttenuationModel}, unitSize={spatial.UnitSize:0.00}, filter={spatial.AttenuationFilterDb:0.00} dB";
            _diagnosticRemaining[spatial] = DiagnosticLifetime(spec.Stream);
        }

        var masterIndex = AudioServer.GetBusIndex("Master");
        var masterMuted = masterIndex >= 0 && AudioServer.IsBusMute(masterIndex);
        var spatialGain = mode switch
        {
            VenueAudioTestMode.Raw2D => 1,
            VenueAudioTestMode.ForcedNearSpatial => 1,
            _ => EstimateInverseDistanceGain(distance ?? 0, spec.UnitSize, spec.MaxDistance)
        };
        var expectedAudible = spec.Stream is not null && metrics.SampleCount > 0 &&
            metrics.NonZeroSampleCount > 0 && metrics.RmsAmplitude * volumeLinear * spatialGain >= 0.0035f &&
            !masterMuted && playing;
        var report = new VenueAudioDiagnosticReport(kind, mode, spec.Stream is not null,
            spec.Stream!.GetLength(), metrics, sourcePosition, listenerPosition, distance, maxDistance,
            attenuation, volumeLinear, volumeDb, bus, masterMuted, playCalled, playing, expectedAudible);
        GD.Print(report.ToString());
        return report;
    }

    public static IReadOnlyList<AmbientConversationCue> BuildConversationSchedule(
        PlaySimulation simulation,
        IReadOnlyDictionary<Guid, Node3D> pawns,
        IReadOnlyDictionary<Guid, PlayerVoiceProfile> profiles)
    {
        var firstFrame = simulation.Frames[0];
        var candidates = firstFrame.Players.Values.OrderBy(state => state.PlayerId).ToArray();
        var cues = new List<AmbientConversationCue>();
        var cueTimes = new[]
        {
            (0.12, AmbientConversationContext.PrePlay),
            (0.48, AmbientConversationContext.Huddle),
            (Math.Max(0.2, simulation.DurationSeconds * 0.42), AmbientConversationContext.Sideline),
            (Math.Max(0.2, simulation.DurationSeconds - 0.04), AmbientConversationContext.PostPlay)
        };
        for (var index = 0; index < cueTimes.Length; index++)
        {
            var speaker = candidates[index % candidates.Length];
            var teammate = candidates
                .Where(candidate => candidate.PlayerId != speaker.PlayerId && SameTeam(candidate.PlayerId, speaker.PlayerId, pawns))
                .OrderBy(candidate => candidate.Position.DistanceTo(speaker.Position))
                .FirstOrDefault(candidate => candidate.Position.DistanceTo(speaker.Position) <= 8);
            if (teammate is null) continue;
            var hasVoice = profiles.TryGetValue(speaker.PlayerId, out var profile) &&
                           profile.BackendType != TtsBackendType.None &&
                           !string.IsNullOrWhiteSpace(profile.ModelIdOrPath);
            cues.Add(new AmbientConversationCue(
                Math.Min(cueTimes[index].Item1, simulation.DurationSeconds), cueTimes[index].Item2,
                speaker.PlayerId, teammate.PlayerId, Phrases[index % Phrases.Length], hasVoice));
        }
        return cues.OrderBy(cue => cue.TimeSeconds).ThenBy(cue => cue.SpeakerPlayerId).ToArray();
    }

    private void BuildPersistentLayers()
    {
        RegisterLoop(_venue.AudioAnchors[VenueAudioHookKind.CrowdAmbience], VenueAudioLayer.CrowdAmbience,
            ProceduralVenueAudio.CrowdLoop(0), 0.7f, 58, 14);
        RegisterLoop(_venue.AudioAnchors[VenueAudioHookKind.OppositeCrowdAmbience], VenueAudioLayer.CrowdAmbience,
            ProceduralVenueAudio.CrowdLoop(1), 0.65f, 58, 14);
        RegisterLoop(_venue.AudioAnchors[VenueAudioHookKind.GoldSidelineChatter], VenueAudioLayer.SidelineChatter,
            ProceduralVenueAudio.ChatterLoop(0), 0.52f, 24, 6);
        RegisterLoop(_venue.AudioAnchors[VenueAudioHookKind.GoldSidelineChatter], VenueAudioLayer.SidelineChatter,
            ProceduralVenueAudio.ChatterLoop(1), 0.36f, 22, 6);
        RegisterLoop(_venue.AudioAnchors[VenueAudioHookKind.NavySidelineChatter], VenueAudioLayer.SidelineChatter,
            ProceduralVenueAudio.ChatterLoop(2), 0.52f, 24, 6);
        RegisterLoop(_venue.AudioAnchors[VenueAudioHookKind.NavySidelineChatter], VenueAudioLayer.SidelineChatter,
            ProceduralVenueAudio.ChatterLoop(3), 0.36f, 22, 6);
        RegisterLoop(_venue.AudioAnchors[VenueAudioHookKind.GoldBenchChatter], VenueAudioLayer.PlayerChatter,
            ProceduralVenueAudio.ChatterLoop(4), 0.38f, 18, 4);
        RegisterLoop(_venue.AudioAnchors[VenueAudioHookKind.NavyBenchChatter], VenueAudioLayer.PlayerChatter,
            ProceduralVenueAudio.ChatterLoop(5), 0.38f, 18, 4);
        PeakConcurrentSpatialSources = _sources.Count;
    }

    private void RegisterLoop(Node3D parent, VenueAudioLayer layer, AudioStream stream, float baseGain,
        float maxDistance, float unitSize)
    {
        var source = NewSpatialSource(stream, maxDistance, unitSize,
            AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance);
        parent.AddChild(source);
        source.Play();
        _sources.Add(new RegisteredSource(source, layer, baseGain, true, maxDistance, unitSize));
    }

    private void PlayImpactAndReaction(Node3D anchor, SimulationEvent simulationEvent,
        CrowdReactionKind reaction, string name)
    {
        PlayOneShot(anchor, VenueAudioLayer.ActionSfx, ProceduralVenueAudio.Action("catch"),
            0.62f, 22, simulationEvent.TimeSeconds, name);
        TriggerCrowdReaction(simulationEvent, reaction);
    }

    private void PlayWhistle(double time, string name) =>
        PlayOneShot(_venue.AudioAnchors[VenueAudioHookKind.Whistle], VenueAudioLayer.ActionSfx,
            ProceduralVenueAudio.Action("whistle"), 0.72f, 42, time, name);

    private void TriggerCrowdReaction(SimulationEvent simulationEvent, CrowdReactionKind reaction)
    {
        if (!Settings.CrowdReactionsEnabled) return;
        _reactions.Add(new CrowdReactionRecord(simulationEvent.TimeSeconds, reaction, simulationEvent.Type));
        var stream = ProceduralVenueAudio.Reaction(reaction);
        PlayOneShot(_venue.AudioAnchors[VenueAudioHookKind.CrowdAmbience], VenueAudioLayer.CrowdAmbience,
            stream, ReactionGain(reaction), 65, simulationEvent.TimeSeconds, $"Crowd {reaction}");
        PlayOneShot(_venue.AudioAnchors[VenueAudioHookKind.OppositeCrowdAmbience], VenueAudioLayer.CrowdAmbience,
            stream, ReactionGain(reaction) * 0.85f, 65, simulationEvent.TimeSeconds, $"Opposite crowd {reaction}");
    }

    private void TryPlayAmbientConversation(AmbientConversationCue cue)
    {
        if (!Settings.AmbientConversationsEnabled)
        {
            SkipAmbient("Ambient conversations are disabled.", cue);
            return;
        }
        if (AuthoredDialogueActive || (_dialoguePlayback?.ActiveAuthoredLineCount ?? 0) > 0)
        {
            SkipAmbient("Authored dialogue has priority.", cue);
            return;
        }
        if ((_dialoguePlayback?.ActiveAmbientLineCount ?? 0) > 0)
        {
            SkipAmbient("Another ambient player phrase is still playing.", cue);
            return;
        }
        if (!_pawns.TryGetValue(cue.SpeakerPlayerId, out var speaker) ||
            !_pawns.TryGetValue(cue.ListenerPlayerId, out var listener) ||
            speaker.GlobalPosition.DistanceTo(listener.GlobalPosition) > 8)
        {
            SkipAmbient("speaker/listener unavailable or outside the 8 m conversation range", cue, "proximity");
            return;
        }

        if (CurrentLayerGain(VenueAudioLayer.PlayerChatter) <= 0.0001f)
        {
            SkipAmbient("player chatter volume is muted", cue);
            return;
        }

        if (cue.HasConfiguredVoice && _speechGeneration is not null && _dialoguePlayback is not null &&
            _voiceProfiles.TryGetValue(cue.SpeakerPlayerId, out var profile))
        {
            var resolution = _speechGeneration.ResolveOrQueueAmbient(profile, cue.Text);
            if (resolution.State == AmbientSpeechResolutionState.Ready && resolution.Clip is not null)
            {
                StartGeneratedAmbientConversation(cue, resolution.Clip);
                return;
            }
            var reason = resolution.State == AmbientSpeechResolutionState.MissingVoiceConfiguration
                ? "no configured local TTS voice"
                : resolution.Reason ?? $"Ambient TTS {resolution.State}.";
            SkipAmbient(reason, cue, resolution.State.ToString());
            if (!DevelopmentConversationFallbackEnabled) return;
        }

        AudioStream? stream = cue.HasConfiguredVoice ? ConfiguredVoiceResolver?.Invoke(cue) : null;
        if (stream is null && DevelopmentConversationFallbackEnabled)
            stream = ProceduralVenueAudio.ConversationFallback(PlayedAmbientConversationCount % 4);
        if (stream is null)
        {
            // Text remains a deterministic presentation cue; no voice is fabricated when none is configured.
            SkipAmbient(cue.HasConfiguredVoice
                ? "Configured voice audio is not ready; no generic voice was substituted."
                : "no configured local TTS voice", cue, "missing-voice");
            return;
        }
        var anchor = speaker is PlayerPawn pawn ? pawn.MouthAudioAnchor : speaker;
        PlayOneShot(anchor, VenueAudioLayer.PlayerChatter, stream, 0.42f, 8,
            cue.TimeSeconds, $"Ambient: {cue.Text}");
        if (speaker is PlayerPawn speakingPawn)
            speakingPawn.StartSpeechShapeCycle(0.13f);
        PlayedAmbientConversationCount++;
        LastAmbientSkippedReason = "Development fallback played explicitly.";
        PublishAmbientDiagnostics();
    }

    private void StartGeneratedAmbientConversation(AmbientConversationCue cue, AmbientSpeechClip clip)
    {
        if (_dialoguePlayback is null || !_pawns.TryGetValue(cue.SpeakerPlayerId, out var node) ||
            node is not PlayerPawn)
        {
            SkipAmbient("speaking player was removed before cached speech could play", cue);
            return;
        }
        try
        {
            var lineId = Guid.NewGuid();
            var line = new DialogueLine(lineId, cue.SpeakerPlayerId, 0, clip.DurationSeconds,
                cue.Text, Math.Clamp(0.72f * CurrentLayerGain(VenueAudioLayer.PlayerChatter), 0, 1),
                SpeechStyle.Normal, 10, cue.ListenerPlayerId, null,
                DialogueGazeTargetKind.ListenerPlayer, audioReference: clip.AudioReference,
                lipSyncEvents: clip.LipSyncEvents, lipSyncSource: clip.LipSyncSource);
            LastAmbientDialogueLineId = lineId;
            LastAmbientPlaybackThreadId = System.Environment.CurrentManagedThreadId;
            var playback = _dialoguePlayback.PlayAmbientLineAsync(line);
            if (_dialoguePlayback.AmbientSourceFor(lineId) is null)
            {
                SkipAmbient("ambient playback yielded to authored or already-playing speech", cue);
                return;
            }
            PlayedAmbientConversationCount++;
            LastAmbientSkippedReason = $"Playing cached {clip.LipSyncSource} speech for {cue.SpeakerPlayerId}.";
            PublishAmbientDiagnostics();
            _ = ObserveAmbientPlaybackAsync(playback, cue.Text);
        }
        catch (Exception exception)
        {
            _speechGeneration?.InvalidateAmbientClip(clip.CacheKey);
            GD.PushWarning($"Ambient TTS skipped for '{cue.Text}': {exception.Message}");
            SkipAmbient($"ambient audio load/play failed: {exception.Message}", cue, "playback-failure");
        }
    }

    private async Task ObserveAmbientPlaybackAsync(Task playback, string phrase)
    {
        try { await playback; }
        catch (Exception exception)
        {
            GD.PushWarning($"Ambient TTS playback failed for '{phrase}': {exception.Message}");
            LastAmbientSkippedReason = $"Ambient playback failed: {exception.Message}";
            PublishAmbientDiagnostics();
        }
    }

    private void SkipAmbient(string reason, AmbientConversationCue? cue = null, string? category = null)
    {
        SuppressedAmbientConversationCount++;
        var playerLabel = cue.HasValue ? AmbientPlayerLabel(cue.Value.SpeakerPlayerId) : null;
        LastAmbientSkippedReason = playerLabel is null ? reason : $"{playerLabel}: {reason}";
        var key = $"{cue?.SpeakerPlayerId}:{category ?? reason}";
        var now = Time.GetTicksMsec();
        if (!_ambientSkipLogTimes.TryGetValue(key, out var last) || now - last >= 5000)
        {
            _ambientSkipLogTimes[key] = now;
            GD.Print(playerLabel is null
                ? $"Ambient conversation skipped: {reason}"
                : $"Ambient conversation skipped for {playerLabel}: {reason}.");
        }
        PublishAmbientDiagnostics();
    }

    private string AmbientPlayerLabel(Guid playerId)
    {
        if (_pawns.TryGetValue(playerId, out var node) && node is PlayerPawn { Player: { } player })
            return $"{player.Team?.Name} #{player.JerseyNumber} {player.Name} ({player.Id})";
        return playerId.ToString();
    }

    private void PublishAmbientDiagnostics()
    {
        var diagnostics = AmbientTtsDiagnostics;
        AmbientTtsDiagnosticsChanged?.Invoke(
            $"Ambient TTS — hits {diagnostics.Hits}, misses {diagnostics.Misses}, " +
            $"pending {diagnostics.Pending}/{diagnostics.MaximumPending}; {LastAmbientSkippedReason}");
    }

    private void PlayOneShot(Node3D parent, VenueAudioLayer layer, AudioStream stream,
        float baseGain, float maxDistance, double simulationTime, string name)
    {
        var source = NewSpatialSource(stream, maxDistance, 4,
            AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance);
        source.Name = name.Replace(' ', '_');
        parent.AddChild(source);
        source.VolumeDb = ToDb(baseGain * CurrentLayerGain(layer));
        source.Play();
        _sources.Add(new RegisteredSource(source, layer, baseGain, false, maxDistance, 4));
        _oneShots.Add(source);
        _oneShotHistory.Add(new VenueOneShotRecord(simulationTime, layer, name, source.GlobalPosition));
        PeakConcurrentSpatialSources = Math.Max(PeakConcurrentSpatialSources,
            _sources.Count(item => GodotObject.IsInstanceValid(item.Source)));
        _oneShotRemaining[source] = Math.Max(0.12, stream.GetLength() + 0.08);
    }

    private void UpdateOneShots(double delta)
    {
        foreach (var source in _oneShotRemaining.Keys.ToArray())
        {
            var remaining = _oneShotRemaining[source] - Math.Max(0, delta);
            if (remaining > 0)
            {
                _oneShotRemaining[source] = remaining;
                continue;
            }
            _oneShotRemaining.Remove(source);
            _sources.RemoveAll(item => item.Source == source);
            _oneShots.Remove(source);
            if (GodotObject.IsInstanceValid(source))
            {
                source.Stop();
                source.Stream = null;
                source.QueueFree();
            }
        }
    }

    private void UpdateDiagnosticPlayers(double delta)
    {
        foreach (var player in _diagnosticRemaining.Keys.ToArray())
        {
            var remaining = _diagnosticRemaining[player] - Math.Max(0, delta);
            if (remaining > 0)
            {
                _diagnosticRemaining[player] = remaining;
                continue;
            }
            _diagnosticRemaining.Remove(player);
            if (!GodotObject.IsInstanceValid(player)) continue;
            if (player is AudioStreamPlayer raw) raw.Stop();
            if (player is AudioStreamPlayer3D spatial) spatial.Stop();
            player.QueueFree();
        }
    }

    private AudioStreamPlayer3D NewSpatialSource(AudioStream stream, float maxDistance, float unitSize,
        AudioStreamPlayer3D.AttenuationModelEnum attenuationModel) => new()
    {
        Stream = stream,
        Bus = "Master",
        UnitSize = unitSize,
        MaxDistance = maxDistance,
        AttenuationModel = attenuationModel,
        AttenuationFilterDb = -14,
        Autoplay = false
    };

    private Node3D? AnchorFor(Guid? playerId) => playerId.HasValue &&
        _pawns.TryGetValue(playerId.Value, out var pawn) ? pawn : null;

    private float TargetGain(VenueAudioLayer layer)
    {
        var configured = layer switch
        {
            VenueAudioLayer.CrowdAmbience => Settings.CrowdVolume,
            VenueAudioLayer.SidelineChatter => Settings.SidelineChatterVolume,
            VenueAudioLayer.PlayerChatter => Settings.PlayerChatterVolume,
            _ => Settings.ActionSfxVolume
        };
        if (Settings.DebugAmbienceBoost && layer != VenueAudioLayer.ActionSfx)
            configured = Math.Max(configured, 0.9f);
        var duck = _featuredDialogueCount > 0
            ? layer switch
            {
                VenueAudioLayer.CrowdAmbience => 0.42f,
                VenueAudioLayer.SidelineChatter => 0.35f,
                VenueAudioLayer.PlayerChatter => 0.25f,
                _ => 0.92f
            }
            : _naturalDialogueCount > 0
                ? layer switch
                {
                    VenueAudioLayer.CrowdAmbience => 0.7f,
                    VenueAudioLayer.SidelineChatter => 0.58f,
                    VenueAudioLayer.PlayerChatter => 0.35f,
                    _ => 1f
                }
                : 1f;
        return configured * duck;
    }

    private void UpdateSourceVolumes()
    {
        foreach (var source in _sources)
            if (GodotObject.IsInstanceValid(source.Source))
            {
                var baseGain = Settings.DebugAmbienceBoost && source.IsPersistent
                    ? Math.Max(source.BaseGain, 0.9f)
                    : source.BaseGain;
                source.Source.VolumeDb = ToDb(baseGain * CurrentLayerGain(source.Layer));
            }
    }

    private void ApplyDebugBoostPlacement()
    {
        if (_listener is null) return;
        var persistent = _sources.Take(_persistentSourceCount).ToArray();
        for (var index = 0; index < persistent.Length; index++)
        {
            var registered = persistent[index];
            if (!GodotObject.IsInstanceValid(registered.Source)) continue;
            if (Settings.DebugAmbienceBoost)
            {
                registered.Source.AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.Disabled;
                registered.Source.MaxDistance = 100;
                registered.Source.GlobalPosition = _listener.GlobalPosition - _listener.GlobalBasis.Z.Normalized() *
                    (1.5f + index * 0.08f);
            }
            else
            {
                registered.Source.Position = Vector3.Zero;
                registered.Source.AttenuationModel = AudioStreamPlayer3D.AttenuationModelEnum.InverseDistance;
                registered.Source.MaxDistance = registered.ProductionMaxDistance;
                registered.Source.UnitSize = registered.ProductionUnitSize;
            }
        }
    }

    private void StopAllSources()
    {
        foreach (var registered in _sources)
            if (GodotObject.IsInstanceValid(registered.Source))
            {
                registered.Source.Stop();
                registered.Source.Stream = null;
                registered.Source.Free();
            }
        _sources.Clear();
        _oneShots.Clear();
        _oneShotRemaining.Clear();
        foreach (var diagnostic in _diagnosticRemaining.Keys)
            if (GodotObject.IsInstanceValid(diagnostic)) diagnostic.Free();
        _diagnosticRemaining.Clear();
        _reactions.Clear();
        _oneShotHistory.Clear();
        _persistentSourceCount = 0;
        _featuredDialogueCount = 0;
        _naturalDialogueCount = 0;
        PeakConcurrentSpatialSources = 0;
    }

    private static bool SameTeam(Guid first, Guid second, IReadOnlyDictionary<Guid, Node3D> pawns) =>
        pawns.TryGetValue(first, out var firstNode) && firstNode is PlayerPawn firstPawn &&
        pawns.TryGetValue(second, out var secondNode) && secondNode is PlayerPawn secondPawn &&
        firstPawn.Player?.Team?.Id == secondPawn.Player?.Team?.Id;

    private static float ReactionGain(CrowdReactionKind reaction) => reaction switch
    {
        CrowdReactionKind.TouchdownCelebration => 0.9f,
        CrowdReactionKind.Interception => 0.8f,
        CrowdReactionKind.Cheer => 0.65f,
        CrowdReactionKind.Disappointment => 0.52f,
        _ => 0.45f
    };

    private static float ToDb(float linear) => linear <= 0.0001f ? -80 : Mathf.LinearToDb(linear);

    private DiagnosticSpec DiagnosticSpecFor(VenueAudioTestKind kind) => kind switch
    {
        VenueAudioTestKind.Crowd => new(ProceduralVenueAudio.CrowdLoop(0),
            _venue.AudioAnchors[VenueAudioHookKind.CrowdAmbience], VenueAudioLayer.CrowdAmbience, 0.7f, 58, 14),
        VenueAudioTestKind.Sideline => new(ProceduralVenueAudio.ChatterLoop(0),
            _venue.AudioAnchors[VenueAudioHookKind.GoldSidelineChatter], VenueAudioLayer.SidelineChatter, 0.52f, 24, 6),
        VenueAudioTestKind.PlayerChatterBed => new(ProceduralVenueAudio.ChatterLoop(4),
            _venue.AudioAnchors[VenueAudioHookKind.GoldBenchChatter], VenueAudioLayer.PlayerChatter, 0.38f, 18, 4),
        VenueAudioTestKind.Footstep => new(ProceduralVenueAudio.Action("footstep"),
            _venue.AudioAnchors[VenueAudioHookKind.Footsteps], VenueAudioLayer.ActionSfx, 0.8f, 20, 4),
        VenueAudioTestKind.CatchImpact => new(ProceduralVenueAudio.Action("catch"),
            _venue.AudioAnchors[VenueAudioHookKind.CatchThrowImpacts], VenueAudioLayer.ActionSfx, 0.85f, 24, 5),
        VenueAudioTestKind.Whistle => new(ProceduralVenueAudio.Action("whistle"),
            _venue.AudioAnchors[VenueAudioHookKind.Whistle], VenueAudioLayer.ActionSfx, 0.85f, 42, 8),
        _ => new(ProceduralVenueAudio.Reaction(CrowdReactionKind.Cheer),
            _venue.AudioAnchors[VenueAudioHookKind.CelebrationReactions], VenueAudioLayer.CrowdAmbience, 0.85f, 65, 14)
    };

    private static double DiagnosticLifetime(AudioStream stream) =>
        Math.Clamp(stream.GetLength() + 0.15, 0.4, 3.5);

    private readonly record struct RegisteredSource(
        AudioStreamPlayer3D Source,
        VenueAudioLayer Layer,
        float BaseGain,
        bool IsPersistent,
        float ProductionMaxDistance,
        float ProductionUnitSize);

    private readonly record struct DiagnosticSpec(
        AudioStream Stream,
        Node3D Anchor,
        VenueAudioLayer Layer,
        float BaseGain,
        float MaxDistance,
        float UnitSize);
}
