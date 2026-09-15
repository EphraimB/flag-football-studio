# Audio, Dialogue, and Speech

[Back to README](../README.md) · [Studio Guide](STUDIO_GUIDE.md) ·
[Validation](VALIDATION.md)

Audio is a presentation system. It follows simulation and dialogue timing but
does not modify football state.

## Dialogue data and playback

`DialogueSequence` and `DialogueLine` are Godot-independent production data. A
sequence can be associated with a play, pre-play/huddle, post-play, or the
sideline. Ordered lines store speaker, text, start/duration, volume,
Whisper/Quiet/Normal/Loud/Shout style, audibility radius, optional listener,
expression/gaze targets, portable audio reference, and timestamped visemes.

`GameProject` owns sequences and player voice profiles.
`DialoguePlaybackController` schedules overlapping lines and mounts one
`AudioStreamPlayer3D` at each speaker's stable mouth/head anchor. It applies
transient mouth, expression, and gaze state and restores prior presentation
state when the line ends.

## Recorded audio assets

Dialogue Director accepts WAV, OGG Vorbis, and MP3 through Godot's native
decoders. **Import Audio...** copies the file beside project JSON under
`audio/`; JSON stores a validated relative `VoiceAudioReference`, never the
source computer's absolute path.

The UI distinguishes no audio, generic mouth motion, automatic approximate or
provider timing, and manual lip sync. **Preview Audio** plays the assignment.
**Remove Audio** clears only the reference and leaves the imported copy for
reuse. Text and audio remain independent. Normal playback of an unrecorded line
is silent; the developer preview tone is explicit and defaults off.

## Lip sync

Each `VisemeEvent` stores start time, optional end/transition time, an existing
mouth pose, and blend strength. Events are ordered and bounded.

Manual events drive `SpeechMouthController`. Without them, playback uses a
clearly labelled deterministic generic cycle or an explicitly generated
approximate track. This is not phoneme recognition or forced alignment.
Expressions and speech poses layer, and independent sources support simultaneous
speakers.

## Voice profiles and optional local TTS

`PlayerVoiceProfile` stores profile identity, description, volume, pitch,
speaking-rate metadata, backend/model selection, speaker ID, optional reference
recording, style, and emotion without depending on Godot.

Player Studio's **Voice** tab edits that same persisted profile. It shows the
roster's configured count, marks silent players with an open circle, discovers
complete Piper model/configuration pairs, and supports explicit copy/paste and
clear actions. **Test Voice** generates “Ready for the next play.” without
blocking the UI, then previews it from the selected player's mouth anchor.
Saving a valid profile is immediately visible to both authored and ambient TTS;
there is no secondary voice registry.

`ITtsProvider` is an asynchronous engine-independent boundary.
`PiperTtsProvider` exchanges JSON over standard I/O with
`tools/tts/piper_service.py`; football code and Godot nodes do not import Python.
Generated files use collision-safe `audio/generated/` destinations.

Piper setup is in [Getting Started](GETTING_STARTED.md). CUDA is preferred with
a CPU retry. No runtime, model, or voice is downloaded automatically. Provider
phoneme durations are used when available; `AutomaticLipSyncGenerator` otherwise
creates labelled approximation. Manual tracks require explicit replacement.

## Spatial listening

The active preview `Camera3D` is the listener. Player POV uses its stabilized
wearable mount; broadcast, sideline, and free views listen at their own
transforms. Camera cuts do not restart or retime dialogue. Whisper/quiet shorten
effective range; loud/shout extend it. This is distance treatment, not full
acoustic simulation.

## Venue audio layers

`VenueAudioController` uses stable `VenueAudioHooks` positions for crowd beds
and reactions, sideline/bench chatter, footsteps, flags/equipment, whistles,
catch/throw impacts, and celebrations.

Eight inexpensive looping spatial sources establish crowd, sideline, and bench
beds. Procedural PCM is deterministic, cached, developer-safe, and cloud-free.
Immutable simulator events trigger one-shots at authoritative times; frames
schedule footsteps.

Ambient player remarks use a deterministic phrase pool, proximity, and authored
dialogue priority. For the speaking player, the controller resolves that
player's `PlayerVoiceProfile` and asks the existing `SpeechGenerationService`
for local speech. It never substitutes another player or a generic voice.
Missing or invalid voice configuration remains intentionally silent unless the
explicit development fallback is enabled. The procedural chatter bed is
separate and does not require Piper.

Repeated phrases use deterministic cache keys covering player/profile identity,
provider/backend, model, speaker, reference audio, phrase, volume, pitch, rate,
style, and emotion. WAV and viseme metadata are stored under
`audio/generated/ambient/`. Cache misses enter a bounded two-worker/eight-item
queue and skip the current utterance; gameplay never waits. A later occurrence
uses the completed clip. Provider work performs no Godot scene-tree access;
decoding, source creation, anchor attachment, and facial playback return to the
main thread.

Ready ambient clips play through `DialoguePlaybackController` at the speaker's
mouth anchor with provider-timed visemes or the existing approximate fallback.
The controller restores prior mouth/expression/gaze state. Authored dialogue
stops ambient speech before taking its own presentation snapshot and prevents
new ambient lines until it finishes.

## Mix priority

1. Featured authored dialogue.
2. Natural authored dialogue.
3. Player and ball action sounds.
4. Ambient player conversations.
5. Sideline/bench chatter.
6. Crowd ambience.

Featured dialogue modestly ducks crowd/chatter while preserving action SFX;
natural dialogue uses a lighter duck. Gains interpolate over roughly 180 ms and
restore after the last line. Ambience controls currently use Master and are
transient. Venue-scale inverse-distance `UnitSize` values keep real anchors
audible; `MaxDistance` alone is not a gain control.

## Diagnostics

Dialogue Director provides raw 2D and forced-near spatial tests for a selected
recording. Game workspace ambience tests offer Raw 2D, Forced Near Spatial, and
Production Spatial paths for Crowd, Sideline, and Player Chatter Bed, plus
Footstep, Catch Impact, Whistle, and Cheer.

Reports include stream state/duration, PCM sample and non-zero counts, peak/RMS,
source/listener positions and distance, attenuation, linear/dB gain, bus,
Master mute, `Play()` call, and post-start `Playing` state.

**Debug ambience boost** defaults off and temporarily brings persistent beds to
a reliable range and level without recreating them. “Engine expected audible”
is a calculated signal-path result; physical output still requires listening.
The compact Ambient TTS status reports cache hits/misses, pending count, and the
last skipped or playback reason. Missing-voice and proximity skip logs identify
the speaking player and suppress identical console messages for five seconds.

## Current limitations

- Venue signals are low-fidelity procedural waveforms, not performances.
- No obstruction, reverb zones, acoustic environments, surface footstep
  library, audience convolution, or production mastering exists.
- Piper models, voices, and recording tools are not bundled.
- Piper output is phoneme-duration timing rather than forced alignment, and the
  adapter does not clone voices or apply style conditioning.
- Speaking rate is metadata; pitch adjustment also changes playback speed.
- No transcription, time stretching, waveform editor, subtitles, localization,
  or full phoneme extraction is implemented.
- Expression/gaze restoration does not reinstate a former moving target tracker.
- Ambient TTS is generated on demand rather than comprehensively prewarmed, so
  the first occurrence of a cache miss is intentionally skipped.
- Failed cache keys remain suppressed for the current service lifetime; editing
  the voice configuration creates a new key, and restarting retries unchanged
  configuration.
