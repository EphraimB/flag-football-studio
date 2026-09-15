# Validation and Diagnostics

[Back to README](../README.md) · [Getting Started](GETTING_STARTED.md) ·
[Architecture](ARCHITECTURE.md)

Build first:

```powershell
dotnet build
```

Commands assume `godot` resolves to Godot 4.7 Mono/.NET. Each flag after `--` is
handled by the configured main scene.

## Validator matrix

| Area | Command | Principal coverage |
| --- | --- | --- |
| Workspaces | `godot --headless --path . -- --validate-workspaces` | Tabs, panel organization, camera sequence visibility, and action wiring |
| Simulation | `godot --headless --path . -- --validate-football-simulation` | Timing, routes, defense, arcs, outcomes, game state, repeatability, divergent fixtures, shared camera/dialogue timeline |
| Sports animation/contact | `godot --headless --path . -- --validate-sports-animation` | Event poses, gait/speed, cuts, blending, contact, ball/hand anchors, immutability, POV |
| Humanoid | `godot --headless --path . -- --validate-humanoids` | Shared topology, body extremes/builds, JSON, deformation, stable anchors |
| Faces | `godot --headless --path . -- --validate-faces` | Clamps/combinations, topology, finite geometry, JSON, animation and anchors |
| Expressions/eyes | `godot --headless --path . -- --validate-expressions` | Eye extremes, gaze targets/limits, blinking, blended expressions while moving, POV |
| Hair | `godot --headless --path . -- --validate-hair` | Styles with head extremes, clamps, deterministic geometry, accessories, JSON, animation/POV |
| Mouth | `godot --headless --path . -- --validate-mouth` | Visemes, jaw repetition, lips, expression layering, speech cycle, geometry, animation/POV |
| Facing/POV | `godot --headless --path . -- --validate-facing-pov` | Possession directions, reset/switch, route/QB facing, POV visibility, body/ball anchors |
| POV input | `godot --headless --path . -- --validate-pov-free-look` | Modes, clamps, recenter, targets, JSON/transient exclusion, mouse capture, facing/gaze independence |
| Camera profiles | `godot --headless --path . -- --validate-camera-profiles` | Lenses/mounts, visibility, sideline placement/tracking, focal zoom, cuts, JSON |
| Dialogue/spatial audio | `godot --headless --path . -- --validate-dialogue-audio` | Dialogue JSON, ordering/attachment, overlap, ranges, anchors/listeners, facial state restoration |
| Voice/lip sync | `godot --headless --path . -- --validate-voice-lip-sync` | Voice/audio/viseme JSON, bounds/order, WAV, silence/fallback modes, simultaneous speech, seek, cuts/POV |
| Local TTS | `godot --headless --path . -- --validate-local-tts` | Fake-provider CUDA/CPU contract, portable output, timing fallback, manual protection, model errors |
| Ambient player TTS | `godot --headless --path . -- --validate-ambient-tts` | Voice resolution, cache identity/rekeying, non-blocking bounded generation, failures, mouth anchors, priority, lip sync, and simulation invariance |
| Player voice setup | `godot --headless --path . -- --validate-player-voice-setup` | Piper pair discovery, no-download behavior, Player Studio assignment/copy/clear/test wiring, persistence, asynchronous generation, and ambient-cache integration |
| Visual presentation | `godot --headless --path . -- --validate-visual-presentation` | Material reuse, skin/team colors, quality/exposure/night lights, unchanged FOV/simulation |
| Venue | `godot --headless --path . -- --validate-environment` | Scoreboard, presets, deterministic spectators, resource bounds, camera clearance, hooks, simulation equivalence |
| Venue audio | `godot --headless --path . -- --validate-venue-audio` | Concurrent sources, scheduling/proximity, listeners, ducking, events/reactions, PCM, source lifetime, raw/near/production paths |

## Simulation trace

```powershell
godot --headless --path . -- --diagnose-football-simulation
```

This produces a readable comparison rather than pass/fail output. It logs
formation, waypoints/classification/progress, defender targets, QB release, pass
target, sampled ball path, and outcome. Use it when distinct plays appear too
similar.

## Detailed coverage notes

### Simulation and presentation invariance

The simulation validator compares slant, go, out, post, and rusher fixtures and
fails if their paths, throw times, trajectories, defensive behavior, outcomes,
or final positions converge. Animation, visual, venue, and audio validators
snapshot authoritative results to ensure presentation never feeds back.

Sports-animation coverage includes QB hold/release, two-hand catch/interception,
drop separation, carry attachment, support-foot retention, correct timestamps,
and unchanged outcomes.

### Character safety

Face validation checks every range end, risky combinations, all persisted face
fields and eye color, fixed topology, finite geometry, and stable anchors. Hair
repeats extreme-head tests for every style. Expression and mouth validators
exercise blending during locomotion.

### Cameras and audio

POV validation confirms head-layer hiding applies only to the local POV while
body and broadcast character stay complete; camera look cannot change player
facing or eye gaze.

Dialogue validation moves POV and broadcast listeners with simultaneous spatial
sources. Voice validation distinguishes unrecorded silence, explicit fallback,
generic motion, automatic timing, and manual lip sync.

Venue-audio validation inspects PCM sample/non-zero counts, peak/RMS, loops, and
persistent source identity through listener/settings changes. It exercises raw,
forced-near, and production crowd paths, sideline/bench beds, debug one-shots,
diagnostic boost, and venue-scale Broadcast attenuation.

Ambient-player-TTS validation uses delayed fake providers to verify that request
submission does not block the simulation caller, provider work stays off the
Godot thread, and only ready clips return to main-thread scene/audio operations.
It also checks player-specific cache separation, deterministic hits, profile
rekeying, intentional missing-voice silence, failure containment, queue bounds,
mouth-anchor spatial playback, timed visemes/restoration, authored-dialogue
preemption, and unchanged persistent venue beds and simulator output.

### TTS without a local model

`--validate-local-tts` uses a deterministic in-process fake provider. CI does
not need Python, Piper, a model, or GPU to verify contracts and persistence.
`--validate-player-voice-setup` likewise uses temporary model stubs and a delayed
fake provider; it does not download or invoke a real voice.

## Repository hygiene

Before handoff:

```powershell
git diff --check
```

For documentation work, inspect relative Markdown targets and ensure documented
flags still appear in the main scene's command-line dispatch.
