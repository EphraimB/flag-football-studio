# Flag Football Studio

Flag Football Studio is an open-source 3D filmmaking studio for designing,
directing, and rendering fully customizable flag football games.

This repository currently contains only the initial project organization. It
does not yet implement gameplay, creation tools, simulation, AI, procedural
characters, dialogue, or rendering pipelines.

## Repository layout

- `assets/` contains source and imported presentation assets, grouped by
  characters, uniforms, footballs, fields, animations, audio, and UI.
- `scenes/` contains Godot scenes, grouped around reusable characters, games,
  studio tools, and UI.
- `src/` contains C# code. Its areas separate football domain rules, players,
  teams, animation, cameras, UI, studio tools, and persistence.
- `data/` contains project-owned, non-code data for teams, uniforms, and
  playbooks.
- `project.godot` and Godot-generated import metadata remain at the project
  root where Godot expects them.

Empty directories contain `.gitkeep` files so the intended architecture can be
versioned before implementation begins.
