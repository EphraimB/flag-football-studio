# Flag Football Studio

Flag Football Studio is an open-source 3D filmmaking studio for designing,
directing, and rendering fully customizable flag football games.

The director prototype demonstrates the core architecture with a primitive 3D
field, two five-player teams, a top-down play editor, persistent play and camera
libraries, a game-state scoreboard, configurable camera previews, editable
humanoid player appearances, reusable team uniform libraries, and deterministic
tween-based playback with skeletal animation states. It is an architectural
prototype rather than a gameplay simulation.

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
  transforms, and exposes stable eye and catch transforms. Hair, uniform trim,
  flags, labels, and accessories remain lightweight bone-attached details.
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
  optional accessories. Jersey numbers are authoritative on the roster
  `Player`; team-wide styling is authoritative on `UniformDefinition`.
  `GameProject` owns and persists the appearance collection.
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
- Edit height; slim, average, athletic, or heavy build; shoulder, chest, waist,
  and hip width; arm and leg length; skin tone; hair style and color; jersey
  number; and the active team's primary, secondary, and flag colors.
  Jersey-number changes update the roster; team color changes update the active
  uniform.
- Toggle headband, wristbands, visor, and arm-sleeve accessories independently.
- Changes deform the selected skinned player immediately in the 3D preview.

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

The current procedural body is deliberately low-poly. It is one skinned mesh
resource with fixed weighted topology, but its material regions are separate,
non-welded surfaces and visible joint or material seams are expected. It does
not yet provide a production-smooth body, facial edge loops, facial morphs, or
cloth deformation. Realistic faces, facial rigging, cloth simulation, motion
capture, AI mesh generation, dialogue, crowds, 360 output, and production
rendering remain future work.
