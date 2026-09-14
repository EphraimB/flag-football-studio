# Studio Guide

[Back to README](../README.md) · [Getting Started](GETTING_STARTED.md) ·
[Architecture](ARCHITECTURE.md)

Flag Football Studio organizes editing into six workspaces while keeping the 3D
preview central. Switching workspaces changes the visible controls, not the
project or active play.

## Game

The Game workspace presents the scoreboard, project/play library, visual
environment, and ambience settings.

### Play library and persistence

- Select a play to update both the 2D editor and 3D preview.
- **New** creates a play from the prototype formation.
- **Rename**, **Duplicate**, and **Delete** manage the ordered library. Duplicate
  performs a deep copy; at least one play must remain.
- **Save Project** and **Load Project** use the default versioned JSON slot.

### Visuals and environment

- Lighting: Day, Golden Hour, Overcast, or Night / Field Lights.
- Quality: Preview, High, or Final.
- Shadow override: Low, Medium, or High.
- Exposure: `0.60` to `1.40`.
- Venue: Practice Field, Community Field, or College Field.
- Spectator density and independent spectator/equipment visibility.

These settings alter presentation only and are currently transient. The venue
does not change field dimensions or simulation coordinates. See
[Environment](ENVIRONMENT.md).

### Audio and ambience

Controls set crowd, sideline, player-chatter, and action-SFX volume and toggle
ambient conversations or crowd reactions. Diagnostic controls exercise raw,
forced-near, and production spatial paths. See [Audio](AUDIO.md).

## Play

### Formation and assignments

- **Move:** drag any Gold or Navy marker into formation.
- **Route:** select an offensive marker and click field locations to append
  waypoints. **Clear Route** removes that route.
- **Coverage:** select a defender and then the offensive player it covers.
- **Set QB:** choose the snap recipient.
- **Target:** choose the intended receiver.
- **Reset:** restore the active play's formation and derived facing.
- **Run Play:** simulate and preview the active `PlayDefinition`.

Positive play-field Y points upfield and maps to world positive Z. Marker
direction ticks match the derived 3D formation facing. Possession determines
which team attacks in which direction; rotations are not redundantly saved per
player.

### Simulation direction

The compact simulation controls edit pre-snap delay, time-based or
route-milestone throw trigger, throw time, short/medium/deep pass arc, explicit
outcome, defensive reaction, and rusher release delay. These values are stored
with each play. See [Simulation](SIMULATION.md).

## Cameras

The Cameras workspace separates selected-camera properties from the prominent
Camera Sequence list.

- **New**, **Rename**, **Duplicate**, and **Delete** manage project cameras.
- Type supports Broadcast Wide, Sideline Low, End Zone, Player POV, and Free
  Camera.
- **Run Selected Camera** holds the selected view for the play.
- **Run With Cuts** plays the ordered camera sequence.
- Add a selected camera at a time in seconds or delete a cut.

The default `0.0s Broadcast Wide` cut remains visible rather than being hidden
among camera properties. See [Cameras](CAMERAS.md).

## Players

Select any Gold or Navy roster member.

### Body

Edit height, build, shoulder/chest/waist/hip widths, arm and leg length, skin
tone, jersey number, active uniform colors, and headband, wristband, visor, or
arm-sleeve accessories. Jersey-number edits update the roster; team colors
update the active uniform.

### Face

Edit head, jaw, chin, cheek, forehead, eye, brow, nose, mouth, lip, ear, and eye
color parameters. Expression preview supports Neutral, Smile, Focused,
Concerned, Surprised, and Frustrated. Blink/auto-blink and player, football,
manual, or world-space gaze controls are transient presentation previews.

### Hair

Choose None, Buzz Cut, Short, Medium, Long, Curly, Ponytail, Bun, or legacy
Mohawk. Controls cover length, volume, hairline, part, curl/wave, color,
ponytail, and bun parameters.

### Mouth

Preview jaw opening, lip width/fullness, upper/lower-lip offsets, and Rest, A,
E, I, O, U, M/B/P, F/V, L, and W/Q poses. Speech cycling layers with facial
expressions but is not saved.

Appearance changes update the 3D player immediately and persist with the
project. See [Characters](CHARACTERS.md).

## Uniforms

- Choose Gold or Navy.
- Create multiple uniforms and use **Rename**, **Duplicate**, or **Delete**.
- **Set Active** dresses the full team immediately.
- Edit home/away designation, wordmark, player-name toggle, primary, secondary,
  accent, jersey, sleeve/collar trim, number/outline, shorts, and flag colors.

Player jersey numbers remain roster data; uniforms supply team-wide styling.
Each team retains at least one uniform. Libraries and active selections persist.

## Dialogue

### Sequences and lines

- Create Play, Pre Play Huddle, Post Play, or Sideline sequences. Sideline
  sequences are project-wide; other contexts attach to the active play.
- Choose speaker, text, start time, duration, volume, speech style, and radius.
- Optionally select a listener, expression, and gaze target.
- Add/update, reorder, delete, or preview lines.

Play-context dialogue starts with simulation and camera cuts. Switching plays
updates visible sequences and cancels transient playback.

### Voice, audio, and lip sync

- Edit and save per-player voice metadata.
- **Import Audio...** copies WAV, OGG, or MP3 into portable project storage.
- Preview or remove an assignment without deleting its copied file.
- Generate/regenerate local speech explicitly when Piper is configured.
- Add timestamped visemes, preview from time, or request labelled approximate
  automatic timing.
- Raw and forced-near tests isolate decoding from spatial playback.

Dialogue text does not generate speech automatically. Unrecorded lines remain
silent unless the explicit developer preview tone is enabled. See
[Audio](AUDIO.md).

