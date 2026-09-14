# Characters, Uniforms, and Animation

[Back to README](../README.md) · [Studio Guide](STUDIO_GUIDE.md) ·
[Validation](VALIDATION.md)

Player identity and appearance are Godot-independent. Presentation classes
translate them into one reusable procedural humanoid used by Gold and Navy.

## Shared humanoid foundation

`HumanoidSkeletonDefinition` defines one 18-bone hierarchy.
`HumanoidSkinnedMesh` builds one reusable indexed `ArrayMesh` and bind-pose
`Skin`; every player shares its topology, weights, and surface layout.

`HumanoidRig` connects the mesh to each skeleton, assigns materials, applies
proportions through bone transforms, and exposes stable eye, mouth, catch, and
hand anchors. Uniform details, flags, labels, and accessories are lightweight
attachments.

## Appearance and uniforms

`PlayerAppearance` stores height, build, shoulder/chest/waist/hip width, arm/leg
length, skin tone, hair, accessories, and face values without Godot types.
Height/build changes preserve topology and anchors.

Jersey number belongs to roster `Player`. `UniformDefinition` stores team-wide
colors, trim, number styling, shorts, flags, wordmark, name toggle, and home/away
designation. `GameProject` persists appearances, uniform libraries, and active
selections; expression, gaze, blink, and speech preview remain transient.

## Face and eyes

`HumanoidFaceMesh` uses one fixed indexed low-poly topology on the head bone.
Parameters cover head, jaw, chin, cheeks, forehead, eyes, brows, nose, mouth,
lips, ears, and eye color.

Dimension/fullness values use `0.75–1.25`; chin projection, eye vertical
position, nose projection, and ear position use `-0.20–0.20`. The model clamps
jaw to head, chin to jaw, cheeks/eye spacing to head, eye size/nose to spacing,
and mouth width to jaw. Player Studio displays the clamped persisted value.

`HumanoidEyeRig` supplies eyeballs, irises, pupils, and lids with independent
clamped gaze. `FacialExpressionController` blends Neutral, Smile, Focused,
Concerned, Surprised, and Frustrated and controls brows/blinks without moving the
POV anchor.

## Hair and mouth

`HumanoidHairRig` builds deterministic low-poly buzz, short, medium, long,
curly, ponytail, bun, and legacy Mohawk geometry around current head dimensions.
Approximate visor/headband clearance is applied.

Hair/tied length uses `0.50–1.50`; general/ponytail volume and bun size use
`0.70–1.40`; hairline `-0.15–0.15`; part `-1.00–1.00`; curl `0.00–1.00`.

`SpeechMouthController` blends Rest, A, E, I, O, U, M/B/P, F/V, L, and W/Q
with jaw and upper/lower-lip controls. Speech layers over expression.
`HumanoidMouthRig` supplies simple inner-mouth and upper/lower teeth geometry.

## Sports animation and contact

`FootballAnimationQualityLayer` derives presentation cues from immutable
simulation frames/events: readiness, acceleration/deceleration, speed-scaled
locomotion, route cuts, QB receive/drop/set/throw, catch preparation,
catch/drop/interception, flag pull, post-catch run, and celebration.

`HumanoidAnimator` blends cues and relates cadence/lean to simulated speed.
Travel-distance gait phase limits sliding. `HumanoidContactSolver` uses
`IGroundSurfaceSampler` for bounded root/foot corrections and short support-foot
locks. `FootballInteractionResolver` selects QB hold, throw hand, two-hand
catch, and carry anchors. Reparenting affects only the rendered football;
simulation owns timing and world position.

## Current limitations

- Low-poly, non-welded material regions can show seams and angular deformation.
- No production body, scanned skin, cloth thickness/wrinkles, or cloth
  simulation exists.
- The face is head-bone-attached rather than a facial rig; cheek/jaw deformation
  is limited.
- Eyes lack corneal refraction, moisture, tear lines, fitted lids, and
  convergence.
- Mouth deformation has block teeth but no gums, individual teeth, tongue, or
  collision.
- Hair has no strands/cards, scalp texture, physics, wind, or secondary motion;
  extreme combinations may intersect.
- Procedural animation is not mocap. Ground support is not full analytic IK and
  currently samples flat terrain without slope/raycast ankle/toe behavior.
- Hand correction lacks elbow/shoulder IK and finger poses, so extreme builds
  can show gaps or stretch.

