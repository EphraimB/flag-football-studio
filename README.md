# Flag Football Studio

Flag Football Studio is an open-source 3D filmmaking studio for designing,
directing, and rendering fully customizable flag football games.

The director prototype demonstrates the core architecture with a primitive 3D
field, two five-player teams, a top-down play editor, persistent play and camera
libraries, a game-state scoreboard, configurable camera previews, editable
humanoid player appearances, reusable team uniform libraries, and a deterministic
fixed-step flag-football simulation with skeletal animation states. Player appearances now
include deterministic low-poly face customization, blended expression previews,
blinking, gaze control, and reusable procedural hairstyles. It is an
architectural prototype rather than a physics-driven competitive game. The presentation
layer also includes deterministic mouth shapes intended as a future speech-
animation foundation. Dialogue Director now supports portable recorded voice
clips, per-player voice metadata, timestamped manual visemes, and procedural 3D
placeholder audio only when explicitly previewing an unrecorded line. The first
optional local TTS pipeline uses an isolated Piper process, prefers NVIDIA CUDA,
falls back to CPU, writes portable WAV output, and consumes provider phoneme
timings when available.

## Repository layout

- `assets/` contains source and imported presentation assets, grouped by
  characters, uniforms, footballs, fields, animations, audio, and UI.
- `scenes/` contains Godot scenes, grouped around reusable characters, games,
  studio tools, and UI.
- `src/Football`, `src/Players`, and `src/Teams` contain the plain C# domain
  model. `Player`, `Team`, and `Game` do not depend on Godot.
- Godot-facing presentation classes live beside their feature areas:
  `PlayerPawn` adapts a domain player to the reusable `HumanoidRig`, `FieldView`
  builds primitive field geometry, and `FootballView` renders the placeholder
  ball.
- `HumanoidSkeletonDefinition` is the single shared 18-bone hierarchy used by
  every Gold and Navy player. `HumanoidSkinnedMesh` builds one reusable indexed
  `ArrayMesh` and one bind-pose `Skin`; every player instance shares that mesh,
  topology, vertex weights, and surface layout.
- `HumanoidRig` connects the shared mesh to its skeleton instance, assigns
  per-player materials, applies body proportions through bone rest/pose
  transforms, and exposes stable eye and catch transforms. `HumanoidFaceMesh`
  builds a per-player face from one fixed indexed topology and attaches it to
  the shared head bone. Uniform trim, flags, labels, and accessories remain
  lightweight bone-attached details.
- `HumanoidHairRig` is a reusable head-bone child that rebuilds deterministic
  low-poly pieces around the current face dimensions. It provides buzz, short,
  medium, long, curly, ponytail, and bun styles and applies simple headband and
  visor clearance offsets.
- `HumanoidEyeRig` provides reusable eyeball, iris, pupil, upper-lid, and
  lower-lid geometry. It applies independently clamped horizontal and vertical
  gaze without rotating or moving the Player POV anchor.
- `FacialExpressionController` owns presentation-only expression, eyebrow,
  blink, and blend state. Its deterministic poses deform the existing brow and
  mouth topology while driving eyelid openness; no expression or gaze state is
  added to the football domain or saved game project.
- `SpeechMouthController` blends Rest, A, E, I, O, U, M/B/P, F/V, L, and W/Q
  poses plus manual jaw and upper/lower-lip controls. `HumanoidRig` layers this
  pose with the active expression before rebuilding the same fixed face
  topology.
- `HumanoidMouthRig` supplies simple reusable inner-mouth and upper/lower teeth
  geometry on the head bone. Speech state is transient presentation data and
  is deliberately absent from football domain models and project JSON.
- `HumanoidAnimator` samples idle, jog, sprint, turn, throw, catch, and
  flag-pull poses, blending between states before applying them to the shared
  skeleton. It is presentation-only and does not add animation state to player
  identity or persisted project data.
- `FootballPlaySimulator` is a Godot-independent fixed-step simulation service.
  It converts a `PlayDefinition` into player/ball frames, assignments, route
  progress, possession state, discrete football events, and a final
  `PlayOutcome`. Timing, acceleration, pursuit, pass arcs, and results are
  deterministic.
