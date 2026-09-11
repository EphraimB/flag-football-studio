# Flag Football Studio

Flag Football Studio is an open-source 3D filmmaking studio for designing,
directing, and rendering fully customizable flag football games.

The Game Director foundation demonstrates the core architecture with a
primitive 3D field, two five-player teams, a top-down play editor, persistent
play libraries, a game-state scoreboard, and simple tween-based playback. It
is an architectural prototype rather than a gameplay simulation.

## Repository layout

- `assets/` contains source and imported presentation assets, grouped by
  characters, uniforms, footballs, fields, animations, audio, and UI.
- `scenes/` contains Godot scenes, grouped around reusable characters, games,
  studio tools, and UI.
- `src/Football`, `src/Players`, and `src/Teams` contain the plain C# domain
  model. `Player`, `Team`, and `Game` do not depend on Godot.
- Godot-facing presentation classes live beside their feature areas:
  `PlayerPawn` renders a domain player, `FieldView` builds primitive field
  geometry, and `FootballView` renders the placeholder ball.
- `PlaySequenceController` owns the temporary tween-based play choreography.
  The main studio scene composes models, views, camera, lighting, and UI.
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

The prototype intentionally uses only Godot primitive geometry. Player Creator,
Jersey Editor, AI, dialogue, crowds, procedural humans, advanced physics,
final animation, and production rendering remain future work.
