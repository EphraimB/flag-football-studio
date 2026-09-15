# Getting Started

[Back to README](../README.md) · [Studio Guide](STUDIO_GUIDE.md) ·
[Validation](VALIDATION.md)

## Requirements

- Godot 4.7 Mono/.NET. The project currently targets Godot.NET.Sdk 4.7.2.
- .NET 8 SDK for desktop builds.
- A renderer supported by Godot Forward+.
- Optional: supported 64-bit Python with the Windows `py` launcher for local
  Piper speech generation.

No external dependency is required for the core studio prototype.

## Build and run

From the repository root:

```powershell
dotnet build
godot --path .
```

Alternatively, open `project.godot` in the Godot editor and press **F5**. The
configured entry point is `scenes/studio/main.tscn`.

The first launch creates a Gold-versus-Navy prototype project. Use the workspace
tabs to edit it and **Run Play** in the Play workspace to preview the active
play. See the [Studio Guide](STUDIO_GUIDE.md) for each workspace.

## Saving and loading

**Save Project** writes the game state and production libraries to versioned
JSON. **Load Project** restores it and selects its first play. The default slot
is:

```text
user://flag-football-studio/game-project.json
```

The actual operating-system path is resolved by Godot. Audio imported for a
dialogue line is copied into an `audio/` folder beside that JSON. Generated WAV
files use `audio/generated/`. Move the JSON and its sibling `audio/` directory
together when transferring a project between computers.

## Optional local TTS

Piper is optional. It is not installed automatically, and the application does
not download voice models.

Install a supported 64-bit Python, then on an NVIDIA/RTX Windows machine run:

```powershell
./tools/tts/setup.ps1 -Runtime Cuda
```

For CPU-only setup:

```powershell
./tools/tts/setup.ps1 -Runtime Cpu
```

The setup script creates `tools/tts/.venv`, installs Piper and the selected ONNX
runtime, and prints an explicit voice-download command. Put the chosen model in
the ignored `tools/tts/models/` directory. Its adjacent `.onnx.json` file is
required.

In the **Players** workspace, select a player and open **Voice**. Choose Piper,
click **Refresh Models**, select a discovered complete model pair, adjust the
speaker/rate/pitch/volume metadata, and click **Save Voice**. Use **Test Voice**
to generate and spatially preview a short phrase. Repeat per player or use the
explicit copy/paste controls; the studio never assigns one generic voice to the
roster automatically.

The bridge prefers `CUDAExecutionProvider` and retries on CPU if CUDA
initialization fails. Missing runtime, model, or configuration errors are shown
in the UI; they do not trigger downloads. Authored speech files are generated
after an explicit Dialogue Director generation action; Player Studio voice tests
are also explicit. Ambient phrases request bounded background generation only
for players whose profiles have been explicitly configured.

More detail is available in [Audio](AUDIO.md) and
[`tools/tts/README.md`](../tools/tts/README.md).

## Headless validation

Build before running validators:

```powershell
dotnet build
godot --headless --path . -- --validate-football-simulation
```

The argument after `--` is handled by the studio's main scene. See
[Validation](VALIDATION.md) for the complete validator matrix and diagnostic
commands.

## Common expectations

- This is a prototype; procedural assets are intentionally stylized.
- Lines with text but no recording are silent during normal playback.
- Ambient player phrases use the speaking player's configured local voice and
  remain silent when none is configured; procedural venue beds do not require
  Piper.
- Visual quality, venue, and ambience controls are currently transient studio
  preferences rather than saved project data.
- Authored play outcomes are deterministic. Presentation settings cannot change
  their timing, final positions, or game-state effects.