- `PlaySequenceController` consumes that simulation timeline, moves Godot player
  and football nodes, attaches caught balls to existing hand anchors, and maps
  simulation motion states onto humanoid animation states. `PlayDirectionResolver`
  derives the offense's attacking field direction from possession and formation
  separation; `FormationFacing` converts that convention to Godot's local `-Z`
  character-forward axis. The main studio scene composes models, views, camera,
  lighting, and UI.
- `PlayDefinition` is the Godot-independent source of truth for formation
  positions, waypoint routes, defensive assignments, quarterback selection,
  intended receiver, snap/throw/reaction timing, pass arc, and authored outcome.
  Both the 2D editor and simulation consume it.
- `PlayDirectorPanel` translates mouse interaction and top-down field
  coordinates into domain-model edits without placing rendering types in the
  domain layer.
- `GameProject` owns home and away teams, score, quarter, clock, down,
  distance, possession, and its ordered collection of named plays.
- `ProjectJsonSerializer` maps domain objects through versioned JSON data
  contracts. `JsonProjectFileStore` handles filesystem I/O without depending
  on Godot; the presentation layer supplies the platform-specific save path.
- `GameDirectorPanel` presents the scoreboard and coordinates play-library
  commands while `Main` applies those commands to domain state.
- `CameraDefinition` and `CameraCut` are Godot-independent descriptions of
  reusable cameras and per-play cut timing. `GameProject` owns both collections,
  and the existing JSON project file persists them. `PlayerPovSettings` stores
  the selected POV mode, sensitivities, stabilization/head-bob strengths, camera
  offsets, near clip, and Look At target without depending on Godot types. The
  same definition owns action-camera lens/mount data and a separate
  `SidelineCameraSettings` value for physical placement, optics, operator
  framing, tracking, and optional auto-zoom.
- `CameraDirectorPanel` edits camera data. `CameraDirectorController` is the
  presentation adapter that positions the Godot `Camera3D`, stabilizes Player
  POV views, applies camera-specific first-person visibility, and performs timed
  cuts during playback.
- `DialogueSequence` and `DialogueLine` are Godot-independent production data.
  A sequence records its play, pre-play/huddle, post-play, or sideline context;
  each ordered line records timing, speaker, text, level/style, radius, optional
  listener, expression, and gaze metadata. `GameProject` owns the sequence
  library and `ProjectJsonSerializer` persists it with the rest of the project.
- `DialogueDirectorPanel` edits sequences and ordered lines.
  `DialoguePlaybackController` is the Godot presentation adapter: it schedules
  overlapping lines, mounts one `AudioStreamPlayer3D` on each speaker's stable
  head-bone `MouthAudioAnchor`, loads WAV, OGG Vorbis, or MP3 speech, drives
  generic or timestamped mouth poses, and temporarily applies/restores
  expression and gaze state.
- `PlayerVoiceProfile` stores Godot-independent voice identity metadata for each
  roster player, including an engine-agnostic backend type, model identifier or
  path, speaker ID, optional reference recording, defaults, style, and emotion.
  `VoiceAudioReference` stores only a validated project-relative
  asset path and format, while ordered `VisemeEvent` values store start/end
  timestamps, mouth shape, and blend strength without referencing Godot types.
- `ProjectAudioAssetStore` imports voice files beside the project JSON under
  `audio/` and creates collision-safe `audio/generated/` WAV destinations.
  `VoiceAudioStreamLoader` is the engine adapter that decodes those
  portable references using Godot's native WAV, OGG Vorbis, and MP3 support.
- `ITtsProvider` is a Godot-independent asynchronous provider boundary.
  `PiperTtsProvider` talks JSON over standard input/output to the optional
  `tools/tts/piper_service.py` process; neither football/domain code nor Godot
  nodes import Python. `AutomaticLipSyncGenerator` maps provider phoneme timing
  first and uses clearly labelled deterministic approximation when timing is
  unavailable.
