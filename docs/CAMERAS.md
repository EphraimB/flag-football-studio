# Cameras

[Back to README](../README.md) · [Studio Guide](STUDIO_GUIDE.md) ·
[Validation](VALIDATION.md)

`CameraDefinition` and `CameraCut` are Godot-independent authored data.
`GameProject` persists reusable cameras and per-play timed cuts.
`CameraDirectorController` translates them into Godot placement, lens settings,
tracking, Player POV visibility, and playback cuts.

## Camera types and sequence

Types are Broadcast Wide, Sideline Low, End Zone, Player POV, and Free Camera.
Camera position and lens/zoom remain separate concepts.

The Cameras workspace separates selected-camera properties from the prominent
Camera Sequence. **Run Selected Camera** holds the selection for the play;
**Run With Cuts** runs the ordered sequence. A camera can be added at a time in
seconds or removed. Simulation, cuts, and play dialogue share one timeline, so
switching views does not alter simulation or lip sync.

## Player POV action camera

Player POV is a body-visible wearable camera. Saved settings include:

- GoPro Wide, GoPro Linear, SuperView-style approximation, or Narrow lens.
- Horizontal/vertical FOV and distortion approximation.
- Eye, Forehead, Chest, or Shoulder mount.
- Stabilization, horizon leveling, body/head-bob strength, and smoothing.
- Pitch, forward/up offsets, near clip, and input sensitivities.

Modes are:

- **Locked Forward:** stabilized player-forward view.
- **Free Look:** mouse/controller yaw and pitch layered over the mount without
  turning the player.
- **Look At Target:** smooth camera-only tracking of a player, football, or world
  point without overwriting eye gaze.

Free Look yaw is clamped to ±110 degrees and total pitch to -75 through +65.
**Recenter** returns to player forward. **Escape** releases captured mouse input;
clicking an unhandled viewport area recaptures it. **R** or the controller right
stick recenters by default.

The mount follows a stable rig anchor with smoothing and optional horizon
leveling. Root movement/facing remains football-driven. Gaze, blinking,
expression, and mouth state remain independent.

### First-person visibility

The selected player's face, eyes, mouth, hair, and head accessories use a
dedicated render layer excluded only by the POV camera. Torso, arms, hands,
legs, feet, flags, and caught football stay visible. Other cameras see the full
character. There is no separate first-person body mesh.

## Sideline sports camera

Sideline Low stays near the selected left or right boundary. It supports:

- Physical height and distance.
- Static, Track Player, Track Football, Follow Play Center, and Manual behavior.
- Target, pan, tilt, framing offset, and tracking strength.
- Focal length/FOV, min/max focal length, zoom speed, target size, and auto-zoom.
- Mouse-wheel optical zoom and **Reset Zoom** to 35 mm.

Tracking pans and tilts smoothly without translating with the play. Optical zoom
changes FOV without moving the camera. Venue layout reserves default sideline
operating lanes.

## Other cameras and persistence

Free Camera exposes position, degree rotation, and FOV. Broadcast Wide and End
Zone provide reusable presets. **Preview** reapplies the current definition.

Definitions persist type, transforms/lenses, POV profile, sideline placement,
tracking targets/strengths, zoom settings, and cuts. Free Look angles, recenter
progress, mouse capture, tracking interpolation, and auto-zoom convergence are
transient.

## Current limitations

- SuperView/fisheye is FOV expansion, not a distortion shader.
- Godot renders vertical FOV and derives horizontal projection from aspect ratio.
- Sideline auto-framing uses distance rather than image-space bounds.
- No focus pull, depth of field, lens breathing, camera collision, tripod model,
  or operator keyframe track exists.
- Free Look lacks physical neck/torso follow-through; clamps prevent centering a
  target directly behind the player.
- Stabilization has no physical head inertia, collision avoidance, or separate
  first-person animation set.
- Extreme poses may bring low-poly hands/shoulders close to the near plane.

