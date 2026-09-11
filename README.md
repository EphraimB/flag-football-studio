# Flag Football Studio

Flag Football Studio is an open-source 3D filmmaking studio for designing,
directing, and rendering fully customizable flag football games.

The first vertical slice demonstrates the core architecture with a primitive
3D field, two five-player teams, and one deliberately scripted flag-football
play. It is an architectural prototype rather than a gameplay simulation.

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

The prototype intentionally uses only Godot primitive geometry. Player Creator,
Jersey Editor, AI, dialogue, crowds, procedural humans, advanced physics,
final animation, and production rendering remain future work.