- `PlayerAppearance` is a Godot-independent per-roster-player model for height,
  build, shoulder/chest/waist/hip widths, arm/leg lengths, skin, hair, and
  optional accessories. Its Godot-independent `FaceAppearance` value stores
  clamped face proportions and eye color; it contains no meshes, nodes, or
  engine vectors. Its Godot-independent `HairAppearance` value stores style,
  length, volume, hairline, part, curl/wave, color, ponytail, and bun settings.
  Jersey numbers are authoritative on the roster `Player`; team-wide styling
  is authoritative on `UniformDefinition`. `GameProject` owns and persists the
  appearance collection.
- `PlayerStudioPanel` edits appearance data, while `PlayerPawn` and
  `HumanoidRig` translate it into live skeletal deformation and materials. No
  rendering types enter the appearance model.
- `UniformDefinition` is a Godot-independent, team-associated description of
  uniform colors, trim, typography options, and home/away designation.
  `GameProject` owns each team's saved uniform library and active selection,
  and `ProjectJsonSerializer` persists both.
- `UniformStudioPanel` edits the uniform library. `PlayerPawn` combines an
  active team uniform with each player's roster number and individual
  appearance to rebuild the procedural humanoid presentation live without
  replacing its stable rig anchors.
- `data/` contains project-owned, non-code data for teams, uniforms, and
  playbooks.
- `project.godot` and Godot-generated import metadata remain at the project
  root where Godot expects them.

Empty directories contain `.gitkeep` files so the intended architecture can be
versioned before implementation begins.

## Run the prototype

Open `project.godot` with Godot 4.7 Mono/.NET and press **F6** or **F5**. The
configured main scene opens automatically. Press **Run Play** to reset and run
the snap, short receiver route, defensive coverage, throw, catch, and closing
movement.

### Play Director controls

- **Move:** drag any Gold or Navy marker into formation.
- **Route:** select a Gold marker, then click open field locations to append
  waypoints. **Clear Route** removes the selected player's route.
- **Coverage:** select a Navy marker, then select the Gold player it covers.
- **Set QB:** select the Gold player who receives the snap.
- **Target:** select the intended Gold receiver.
- **Reset:** restore the prototype formation, route, coverage, quarterback,
  and target.
- **Run Play:** preview the current `PlayDefinition` in 3D.
- **Simulation:** edit pre-snap delay, choose a time-based or receiver-route-
  milestone throw trigger, set short/medium/deep pass arc and deterministic
  outcome, and tune defensive reaction and rusher release delays. These values
  are saved with each play.

### Play library and persistence

- Select a play in **Play Library** to switch the 2D editor and 3D preview.
- **New** adds a new play based on the prototype formation.
- Enter a name and choose **Rename** to rename the selected play.
- **Duplicate** makes a deep copy, including formation, routes, coverage,
  quarterback, and target. **Delete** removes the selected play; one play must
  remain in the project.
- **Save Project** writes the full game state and ordered play library as JSON.
- **Load Project** restores that file and selects its first play.

The prototype uses one automatic save slot at
`user://flag-football-studio/game-project.json`. Godot maps `user://` to the
current operating system's per-user application-data directory.

### Camera Director controls

- Select a camera in the camera list to make it active and preview it.
- **New**, **Rename**, **Duplicate**, and **Delete** manage project cameras.
- **Type** selects Broadcast Wide, Sideline Low, End Zone, Player POV, or Free
  Camera.
- For **Player POV**, choose a Gold or Navy player from **POV Player**.
- Player POV is a wearable action-camera profile. Choose GoPro Wide, GoPro
  Linear, a SuperView-style approximation, or Narrow; edit horizontal/vertical
  FOV, distortion approximation, horizon leveling, stabilization, body motion,
  smoothing, offsets, and near clip; and mount it at the eyes, forehead, chest,
  or shoulder. Head-only culling remains specific to the POV camera.
- **POV Mode** provides **Locked Forward**, **Free Look**, and **Look At Target**.
  Locked Forward retains the stabilized forward view. Free Look layers clamped
  mouse or controller yaw/pitch over the mount. Look At Target smoothly tracks
  another player, the football, or a world-space XYZ point without changing eye
  gaze.
