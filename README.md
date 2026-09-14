# Flag Football Studio

Flag Football Studio is an open-source 3D filmmaking studio for designing,
directing, and rendering fully customizable flag football games.

## Product vision

The long-term goal is a director-first production tool: build teams and players,
author football plays, stage dialogue, choose cameras, and render repeatable game
scenes. It is not intended to become a physics-driven competitive football game.

## Development status

Flag Football Studio is an **architectural prototype** built with Godot 4.7
Mono/.NET and C#. Its procedural visuals, animation, audio, and venue are
development foundations rather than production-quality output.

Implemented foundations include:

- Six focused workspaces: Game, Play, Cameras, Players, Uniforms, and Dialogue.
- A deterministic, director-authored flag-football simulation and 3D playback.
- Top-down formation and route editing with persistent play libraries.
- Reusable cameras, timed cuts, action-camera Player POV, and sideline tracking.
- Procedural customizable humanoids, uniforms, animation, and contact posing.
- Imported voice audio, manual/automatic visemes, optional local Piper TTS, and
  spatial venue audio.
- Procedural fields, venue presets, spectators, materials, and lighting presets.
- Versioned JSON project persistence and focused headless validation tools.

Planned work such as production rendering, realistic assets, advanced cloth and
physics, AI-driven behavior, animated crowds, and 360 output is not implemented.
A full product roadmap will be created as a separate milestone.

## Quick start

Requirements:

- Godot 4.7 Mono/.NET
- .NET 8 SDK
- Optional: 64-bit Python and Piper for local speech generation

Open `project.godot` in Godot and press **F5**, or build and launch from a shell:

```powershell
dotnet build
godot --path .
```

The main scene is `scenes/studio/main.tscn`. Choose a workspace, edit the active
project, and use **Run Play** to preview the deterministic simulation.

See [Getting Started](docs/GETTING_STARTED.md) for setup, persistence, TTS, and
headless launch details.

## Architecture at a glance

Godot-independent C# models and services describe projects, teams, plays,
cameras, dialogue, appearances, and simulation. Godot presentation adapters
consume that data to render, animate, play audio, and provide editor UI.

The central invariant is:

> Presentation may visualize authoritative simulation state, but it must not
> change simulation timing, positions, possession, or outcomes.

Project JSON persists authored production data. Transient presentation state,
such as live camera interpolation, animation blends, audio playback, and venue
quality settings, is not persisted.

## Documentation

| Guide | Contents |
| --- | --- |
| [Getting Started](docs/GETTING_STARTED.md) | Requirements, build, launch, persistence, and optional TTS setup |
| [Architecture](docs/ARCHITECTURE.md) | Boundaries, data flow, repository organization, and persistence |
| [Studio Guide](docs/STUDIO_GUIDE.md) | Game, Play, Cameras, Players, Uniforms, and Dialogue workflows |
| [Simulation](docs/SIMULATION.md) | Deterministic play model, outcomes, animation integration, and diagnostics |
| [Cameras](docs/CAMERAS.md) | Camera definitions, cuts, Player POV, sideline operation, and limitations |
| [Characters](docs/CHARACTERS.md) | Humanoid rig, customization, face/hair/mouth systems, and animation |
| [Audio](docs/AUDIO.md) | Voice assets, lip sync, Piper, spatial dialogue, ambience, and diagnostics |
| [Environment](docs/ENVIRONMENT.md) | Field, materials, lighting, venue presets, spectators, and quality |
| [Validation](docs/VALIDATION.md) | Headless validators, diagnostic commands, and regression coverage |
