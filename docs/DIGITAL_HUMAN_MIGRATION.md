# Digital-Human Migration

[Back to README](../README.md) · [Character asset contract](CHARACTER_ASSET_CONTRACT.md) ·
[Characters](CHARACTERS.md) · [Architecture](ARCHITECTURE.md)

This document records the audited starting point and the migration plan from the
current procedural characters to production-capable digital humans. It is an
architecture milestone, not a claim that a photorealistic human asset is already
in the repository.

## Milestone status

Implemented in this milestone:

- A stable `CharacterVisualController` boundary between `PlayerPawn` and its
  concrete character visual.
- Explicit `ProceduralLegacy` and `ImportedModular` backend requests.
- A GLB/glTF contract validator for skeleton roles, modules, anchors, and blend
  shapes.
- Clean fallback to the current procedural rig when the imported asset is absent
  or incompatible.
- Runtime validation that existing morph, animation, face, contact, anchor, and
  POV paths still pass through the new boundary.

Not implemented:

- A photorealistic character, production textures, imported animation library,
  runtime retarget solver, cloth, grooming, or new shader pipeline.
- An activatable imported backend. No suitable licensed rigged/skinned source
  asset exists in `assets/characters`, so activating an unverified substitute
  would fake completion and risk breaking established behavior.

A scoped local-source check also found three character-looking GLBs outside this
repository. Their glTF metadata contains one static mesh and zero skins,
animations, or morph targets, and no adjacent reuse/redistribution license was
found. They are not valid digital-human sources and were not copied.

Developers can exercise the import path without changing project data:

```powershell
godot --path . -- --character-visual=imported
godot --path . -- --character-visual=imported --character-asset=res://assets/characters/imported/base_player.glb
godot --path . -- --character-visual=legacy
```

The imported request validates the asset and reports why legacy rendering stays
active. These command-line choices and fallback state are presentation-only and
are not persisted.

## Current character audit

### A. Asset and mesh audit

The repository contains no imported human FBX, GLB, glTF, production texture
set, licensed base mesh, or scanned source. `HumanoidSkinnedMesh` constructs the
body at runtime as indexed `ArrayMesh` surfaces. The audited default topology is
approximately 688 vertices and 1,024 triangles across all body/uniform surfaces.
Limbs are ten-sided tubes with four longitudinal rings; hands are ellipsoids and
shoes are boxes.

`HumanoidFaceMesh` adds approximately 538 vertices and 852 triangles. Its head
is a low-resolution procedural sphere combined with separate primitive facial
parts. The face is rigidly attached to the head bone rather than continuously
skinned to a facial deformation rig. There are no modeled fingers, toes, gums,
individual teeth, tear lines, eyelashes, nostrils, tongue, or realistic ear
anatomy.

UVs are generated for primitive surfaces and are adequate for procedural detail,
not for a production scan workflow. Material-region boundaries are not welded,
which can produce visible seams. The topology is deterministic but lacks the
edge flow needed for close facial deformation, shoulder/hip compression, hands,
and wrinkle-preserving cloth.

### B. Skeleton and rig audit

All current players share an 18-bone control/deformation hierarchy:

```text
Root → Hips → Spine → Chest → Neck → Head
             ├─ LeftUpperArm → LeftLowerArm → LeftHand
             ├─ RightUpperArm → RightLowerArm → RightHand
Hips         ├─ LeftUpperLeg → LeftLowerLeg → LeftFoot
             └─ RightUpperLeg → RightLowerLeg → RightFoot
```

This rig is useful as a semantic control rig but is not a production deformation
rig. It lacks clavicles, a multi-segment spine, forearm/thigh/calf twist bones,
separate wrist/ankle roles, toes, finger chains, jaw, and facial joints.
`HumanoidAnimator` writes poses directly using these bone names, so renaming or
inserting bones into the existing hierarchy would break animation, mesh binds,
contact solving, and anchors.

Body proportion controls currently combine bone rest changes and mesh scaling.
They preserve the prototype anchors, but do not provide corrective shapes for
extreme shoulder, elbow, hip, knee, wrist, or ankle deformation.

### C. Shader and material audit

`StudioMaterialLibrary` builds cached `StandardMaterial3D` resources by semantic
role. Procedurally generated normal/roughness detail and tuned roughness/specular
values improve readability and resource reuse. Skin uses a lightweight
subsurface-like approximation; hair and fabrics use role-specific response.

These are real-time prototype materials, not production human shaders. Missing
features include authored albedo/normal/roughness/height maps, micro-normal skin
detail, robust subsurface scattering, specular breakup, cavity detail, eye
cornea/tear film, anisotropic or card-based hair, cloth weave at appropriate
texel density, and per-garment masks. Current color customization must remain the
source of tint/style when better materials arrive.

### D. Animation compatibility audit

Football timing and outcomes come from `FootballPlaySimulator`.
`FootballAnimationQualityLayer` derives presentation cues and
`HumanoidAnimator` procedurally blends idle/readiness, locomotion, cuts, QB,
catch, drop, interception, flag-pull, and celebration poses. Contact and ball
interaction remain presentation-only.