- The compact POV settings grid edits mouse and controller sensitivity,
  stabilization strength, head-bob strength, pitch offset, forward offset, and
  near clip. **Recenter** smoothly returns transient view angles to player
  forward.
- For **Free Camera**, edit position, rotation in degrees, and FOV. Changes are
  previewed immediately; **Preview** reapplies the selected camera at any time.
- For **Sideline Low**, select the left or right sideline, physical height and
  distance beyond the boundary, Static/Track Player/Track Football/Follow Play
  Center/Manual behavior, target, pan, tilt, tracking strength, and framing
  offset. The camera pans and tilts in place rather than translating with play.
  Focal length changes projection FOV without moving the camera. Min/max focal
  length, zoom speed, target size, and auto-zoom are saved per camera; the mouse
  wheel optically zooms unhandled viewport input and **Reset Zoom** restores
  35 mm.
- Enter a time in seconds and choose **Add Cut** to add the selected camera to
  the current play's cut list. Select a cut and choose **Delete Cut** to remove
  it. Timed cuts run when **Run Play** is pressed.

The play simulation, camera cuts, and play-context dialogue start together on
the same playback timeline. Cameras and dialogue remain presentation systems
and do not alter the authored football outcome.

Camera definitions, cuts, and the saved POV settings are included whenever
**Save Project** is used and are restored by **Load Project**. Live Free Look
yaw/pitch, recenter progress, and mouse-capture state are intentionally transient.
Auto-zoom convergence and tracking interpolation are also transient; their
configured lens, limits, target, and strengths persist.

Player POV uses a damped wearable mount that tracks a stable eye, head, chest,
or shoulder rig anchor. Root movement and turns remain football-driven while
skeletal mount motion is smoothed and optionally horizon-leveled. Configurable
forward/up offsets, near clip, pitch, and body-motion strength keep the torso and
moving hands naturally available to the frame.

Only the selected player's head-attached face, eyes, mouth, hair, and head
accessories move to the dedicated first-person head render layer. The POV camera
excludes that layer while continuing to render the shared skinned torso, arms,
hands, legs, feet, flags, and a caught football. Other cameras continue to
include the head layer and therefore see the complete character. Eye gaze,
blinks, and expressions remain independent from the camera mount.

Free Look captures the mouse only while that mode is active. Move the mouse to
look, press **R** or the controller right stick to recenter, and press **Escape**
to release the mouse for studio UI interaction. Clicking an unhandled part of
the 3D viewport recaptures it. Controller right-stick axes use the same clamped
view limits: yaw ±110 degrees and total pitch from -75 to +65 degrees.

Formation orientation is not stored per player. Positive play-field Y appears
upfield in Play Director and maps to world positive Z. The offense faces its
resolved attacking direction, and the defense faces the reverse direction.
Changing possession swaps the two roles; switching or resetting a play resolves
the convention again from that play's formation. White direction ticks on the
2D player markers show the same facing used by the 3D preview.

### Player Studio controls

- Choose any Gold or Navy roster member from the player list.
- Use the **Body** tab to edit height; slim, average, athletic, or heavy build;
  shoulder, chest, waist, and hip width; arm and leg length; skin tone; jersey
  number; and the active team's primary, secondary, and flag colors.
  Jersey-number changes update the roster; team color changes update the active
  uniform.
- Use the dedicated **Hair** tab to select None, Buzz Cut, Short, Medium, Long,
  Curly, Ponytail, or Bun and edit length, volume, hairline height, part
  position, curl/wave amount, color, ponytail length/volume, and bun size. A
  legacy Mohawk option remains available so older project files retain their
  appearance.
- Use the dedicated **Face** tab to edit head width/height, jaw width/height,
  chin width/projection, cheekbone width/fullness, forehead height, eye
  spacing/size/vertical position, eyebrow height, nose width/length/projection,
  mouth width, lip fullness, ear size/position, and eye color.
- Choose Neutral, Smile, Focused, Concerned, Surprised, or Frustrated under
  **Expression preview**. Transitions blend smoothly and do not interrupt body
  animation.
