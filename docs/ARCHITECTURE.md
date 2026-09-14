# Architecture

[Back to README](../README.md) · [Studio Guide](STUDIO_GUIDE.md) ·
[Validation](VALIDATION.md)

Flag Football Studio separates authored production data and football simulation
from Godot presentation. This makes plays repeatable and keeps rendering,
animation, camera, and audio improvements from changing their outcomes.

## Architectural invariants

1. Football-domain models and the simulator remain Godot-independent where
   practical.
2. `PlayDefinition` is the authored source of truth for formation, assignments,
   routes, timing, pass settings, and intended outcome.
3. `FootballPlaySimulator` produces the authoritative fixed-step timeline.
4. Presentation systems consume that timeline; they do not feed transforms or
   timing back into it.
5. Player identity and appearance data are distinct from meshes, rigs, and
   animation state.
6. Camera, dialogue, ambience, venue, and visual-quality behavior cannot modify
   football state.
7. Project files store portable authored data, never transient playback state or
   arbitrary absolute audio paths.

## Data flow

```text
GameProject
  ├─ teams, roster, appearances, uniforms
  ├─ ordered PlayDefinitions
  ├─ CameraDefinitions and per-play CameraCuts
  └─ DialogueSequences, voice profiles, audio references, visemes
             │
             ▼
FootballPlaySimulator ──► immutable frames/events/outcome
             │
             ├─► PlaySequenceController ──► transforms and football attachment
             ├─► animation/contact layer ──► skeletal presentation
             ├─► CameraDirectorController ──► preview camera and timed cuts
             ├─► DialoguePlaybackController ──► speech, mouth, gaze, expression
             └─► VenueAudioController ──► event-aligned ambience and action SFX
```

The workspace panels edit their corresponding models. `Main` composes those
panels with the field, characters, simulator, camera, lighting, environment,
persistence, and audio adapters.

## Domain and persistence

- `src/Football` contains project, game, play, simulation, direction, and
  football-presentation contracts.
- `src/Players` contains player identity and engine-independent appearance
  values alongside Godot presentation classes for the reusable humanoid.
- `src/Teams` contains team and uniform definitions.
- `src/Cameras` contains engine-independent camera/cut definitions and the Godot
  camera controller.
- `src/Dialogue` contains dialogue, voice, audio-reference, and lip-sync models
  plus their playback adapter.
- `src/Persistence` maps domain data through versioned JSON contracts and owns
  project-relative audio asset storage.

`GameProject` owns home and away teams, score, quarter, clock, down, distance,
possession, ordered plays, cameras, cuts, dialogue, voice profiles, appearances,
and team uniform libraries.

`ProjectJsonSerializer` currently uses format version 1. Missing collections in
older prototype files receive compatible defaults where supported.
`JsonProjectFileStore` performs filesystem I/O without depending on Godot. The
Godot layer supplies the resolved save location.

The default slot is:

```text
user://flag-football-studio/game-project.json
```

Godot maps `user://` to the operating system's per-user application-data
directory. Imported speech is copied next to the project under `audio/`, and
generated speech uses `audio/generated/`; JSON stores only relative references.

Transient values intentionally excluded from project JSON include active audio
sources, live mouth/expression/gaze state, animation blends, current playback
time, Free Look angles, mouse capture, tracking interpolation, environment
quality, and ambience mixer controls.

## Presentation responsibilities

### Play and animation

`PlaySequenceController` applies simulator frames to Godot players and the
football. `FootballAnimationQualityLayer` derives presentation cues from those
frames and events. `HumanoidAnimator`, `HumanoidContactSolver`, and
`FootballInteractionResolver` add skeletal motion, ground contact, and visual
ball attachments without changing authoritative state.

### Characters

All players use the same 18-bone hierarchy and reusable skinned topology.
Per-player appearance drives bone proportions, the fixed-topology face,
procedural hair, reusable eye/mouth geometry, accessories, and materials.
Stable eye, head, mouth, and hand anchors connect cameras, dialogue, and ball
presentation to the rig.

### Cameras and dialogue

`CameraDefinition` and `CameraCut` are authored data.
`CameraDirectorController` owns Godot camera placement, tracking, stabilized
POV, visibility layers, and timed switching. Dialogue models contain text and
timing; `DialoguePlaybackController` owns spatial sources and transient facial
presentation.

### Rendering, venue, and sound

`StudioMaterialLibrary` caches semantic shared materials.
`SportsLightingController` manages procedural outdoor-lighting presets.
`VenueEnvironment` and `ProceduralSpectatorSystem` build presentation-only venue
geometry. `VenueAudioController` consumes simulation/dialogue events and uses
stable `VenueAudioHooks` anchors; it does not participate in football rules.
Ambient phrase requests reuse `SpeechGenerationService`: cache misses run through
the bounded engine-independent TTS queue, while ready clips return to the main
thread and use `DialoguePlaybackController` for mouth-anchor playback and lip
sync.

## Repository organization

| Path | Responsibility |
| --- | --- |
| `assets/` | Imported/source assets grouped by animations, audio, characters, fields, footballs, UI, and uniforms |
| `data/` | Project-owned playbook, team, and uniform data areas |
| `scenes/` | Reusable Godot character, game, studio, and UI scenes |
| `src/Football`, `src/Players`, `src/Teams` | Core models and football/character feature code |
| `src/Animation`, `src/Rendering`, `src/Audio` | Presentation-only animation, visual, venue, and ambient systems |
| `src/Cameras`, `src/Dialogue`, `src/Tts` | Camera, dialogue, voice, and optional speech-generation features |
| `src/Persistence` | JSON and portable audio asset storage |
| `src/UI`, `src/Studio` | Workspace panels and studio composition helpers |
| `src/Validation` | Focused runtime regression validators |
| `tools/tts` | Optional isolated Python/Piper bridge |

Godot-generated metadata remains where Godot expects it. Empty planned asset or
data directories are retained with `.gitkeep` files.

## Related documentation

- [Simulation](SIMULATION.md)
- [Characters](CHARACTERS.md)
- [Cameras](CAMERAS.md)
- [Audio](AUDIO.md)
- [Environment](ENVIRONMENT.md)