The current direct bone-name pose logic cannot drive an unrelated imported rig
unchanged. Imported animations also cannot simply replace it because simulation
events, cadence, contact locks, ball anchors, and POV stabilization require
known semantic roles. A compatibility mapping and calibrated rest-pose offsets
are mandatory.

### E. Rendering and lighting audit

The project uses Godot Forward+ with shared material caching, procedural sky and
directional/field lighting presets, MSAA/quality controls, and per-camera culling
for Player POV. This is an appropriate real-time base. It does not currently
provide a production skin pipeline, temporal anti-aliasing strategy, strand/card
hair solution, high-resolution shadow policy for close-ups, or dedicated
character LOD/importance management.

## Recommended skeleton strategy

Use the existing 18-bone hierarchy as a **semantic control rig**, and introduce
a richer imported **render/deformation skeleton** behind the character visual
boundary.

This hybrid is safer than either extreme:

- Replacing the 18-bone hierarchy in place would break current animation and
  skin bindings.
- Keeping it as the final deformation skeleton cannot produce credible
  shoulders, twisting limbs, hands, feet, or facial animation.
- A control-to-render mapping preserves established simulation-facing APIs while
  the render rig adds clavicles, spine segments, twist bones, wrists, fingers,
  ankles, toes, jaw, and optional facial joints.

`ImportedCharacterAssetContract.LegacyControlToRenderBone` defines the first
semantic mapping. A future binder should:

1. Evaluate existing animation/contact intent on the control rig.
2. Copy mapped global poses into render-rig local space using explicit rest-pose
   corrections.
3. Distribute twists and spine bends across richer deformation chains.
4. Apply hand/foot/contact targets after base retargeting.
5. Layer identity, expression, blink, and viseme blend shapes without changing
   football timing.

Retarget profiles should be versioned data, never implicit name guessing during
playback.

## Modular character responsibilities

| Module | Responsibility | Recommended technique |
| --- | --- | --- |
| Body | Skin, proportions, neck/limb continuity | One skinned neutral base with corrective blend shapes |
| Head | Identity, expressions, blinks, visemes | Shared fixed topology, blend shapes plus jaw/eye bones |
| Eyes | Sclera, iris, pupil, cornea/tear response | Separate eye meshes driven by eye bones |
| Teeth | Upper/lower dental placeholders | Rigid to head/jaw; replace current blocks |
| Hair | Style and color | Interchangeable mesh/cards; skinned/rigid to head roles |
| Jersey/shorts | Team uniform design | Separate skinned garments using the same render skeleton |
| Socks/shoes | Lower-leg/foot presentation | Separate skinned or rigid modular meshes |
| Flag belt/flags | Flag-football equipment | Belt attachment plus lightweight flag bones/meshes |
| Accessories | Visors, headbands, wrist items | Named attachment sockets; no arbitrary scene lookup |

Player identity remains in `Player`, appearance in `PlayerAppearance`, voice in
`PlayerVoiceProfile`, and team styling in `UniformDefinition`. Imported meshes
and materials interpret those values; they must not become alternate domain
models.

## Migration stages

1. **Asset qualification:** acquire or author a licensed, redistributable GLB
   satisfying [the character asset contract](CHARACTER_ASSET_CONTRACT.md).
2. **Single-character integration:** validate, instantiate, bind semantic
   anchors, apply one idle/run/throw/catch/flag-pull sequence, and prove POV.
3. **Retarget and contact:** map the control rig, calibrate rest corrections,
   distribute twists, and reconnect foot/hand support.
4. **Customization:** map existing body/face values to blend shapes and safe bone
   lengths; retain exact JSON compatibility.
5. **Materials and modules:** add production PBR texture sets and modular team
   garments while keeping roster numbers and uniform styling authoritative.
6. **LOD and crowd scaling:** add validated render LODs, update-frequency tiers,
   and distant impostors without altering animation event timing.
7. **Default-backend decision:** switch the default only after visual, animation,
   POV, persistence, and performance regressions pass on representative hardware.

## Performance direction

Character importance should be camera-aware but presentation-only:

- Hero/POV and close-up speakers: highest mesh/material/face/animation quality.
- Active-play medium shots: reduced mesh LOD, full event animation, bounded face
  updates.
- Sideline and background players: lower mesh LOD and reduced animation/facial
  update rates.
- Distant spectators: existing cheap instancing or future impostors, never full
  digital-human rigs.

LOD changes must not move anchors, change culling ownership, or alter simulation
state. The first real asset should ship with measurable mesh/texture budgets and
at least three geometrically compatible LODs.

## Blocking dependencies

The next truthful visual step requires an externally sourced or authored asset:

- A legally usable human base with redistribution terms compatible with this
  open-source repository.
- A source file editable in Blender or another DCC and an exported GLB matching
  the contract.
- Authored PBR texture sets and modular garments.
- Animation clips or a deliberate procedural-retarget implementation calibrated
  against the render skeleton.

This milestone does not download, generate, or invent those resources.