- **Blink Test** triggers one blink. **Auto Blink** enables a deterministic
  blink every 3.2 seconds for the selected player.
- Under **Gaze test**, choose manual horizontal/vertical gaze, the football, or
  another roster player, then choose **Apply Gaze**. **Center** restores neutral
  manual gaze. Presentation APIs also support arbitrary world-space points.
- Use the dedicated **Mouth** tab to preview jaw opening, lip width/fullness,
  independent upper/lower-lip offsets, and Rest, A, E, I, O, U, M/B/P, F/V, L,
  or W/Q mouth shapes. **Cycle Speech Shapes** previews the complete sequence
  and returns to Rest. These controls layer over expressions but are not saved.
- Toggle headband, wristbands, visor, and arm-sleeve accessories independently.
- Body and face changes rebuild the selected player's presentation immediately
  in the 3D preview while preserving its skeleton and stable camera anchors.

Hair length and tied-hair length use `0.50–1.50`; general and ponytail volume
and bun size use `0.70–1.40`; hairline height uses `-0.15–0.15`; part position
uses `-1.00–1.00`; and curl/wave amount uses `0.00–1.00`. Values are clamped in
the engine-independent model before rendering or persistence.

All player appearances are included in **Save Project** and restored by
**Load Project**. Older project files without appearance data receive default
Gold and Navy placeholder appearances when loaded.

### Uniform Studio controls

- Choose **Gold** or **Navy** to view that team's saved uniform library.
- **New**, **Rename**, **Duplicate**, and **Delete** manage uniforms. Each team
  must retain at least one uniform.
- Select a saved uniform and choose **Set Active** to dress every player on that
  team in it immediately.
- Edit its home/away designation, wordmark, player-name-on-back toggle, and
  primary, secondary, accent, jersey, sleeve trim, collar trim, number, number
  outline, shorts, and flag colors.
- Uniform edits are reflected live when the selected uniform is active. Player
  jersey numbers continue to come from the roster and are not stored in the
  uniform.

All saved uniforms and each team's active selection are included in **Save
Project** and restored by **Load Project**. Older project files without uniform
data receive default Gold and Navy home/away uniforms when loaded.

### Dialogue Director controls

- Enter a sequence name, choose **Play**, **Pre Play Huddle**, **Post Play**, or
  **Sideline**, then choose **New Sequence**. Non-sideline sequences attach to
  the currently selected play; sideline sequences remain project-wide.
- Select a sequence, choose a roster speaker, enter the preserved script text,
  and set start time, duration, normalized volume, speech style, and audibility
  radius. Whisper and quiet styles intentionally reduce effective range; loud
  and shout styles extend it.
- Optionally choose a listener, an expression, and a gaze mode. Gaze can target
  the listener, another roster player, the football, or a world-space XYZ
  point.
- **Add / Update** saves the line. **Move Up**, **Move Down**, and **Delete Line**
  edit its persisted order. **Preview Line** plays only the selected line.
- **Run Play** schedules all `Play`-context dialogue attached to the active play
  alongside the existing choreography and camera cuts. Switching plays updates
  the visible dialogue library and cancels transient playback.
- Under **Voice / Audio**, edit the selected speaker's profile name,
  description, default volume, pitch in semitones, and speaking-rate metadata.
  The reusable voice-profile selector can choose any roster voice for the
  selected line. Choose the Piper backend, select an installed `.onnx` model,
  optionally enter its speaker ID/style/emotion metadata, then choose **Save
  Voice** to persist the configuration.
- **Generate Speech** queues an asynchronous local request for an unrecorded
  line. **Regenerate Speech** explicitly creates a new collision-safe WAV and
  replaces the line's assignment; it never overwrites the old file. Status
  reports queued, generating, completed, failed, or cancelled and identifies
  CUDA versus CPU. **Generate Lip Sync** creates an approximate track for an
  imported clip; **Regenerate Lip Sync** is the explicit action permitted to
  replace a manual track.
