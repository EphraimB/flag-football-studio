# Flag Football Studio

Flag Football Studio is an open-source 3D filmmaking studio for designing,
directing, and rendering fully customizable flag football games.

The director prototype demonstrates the core architecture with a primitive 3D
field, two five-player teams, a top-down play editor, persistent play and camera
libraries, a game-state scoreboard, configurable camera previews, editable
humanoid player appearances, reusable team uniform libraries, and deterministic
tween-based playback with skeletal animation states. Player appearances now
include deterministic low-poly face customization, blended expression previews,
blinking, gaze control, and reusable procedural hairstyles. It is an
architectural prototype rather than a gameplay simulation.

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
- `HumanoidAnimator` samples idle, jog, sprint, turn, throw, catch, and
  flag-pull poses, blending between states before applying them to the shared
  skeleton. It is presentation-only and does not add animation state to player
  identity or persisted project data.
- `PlaySequenceController` owns deterministic tween-based choreography and
  selects humanoid animation states for each action. The main studio scene
  composes models, views, camera, lighting, and UI.
- `PlayDefinition` is the Godot-independent source of truth for formation
  positions, waypoint routes, defensive assignments, quarterback selection,
  and the intended receiver. Both the 2D editor and 3D playback consume it.
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
  and the existing JSON project file persists them.
- `CameraDirectorPanel` edits camera data. `CameraDirectorController` is the
  presentation adapter that positions the Godot `Camera3D`, attaches Player POV
  views, and applies timed cuts during playback.
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
- For **Free Camera**, edit position, rotation in degrees, and FOV. Changes are
  previewed immediately; **Preview** reapplies the selected camera at any time.
- Enter a time in seconds and choose **Add Cut** to add the selected camera to
  the current play's cut list. Select a cut and choose **Delete Cut** to remove
  it. Timed cuts run when **Run Play** is pressed.

Camera definitions and cuts are included whenever **Save Project** is used and
are restored by **Load Project**.

Player POV cameras attach to the rig's head-relative `EyeAnchor`, so they follow
head animation and remain valid when Player Studio or Uniform Studio rebuilds
the generated presentation.

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

### Humanoid validation

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

The current procedural body is deliberately low-poly. It is one skinned mesh
resource with fixed weighted topology, but its material regions are separate,
non-welded surfaces and visible joint or material seams are expected. It does
not yet provide a production-smooth body or cloth deformation. The face is a
rigid head-bone-attached, fixed-topology low-poly mesh with separately generated
eyes, brows, lips, nose, and ears. Expressions move only the procedural brows,
mouth, and eyelids; they do not provide a facial bone rig or full cheek/jaw skin
deformation. Eyeballs, irises, pupils, and lids are simple reusable primitives,
with no eyelid curvature fitting, tear line, eye moisture, corneal refraction,
or convergence model. There are no wrinkles, teeth, tongue, lip sync, speech
animation, or photorealistic skin shaders. Extreme settings are safe and
deterministic but can still look stylized, angular, or show seams and minor
overlap where feature surfaces meet. AI face generation, photo reconstruction,
cloth simulation, dialogue, crowds, 360 output, and production rendering remain
future work. Hair is assembled from rigid low-poly caps, panels, capsules, and
curl volumes attached to the head bone. It has no strands, scalp texture,
physics, collision response, wind, secondary motion, transparency cards, or
photorealistic shading. Clearance for the headband and visor is approximate;
extreme face, hair, and accessory combinations may still show small gaps,
intersections, hard seams, or exaggerated silhouettes.
