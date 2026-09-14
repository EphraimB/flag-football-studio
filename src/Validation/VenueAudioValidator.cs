using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlagFootballStudio.Domain;
using Godot;

namespace FlagFootballStudio.Presentation;

public partial class VenueAudioValidator : Node3D
{
    public async Task RunAsync()
    {
        var game = Game.CreatePrototype();
        var project = GameProject.CreatePrototype(game);
        var play = project.Plays[0];
        var simulator = new FootballPlaySimulator();
        var simulation = simulator.Simulate(play, game.Gold, game.Navy);
        var baseline = Snapshot(simulation);

        var venue = new VenueEnvironment { Name = "AudioValidationVenue" };
        AddChild(venue);
        var football = new FootballView { Name = "AudioValidationFootball" };
        AddChild(football);
        var camera = new Camera3D { Name = "AudioValidationListener", Current = true };
        AddChild(camera);
        var pawns = SpawnPlayers(game, project, play);
        var audio = new VenueAudioController { Name = "AudioValidationController" };
        AddChild(audio);
        audio.Configure(venue, pawns, football, camera, project.PlayerVoiceProfiles);
        audio.ApplySettings(AmbientAudioSettings.Default);

        Require(audio.PersistentSpatialSourceCount >= 8,
            "Crowd, sideline, and bench layers did not create concurrent spatial sources.");
        await ValidateRuntimeDiagnostics(audio, venue, camera);
        audio.ApplySettings(new AmbientAudioSettings(1, 1, 1, 1, true, true));
        ValidateDeterministicConversations(simulation, pawns, project.PlayerVoiceProfiles);
        ValidateListenerDistance(venue, camera);
        ValidateDucking(audio);
        ValidateEventReactions(audio, simulation);
        ValidateOutcomeReactions(audio, simulator, game);
        await ValidateDialogueIntegration(audio, pawns, football, camera);
        ValidateConversationPriority(audio, simulation);

        var after = Snapshot(simulator.Simulate(play, game.Gold, game.Navy));
        Require(after == baseline, "Ambient venue audio changed simulation frames, timestamps, or outcome.");
        Require(audio.PeakConcurrentSpatialSources > audio.PersistentSpatialSourceCount - audio.ActiveOneShotCount,
            "Action and reaction one-shots did not overlap persistent spatial ambience.");
        Require(ProceduralVenueAudio.CachedStreamCount <= 24,
            "Procedural audio allocated an unbounded number of stream resources.");
        audio.Shutdown();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree().CreateTimer(0.05), SceneTreeTimer.SignalName.Timeout);
    }

    private async Task ValidateRuntimeDiagnostics(
        VenueAudioController audio,
        VenueEnvironment venue,
        Camera3D camera)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Require(audio.PersistentSourcesPlaying == audio.PersistentSpatialSourceCount,
            "Ambient beds were not playing after scene/controller readiness.");
        Require(audio.PersistentLoopingSourceCount == audio.PersistentSpatialSourceCount,
            "One or more intended ambient beds were not configured to loop.");
        var sourceIds = audio.PersistentSourceInstanceIds.ToArray();

        camera.GlobalPosition = new Vector3(15, 19, 24);
        var rawCrowd = await audio.TestAsync(VenueAudioTestKind.Crowd, VenueAudioTestMode.Raw2D);
        Require(rawCrowd.StreamNonNull && rawCrowd.Metrics.SampleCount > 0 &&
                rawCrowd.Metrics.NonZeroSampleCount > 0 && rawCrowd.Metrics.PeakAmplitude >= 0.16f &&
                rawCrowd.Metrics.RmsAmplitude >= 0.045f && rawCrowd.ExpectedAudible,
            "Raw crowd PCM is empty, silent, or below a reasonable audible threshold.");
        var forcedCrowd = await audio.TestAsync(VenueAudioTestKind.Crowd, VenueAudioTestMode.ForcedNearSpatial);
        Require(forcedCrowd.Distance is >= 0.99f and <= 1.01f && forcedCrowd.ExpectedAudible,
            "Forced-near crowd playback was not active and audible one meter from the listener.");
        var productionCrowd = await audio.TestAsync(VenueAudioTestKind.Crowd, VenueAudioTestMode.ProductionSpatial);
        Require(productionCrowd.Playing && productionCrowd.ExpectedAudible,
            "Normal crowd playback remained below the audible threshold at Broadcast Wide.");

        var distance = productionCrowd.Distance ?? 0;
        var oldUnitGain = VenueAudioController.EstimateInverseDistanceGain(distance, 1,
            productionCrowd.MaxDistance ?? 58);
        var correctedGain = VenueAudioController.EstimateInverseDistanceGain(distance, 14,
            productionCrowd.MaxDistance ?? 58);
        var oldEstimatedRms = productionCrowd.Metrics.RmsAmplitude * productionCrowd.VolumeLinear * oldUnitGain;
        var correctedEstimatedRms = productionCrowd.Metrics.RmsAmplitude * productionCrowd.VolumeLinear * correctedGain;
        GD.Print($"AMBIENCE ROOT-CAUSE COMPARISON: distance={distance:0.00}m, old UnitSize=1 gain={oldUnitGain:0.000}, " +
                 $"estimated RMS={oldEstimatedRms:0.0000}; corrected UnitSize=14 gain={correctedGain:0.000}, " +
                 $"estimated RMS={correctedEstimatedRms:0.0000}");
        Require(oldEstimatedRms < 0.0035f && correctedEstimatedRms >= 0.0035f,
            "The diagnostic no longer demonstrates the production attenuation failure and correction.");

        camera.GlobalPosition = venue.AudioAnchors[VenueAudioHookKind.GoldSidelineChatter].GlobalPosition +
                                new Vector3(-1, 0, 0);
        var sideline = await audio.TestAsync(VenueAudioTestKind.Sideline, VenueAudioTestMode.ProductionSpatial);
        Require(sideline.ExpectedAudible, "Normal sideline chatter bed was not audible near its production anchor.");
        camera.GlobalPosition = venue.AudioAnchors[VenueAudioHookKind.GoldBenchChatter].GlobalPosition +
                                new Vector3(-1, 0, 0);
        var chatter = await audio.TestAsync(VenueAudioTestKind.PlayerChatterBed, VenueAudioTestMode.ProductionSpatial);
        Require(chatter.ExpectedAudible, "Procedural bench/player chatter bed was not audible without Piper.");

        foreach (var kind in new[] { VenueAudioTestKind.Footstep, VenueAudioTestKind.CatchImpact,
                     VenueAudioTestKind.Whistle, VenueAudioTestKind.Cheer })
        {
            var action = await audio.TestAsync(kind, VenueAudioTestMode.ForcedNearSpatial);
            Require(action.PlayCalled && action.Playing && action.ExpectedAudible,
                $"{kind} diagnostic did not start an audible action stream.");
        }

        camera.GlobalPosition = new Vector3(-13, 3.2f, 0);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        camera.GlobalPosition = new Vector3(15, 19, 24);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Require(sourceIds.SequenceEqual(audio.PersistentSourceInstanceIds) &&
                audio.PersistentSourcesPlaying == audio.PersistentSpatialSourceCount,
            "Listener/camera changes stopped or recreated persistent ambience sources.");

        audio.ApplySettings(AmbientAudioSettings.Default with { DebugAmbienceBoost = true });
        Require(audio.PersistentSourcePositions.All(position => position.DistanceTo(camera.GlobalPosition) < 3),
            "Debug ambience boost did not place persistent sources within reliable listener range.");
        Require(audio.TargetLayerGain(VenueAudioLayer.CrowdAmbience) >= 0.9f,
            "Debug ambience boost did not raise ambient layer gain to an obvious test level.");
        audio.ApplySettings(AmbientAudioSettings.Default);
        Require(sourceIds.SequenceEqual(audio.PersistentSourceInstanceIds),
            "Audio settings/workspace-style updates recreated persistent ambience nodes.");
    }

    private Dictionary<Guid, Node3D> SpawnPlayers(Game game, GameProject project, PlayDefinition play)
    {
        var pawns = new Dictionary<Guid, Node3D>();
        foreach (var team in new[] { game.Gold, game.Navy })
        foreach (var player in team.Roster)
        {
            var pawn = new PlayerPawn { Name = $"Audio_{team.Name}_{player.Name}" };
            pawn.Configure(player, project.AppearanceFor(player.Id), project.ActiveUniformFor(team.Id));
            AddChild(pawn);
            var start = play.StartingPositions[player.Id];
            pawn.Position = new Vector3(start.X, 0.08f, start.Y);
            pawns.Add(player.Id, pawn);
        }
        return pawns;
    }

    private static void ValidateDeterministicConversations(
        PlaySimulation simulation,
        IReadOnlyDictionary<Guid, Node3D> pawns,
        IReadOnlyDictionary<Guid, PlayerVoiceProfile> profiles)
    {
        var first = VenueAudioController.BuildConversationSchedule(simulation, pawns, profiles);
        var second = VenueAudioController.BuildConversationSchedule(simulation, pawns, profiles);
        Require(first.SequenceEqual(second) && first.Count >= 3,
            "Ambient conversation scheduling was not deterministic or omitted contexts.");
        Require(first.All(cue => !string.IsNullOrWhiteSpace(cue.Text) && cue.SpeakerPlayerId != cue.ListenerPlayerId),
            "Ambient conversation template produced an invalid speaker/listener pair.");
        Require(first.All(cue => !cue.HasConfiguredVoice),
            "Default player profiles incorrectly claimed a configured TTS voice.");
    }

    private static void ValidateListenerDistance(VenueEnvironment venue, Camera3D camera)
    {
        var goldSideline = venue.AudioAnchors[VenueAudioHookKind.GoldSidelineChatter].GlobalPosition;
        var navySideline = venue.AudioAnchors[VenueAudioHookKind.NavySidelineChatter].GlobalPosition;
        camera.GlobalPosition = new Vector3(-12.5f, 2, 0);
        var nearby = VenueAudioController.EstimateSpatialAudibility(camera.GlobalPosition, goldSideline, 17);
        var distant = VenueAudioController.EstimateSpatialAudibility(camera.GlobalPosition, navySideline, 17);
        Require(nearby > 0.8f && distant == 0,
            "Player POV/sideline listener did not distinguish nearby and distant sideline chatter.");

        camera.GlobalPosition = new Vector3(15, 19, 24);
        var broadcastCrowd = VenueAudioController.EstimateSpatialAudibility(camera.GlobalPosition,
            venue.AudioAnchors[VenueAudioHookKind.CrowdAmbience].GlobalPosition, 58);
        Require(broadcastCrowd > 0,
            "Broadcast listener could not hear venue-scale crowd ambience.");
    }

    private static void ValidateDucking(VenueAudioController audio)
    {
        audio.AdvanceMix(2);
        var openCrowd = audio.CurrentLayerGain(VenueAudioLayer.CrowdAmbience);
        audio.SetAuthoredDialogueActivity(AuthoredDialoguePriority.Natural, true);
        audio.AdvanceMix(2);
        var naturalCrowd = audio.CurrentLayerGain(VenueAudioLayer.CrowdAmbience);
        audio.SetAuthoredDialogueActivity(AuthoredDialoguePriority.Featured, true);
        audio.AdvanceMix(2);
        var featuredCrowd = audio.CurrentLayerGain(VenueAudioLayer.CrowdAmbience);
        var featuredAction = audio.CurrentLayerGain(VenueAudioLayer.ActionSfx);
        Require(featuredCrowd < naturalCrowd && naturalCrowd < openCrowd,
            "Featured and natural dialogue did not apply ordered ambience ducking.");
        Require(featuredAction > featuredCrowd * 2,
            "Featured-dialogue ducking failed to preserve important action sounds.");
        audio.SetAuthoredDialogueActivity(AuthoredDialoguePriority.Featured, false);
        audio.SetAuthoredDialogueActivity(AuthoredDialoguePriority.Natural, false);
        audio.AdvanceMix(2);
        Require(Math.Abs(audio.CurrentLayerGain(VenueAudioLayer.CrowdAmbience) - openCrowd) < 0.002,
            "Ambient levels did not restore smoothly after authored dialogue.");
    }

    private static void ValidateEventReactions(VenueAudioController audio, PlaySimulation simulation)
    {
        audio.BeginPlay(simulation);
        foreach (var frame in simulation.Frames.Where((_, index) => index % 5 == 0))
            audio.HandleSimulationFrame(frame);
        foreach (var simulationEvent in simulation.Events)
            audio.HandleSimulationEvent(simulationEvent);

        foreach (var reaction in audio.ReactionHistory)
        {
            var sourceEvent = simulation.Events.First(item => item.Type == reaction.Trigger &&
                Math.Abs(item.TimeSeconds - reaction.SimulationTime) < 0.0001);
            Require(Math.Abs(sourceEvent.TimeSeconds - reaction.SimulationTime) < 0.0001,
                "Crowd reaction drifted from its authoritative simulation timestamp.");
        }
        foreach (var eventType in new[] { SimulationEventType.SnapStarted, SimulationEventType.ThrowReleased,
                     SimulationEventType.PassCompleted, SimulationEventType.FlagPullAttempted })
        {
            var source = simulation.Events.First(item => item.Type == eventType);
            Require(audio.OneShotHistory.Any(sound => Math.Abs(sound.SimulationTime - source.TimeSeconds) < 0.0001),
                $"{eventType} did not create an action sound at its simulation timestamp.");
        }
    }

    private static void ValidateOutcomeReactions(VenueAudioController audio, FootballPlaySimulator simulator, Game game)
    {
        var expected = new Dictionary<PlayOutcomeKind, CrowdReactionKind>
        {
            [PlayOutcomeKind.Incompletion] = CrowdReactionKind.Disappointment,
            [PlayOutcomeKind.DroppedPass] = CrowdReactionKind.Disappointment,
            [PlayOutcomeKind.Interception] = CrowdReactionKind.Interception,
            [PlayOutcomeKind.Touchdown] = CrowdReactionKind.TouchdownCelebration,
            [PlayOutcomeKind.OutOfBounds] = CrowdReactionKind.Moderate
        };
        foreach (var entry in expected)
        {
            var play = PlayDefinition.CreatePrototype(game, $"Audio {entry.Key}");
            play.SetSimulationSettings(play.SimulationSettings with { IntendedOutcome = entry.Key });
            var simulation = simulator.Simulate(play, game.Gold, game.Navy);
            audio.BeginPlay(simulation);
            foreach (var item in simulation.Events)
                audio.HandleSimulationEvent(item);
            Require(audio.ReactionHistory.Any(item => item.Reaction == entry.Value),
                $"{entry.Key} did not produce its distinct crowd reaction.");
        }

        audio.ApplySettings(new AmbientAudioSettings(1, 1, 1, 1, true, false));
        var disabled = PlayDefinition.CreatePrototype(game, "Disabled reactions");
        disabled.SetSimulationSettings(disabled.SimulationSettings with { IntendedOutcome = PlayOutcomeKind.Touchdown });
        var disabledSimulation = simulator.Simulate(disabled, game.Gold, game.Navy);
        audio.BeginPlay(disabledSimulation);
        foreach (var item in disabledSimulation.Events) audio.HandleSimulationEvent(item);
        Require(audio.ReactionHistory.Count == 0, "Crowd-reaction toggle did not suppress reactions.");
        audio.ApplySettings(new AmbientAudioSettings(1, 1, 1, 1, true, true));
    }

    private async Task ValidateDialogueIntegration(VenueAudioController audio,
        IReadOnlyDictionary<Guid, Node3D> pawns, FootballView football, Camera3D camera)
    {
        var dialogue = new DialoguePlaybackController { Name = "AudioPriorityDialogue" };
        AddChild(dialogue);
        dialogue.Configure(pawns, football, camera);
        dialogue.DialogueActivityChanged += audio.SetAuthoredDialogueActivity;
        var speaker = ((PlayerPawn)pawns.Values.First()).Player!.Id;
        var line = new DialogueLine(Guid.NewGuid(), speaker, 0, 0.16, "Featured test line.");
        var playback = dialogue.PreviewLineAsync(line);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Require(audio.AuthoredDialogueActive && dialogue.AudibleLineCount == 1,
            "Featured authored dialogue did not notify the ambience priority mixer.");
        await playback;
        Require(!audio.AuthoredDialogueActive,
            "Ambience priority remained ducked after authored dialogue ended.");
        dialogue.QueueFree();
    }

    private static void ValidateConversationPriority(VenueAudioController audio, PlaySimulation simulation)
    {
        audio.DevelopmentConversationFallbackEnabled = true;
        audio.BeginPlay(simulation);
        audio.SetAuthoredDialogueActivity(AuthoredDialoguePriority.Featured, true);
        foreach (var cue in audio.ConversationSchedule)
            audio.HandleSimulationFrame(simulation.FrameAt(cue.TimeSeconds));
        Require(audio.PlayedAmbientConversationCount == 0 && audio.SuppressedAmbientConversationCount > 0,
            "Ambient conversations overrode featured authored dialogue.");
        audio.SetAuthoredDialogueActivity(AuthoredDialoguePriority.Featured, false);

        audio.BeginPlay(simulation);
        foreach (var cue in audio.ConversationSchedule)
            audio.HandleSimulationFrame(simulation.FrameAt(cue.TimeSeconds));
        Require(audio.PlayedAmbientConversationCount > 0,
            "Explicit development fallback could not exercise ambient conversation playback.");
        audio.DevelopmentConversationFallbackEnabled = false;
    }

    private static string Snapshot(PlaySimulation simulation) => string.Join('|',
        simulation.Frames.SelectMany(frame => frame.Players.OrderBy(player => player.Key)
            .Select(player => $"{frame.TimeSeconds:R}:{player.Key}:{player.Value.Position}"))) +
        $"|{simulation.Outcome.Kind}:{simulation.Outcome.EndTimeSeconds:R}";

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