- **Import Audio...** opens the operating system's native WAV, OGG, and MP3
  picker, copies the recording into the portable project `audio/` directory,
  and assigns its relative reference to the selected line. The always-visible
  status row shows the imported filename, decoded clip duration, and exactly
  whether there is no assignment, generic mouth motion, automatic approximate
  timing, automatic provider timing, or manual lip sync.
  **Preview Audio** plays the assigned recording; **Remove Audio** removes only
  the assignment and deliberately leaves the imported file available for reuse.
- **Test Raw Audio** sends the exact selected stream through a non-spatial
  `AudioStreamPlayer` on the Master bus. **Test Spatial Audio** sends that stream
  through `AudioStreamPlayer3D` one meter in front of the active camera with
  attenuation disabled and a generous maximum distance. The diagnostic readout
  and Godot log report the resolved path, file/decoder metadata, stream and
  playback state, bus/mute state, listener/source positions, distance, volume,
  and attenuation configuration. If raw succeeds but normal preview is silent,
  compare its listener distance with the line's effective audibility radius.
- Dialogue text never generates speech automatically. **Preview Line** reports
  a missing recording unless **Developer tone for unrecorded previews** is
  explicitly enabled; that option is off by default.
- The manual lip-sync list supports adding/updating a viseme with a start time,
  optional end time, and blend strength, selecting and editing existing events,
  and deleting events. **Preview From Time** seeks the real audio and evaluates
  the timestamped mouth track from that position.

Dialogue metadata, including text and ordered line timing, is saved in the
existing project JSON. Audio sources, current mouth pose, live expression/gaze,
and playback time are transient and are never serialized. Older JSON without a
dialogue collection loads with an empty library and default voice profiles.
Absolute source paths are never serialized. Moving the project JSON together
with its sibling `audio/` folder preserves all assignments.

### Optional local speech setup

Piper is not installed and no voice model is downloaded automatically. Install
a supported 64-bit Python with the Windows `py` launcher, then on the target
Windows RTX machine run:

```powershell
./tools/tts/setup.ps1 -Runtime Cuda
```

The script creates `tools/tts/.venv`, installs Piper plus
`onnxruntime-gpu`, and prints the explicit `piper.download_voices` command for
placing a chosen voice in the ignored `tools/tts/models/` directory. Use
`-Runtime Cpu` on a non-NVIDIA machine. In Dialogue Director, select the
downloaded `.onnx` file; its adjacent `.onnx.json` is required. Generation
prefers `CUDAExecutionProvider` and retries on CPU if CUDA initialization fails.
Missing runtime/model/config errors are shown in status rather than triggering a
download. Generated WAV files are produced only by **Generate Speech** or
**Regenerate Speech**, never during ordinary playback or final rendering.

Spatial listening follows Godot's active preview `Camera3D`. Player POV moves
that camera to the stabilized eye mount, while broadcast/free camera previews
move the same listener to their camera transform. This preserves positional
left/right direction and distance attenuation through camera changes without
coupling audio to football rules. Concurrent lines each own an independent 3D
source, so nearby conversations may overlap.

### Humanoid validation

The focused football simulation validation is available with:

```powershell
godot --headless --path . -- --validate-football-simulation
```

It checks snap, route, reaction, throw and catch timing; pass arcs; every
authored outcome; flag pulls; score, possession, down and distance updates;
JSON persistence; repeatability; and camera/dialogue timing against the same
play duration.

After building, run the focused headless validation with:

```powershell
godot --headless --path . -- --validate-humanoids
```

It validates shared mesh identity and topology, height extremes, every body
build, opposite proportion extremes, JSON round trips, jog/sprint/throw/catch/
flag-pull deformation, and eye/hand anchors after morphing.

### Face validation and parameter safety

After building, run the focused facial validation with:

```powershell
godot --headless --path . -- --validate-faces
```

All dimension and fullness controls use a safe `0.75` to `1.25` scale range.
Chin projection, eye vertical position, nose projection, and ear position use
an offset range of `-0.20` to `0.20`. The domain model also constrains risky
combinations: jaw width is at most head width plus `0.12`; chin width is at
most jaw width plus `0.03`; cheekbone width and eye spacing are bounded by head
width; eye size is bounded by eye spacing; nose width is bounded by eye
spacing; and mouth width is bounded by jaw width. Player Studio writes the
clamped values back into its controls so the persisted value is always the one
shown.

The face validator checks both ends of every range, conflicting combined
extremes, fixed vertex/index topology, non-empty finite geometry, all 20 JSON
fields plus eye color, reuse of the skinned body resource, animation
compatibility, and stable eye/POV and hand anchors.

### Expression and eye validation

Run the focused presentation validation with:

```powershell
godot --headless --path . -- --validate-expressions
```

It covers minimum-spacing/maximum-size eye combinations, both gaze axes at
their full limits, moving node targets and world-space targets, repeated manual
and deterministic automatic blinks, interpolation through every expression
while jogging, topology invariance, and Player POV stability.

### Hair validation

Run the focused procedural-hair validation with:

```powershell
godot --headless --path . -- --validate-hair
```

It checks every supported hairstyle against opposite extreme head shapes,
parameter clamps, deterministic piece counts, accessory clearance paths, JSON
round trips, shared body and face topology, animation attachment, expression
preservation, and Player POV stability.

### Mouth validation

Run the focused jaw and speech-shape validation with:

```powershell
godot --headless --path . -- --validate-mouth
```

It checks every mouth shape against opposite extreme face morphs, fixed
topology, repeated full jaw opening, upper/lower-lip controls, smooth mouth and
expression blending, deterministic speech-shape cycling, inner-mouth geometry,
animation playback, hair preservation, and Player POV stability.

### Formation-facing and Player POV validation

Run the focused integration validation with:

```powershell
godot --headless --path . -- --validate-facing-pov
```

It checks Gold and Navy possession directions, mirrored play switching,
formation reset, pre-snap and moving receiver/defender facing, quarterback throw
orientation, POV-only head hiding, simultaneous broadcast head visibility,
whole-body geometry, stabilized tracking through every animation state,
gaze/expression/blink independence, and the caught-football hand anchor.

### Interactive Player POV validation

Run the focused mode/input validation with:

```powershell
godot --headless --path . -- --validate-pov-free-look
```

It checks Locked Forward compatibility, Free Look clamps and recentering,
player/football/world target tracking, JSON round trips, transient-angle
exclusion, mouse release/recapture, whole-body and broadcast visibility,
animation stabilization, and independence from player facing and eye gaze.

### Action and sideline camera validation

```powershell
godot --headless --path . -- --validate-camera-profiles
```

It checks every action-camera lens and wearable mount, body visibility, Free
Look independence, fixed sideline placement, smooth player/football tracking,
focal-length zoom without translation, route/catch/flag-pull tracking,
POV-to-sideline cuts, and JSON round trips.

### Dialogue and spatial-audio validation

Run the focused dialogue validation with:

```powershell
godot --headless --path . -- --validate-dialogue-audio
```

It checks project JSON round trips, ordered play attachment and reload,
overlapping lines, whisper/shout ranges, mouth-anchor source attachment,
camera-relative listener movement for Player POV and broadcast positions,
speech-mouth cycling, expression/gaze application, and presentation restoration.

### Voice audio and lip-sync validation

Run the focused recorded-voice validation with:

```powershell
godot --headless --path . -- --validate-voice-lip-sync
```

It validates voice-profile, relative-audio-reference, and timestamped-viseme
JSON round trips; ordering, bounds, and overlap rejection; real WAV decoding;
silent unrecorded playback versus explicit preview fallback; generic and manual
lip-sync modes; simultaneous speakers; camera cuts and Player POV during speech;
expression/gaze layering; seeking; play association; and project reload.

### Local TTS and automatic lip-sync validation

```powershell
godot --headless --path . -- --validate-local-tts
```

This uses a deterministic in-process fake provider—so no model or GPU is needed
in CI—to validate CUDA preference plus CPU fallback flags, asynchronous provider
contracts, project-relative collision-safe output, timed phoneme priority,
approximate imported-audio timing, manual-track protection and explicit
replacement, JSON round trips, and clear behavior for an unavailable model.

The current procedural body is deliberately low-poly. It is one skinned mesh
resource with fixed weighted topology, but its material regions are separate,
non-welded surfaces and visible joint or material seams are expected. It does
not yet provide a production-smooth body or cloth deformation. The face is a
rigid head-bone-attached, fixed-topology low-poly mesh with separately generated
eyes, brows, lips, nose, and ears. Expressions move only the procedural brows,
mouth, and eyelids; they do not provide a facial bone rig or full cheek/jaw skin
deformation. Eyeballs, irises, pupils, and lids are simple reusable primitives,
with no eyelid curvature fitting, tear line, eye moisture, corneal refraction,
or convergence model. Mouth opening is vertex deformation rather than a true
jaw bone or oral rig. The inner mouth is a dark flattened primitive and teeth
are simple blocks layered in front of it; there are no gums, individual teeth,
tongue, mouth collision, phoneme extraction, full lip sync, audio synthesis,
wrinkles, or photorealistic skin shaders. Extreme settings are deterministic
but can still look stylized, angular, or show seams and minor overlap where
feature surfaces meet. AI dialogue, photo reconstruction, cloth simulation,
crowds, 360 output, and production rendering remain future work. Hair is
assembled from rigid low-poly caps, panels, capsules, and curl volumes attached
to the head bone. It has no strands, scalp texture, physics, collision response,
wind, secondary motion, transparency cards, or photorealistic shading.
Clearance for the headband and visor is approximate; extreme face, hair, and
accessory combinations may still show small gaps, intersections, hard seams, or
exaggerated silhouettes.

First-person presentation uses render layers rather than a separate body mesh.
The current procedural torso, limbs, and hands remain low-poly, and extreme
animation poses can still bring shoulders or hands close to the near plane.
Free Look has no physical neck/torso follow-through, target tracking respects the
same realistic yaw/pitch clamps and therefore cannot center targets directly
behind the player, and controller response uses a fixed Input Map deadzone.
Stabilization does not implement physical head inertia, collision avoidance, or
a separate first-person animation set. SuperView/fisheye is currently an FOV
expansion rather than a post-process distortion shader. Horizontal and vertical
action-camera FOV are saved together, but Godot renders the vertical value and
derives actual horizontal projection from viewport aspect ratio. Sideline
auto-framing uses target distance rather than image-space bounds; there is no
focus pull, depth-of-field, lens breathing, camera collision, tripod geometry,
or recorded operator keyframe track yet.

Dialogue can play imported or locally generated Piper speech, but recording and
external editing happen outside the application. Piper is optional and must be
installed/configured by the user; bundled voices and model management are not
provided. Its alignment output is phoneme-duration timing, not forced alignment
against recorded performances. Reference-audio, style, emotion, and pitch are
stored as engine-agnostic profile metadata, but the initial Piper adapter does
not support cloning/style conditioning and Piper pitch is not independently
resampled. There is no automatic transcription, time-stretching, waveform display, voice mixing/mastering,
occlusion, reverb zones, subtitles, or localization pipeline yet. Speaking-rate
is metadata only; pitch uses Godot's playback pitch control and therefore also
affects playback speed. Lines without manual events use an explicitly labelled
generic viseme cycle unless automatic approximation was requested. Unrecorded lines are silent during
normal playback and use a deterministic tone only for explicit preview. Speech
styles still use simple range multipliers rather than acoustic simulation, and
expression/gaze restoration returns to the prior visible direction rather than
resuming a formerly tracked moving gaze target.

The initial football simulator is an authored filmmaking system, not a full
rules engine. Route classification is inferred from waypoint geometry, player
speed and acceleration use position-based assignment defaults, zone landmarks
and one eligible rusher are selected deterministically, and catch, drop,
interception, touchdown, and boundary results follow the director's saved
outcome rather than collision probability. Ball flight is an authored arc, not
rigid-body aerodynamics; blocking, contact, penalties, lateral pitches,
adaptive quarterback reads, stochastic skill checks, and officiating remain
out of scope.
