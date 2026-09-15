# Character Asset Contract

[Back to README](../README.md) · [Migration plan](DIGITAL_HUMAN_MIGRATION.md) ·
[Characters](CHARACTERS.md) · [Validation](VALIDATION.md)

This is the import contract for the first real reusable player asset. A source
asset must pass this contract before the imported backend can replace the legacy
procedural visual.

## Format and coordinate system

| Item | Contract |
| --- | --- |
| Exchange format | glTF 2.0 binary `.glb` preferred; `.gltf` accepted for development |
| Godot path | Default `res://assets/characters/imported/base_player.glb` |
| Units | 1 authored unit = 1 metre |
| Axes | Y up; character faces Godot forward (`-Z`) |
| Origin | Ground plane, centered between the feet |
| Neutral pose | Symmetric A-pose, palms approximately inward, feet forward and flat |
| Transforms | Apply object scale/rotation before export; root scale must be `(1,1,1)` |
| Runtime dependency | None; Godot must load the exported asset without Blender or an external importer |

GLB is the open, portable default. FBX can be an authoring interchange only; it
must not become the runtime source of truth.

## Scene organization

The imported scene contains one `Skeleton3D` named `RenderSkeleton`. These named
modules are required as descendants; a module may contain more than one mesh but
must have one stable root node:

```text
Body  Head  Eyes  Teeth  Jersey  Shorts  Socks  Shoes
FlagBelt  Flags  HeadVisuals
```

Body, head, eyes, teeth, jersey, shorts, socks, shoes, flag belt/flags, hair, and
accessories must remain independently addressable. Do not merge the whole player
into one opaque material/mesh. Gold and Navy use the same topology and skeleton;
team and player differences come from data, materials, blend shapes, and modular
selections.

## Skeleton

Required deformation bones:

```text
Root Pelvis Spine01 Spine02 Chest Neck Head
LeftClavicle LeftUpperArm LeftForearm LeftHand
RightClavicle RightUpperArm RightForearm RightHand
LeftThigh LeftCalf LeftFoot LeftToe
RightThigh RightCalf RightFoot RightToe
```

Both hands require three-joint chains named with left/right prefixes for:

```text
Thumb01..03  Index01..03  Middle01..03  Ring01..03  Pinky01..03
```

Recommended deformation helpers:

```text
LeftForearmTwist RightForearmTwist
LeftThighTwist RightThighTwist
LeftCalfTwist RightCalfTwist
Jaw
```

All required bones must have unique names, non-zero segment lengths where
applicable, finite transforms, normalized weights, and no vertex with more than
four influences unless the target Godot import path is explicitly validated for
more. Avoid scale animation on deformation bones.

The existing 18-bone procedural hierarchy remains the control rig. The runtime
mapping is defined by `ImportedCharacterAssetContract.LegacyControlToRenderBone`.
An asset with different source names needs an explicit, versioned retarget
profile; fuzzy runtime name matching is not accepted.

## Stable anchors

These `Node3D` anchors are required. They must be descendants of the appropriate
bone/socket and retain stable names across LODs:

| Anchor | Meaning |
| --- | --- |
| `EyeAnchor` | Stabilized midpoint between eyes for POV; never parented to eyeball rotation |
| `MouthAudioAnchor` | Spatial speech source near mouth/head |
| `LeftHandAnchor`, `RightHandAnchor` | Hand interaction references |
| `CatchAnchor` | Center of two-hand catch volume |
| `QuarterbackHoldAnchor` | Two-hand pre-throw ball hold |
| `ThrowAnchor` | Throwing-hand release location |
| `CarryAnchor` | Post-catch ball carry location |
| `LeftSoleAnchor`, `RightSoleAnchor` | Foot-contact sampling/validation points |
| `HairMount` | Head-relative hair root |
| `AccessoryMount` | Stable head accessory socket |

LOD swaps must keep anchor transforms within an authored tolerance; they may not
replace anchors or move them to mesh vertices.

## Blend-shape contract

The shared head topology must expose these exact identity targets:

```text
identity_head_width identity_head_height identity_jaw_width
identity_jaw_height identity_chin_width identity_chin_projection
identity_cheekbone_width identity_cheek_fullness identity_forehead_height
identity_eye_spacing identity_eye_size identity_eye_vertical
identity_brow_height identity_nose_width identity_nose_length
identity_nose_projection identity_mouth_width identity_lip_fullness
identity_ear_size identity_ear_position
```

Expression, lid, mouth, and jaw targets:

```text
expression_smile expression_focused expression_concerned
expression_surprised expression_frustrated
blink_left blink_right jaw_open
viseme_a viseme_e viseme_i viseme_o viseme_u
viseme_mbp viseme_fv viseme_l viseme_wq
```

Neutral/rest is the zero-weight base and therefore has no separate required
shape. Targets must use identical vertex order and count, remain finite at their
authored extrema, and support safe combined evaluation. Corrective shapes for
problematic combinations are encouraged, but they must not change persisted
appearance semantics.

Body customization should use named body blend shapes plus calibrated bone
length/rest offsets. Do not implement height or build by non-uniformly scaling
the entire imported character. The first asset integration must map the existing
height, shoulder, chest, waist, hip, arm, leg, and build values and clamp unsafe
combinations exactly once in the presentation adapter.

## Modular geometry

| Component | Binding |
| --- | --- |
| Body/head | Skinned to `RenderSkeleton`; shared topology across all players |
| Jersey/shorts/socks | Separate skinned garments using the same skeleton/rest pose |
| Shoes | Separate mesh, skinned or rigidly bone-bound with verified foot deformation |
| Eyes/teeth | Separate meshes driven by head, eye, and optional jaw roles |
| Hair | Interchangeable mesh/cards attached to `HairMount`; may add documented hair bones |
| Flag belt/flags | Separate belt/equipment module; optional lightweight flag bones |
| Visor/headband/accessories | Separate named modules attached through stable sockets |

Garments must not encode a jersey number, team identity, or fixed Gold/Navy
colors in geometry. Roster `Player.JerseyNumber` stays authoritative; team style
comes from `UniformDefinition`.

## Materials and textures

Use semantic material slots with stable names:

```text
Skin Eyes Cornea Teeth Hair Jersey Shorts Socks Shoes FlagBelt Flags
```

Use metallic/roughness PBR inputs. Albedo/base-color and emissive textures are
sRGB; normal, roughness, metallic, ambient-occlusion, masks, and height are
linear/non-color data. Prefer portable PNG for source-controlled development and
Godot import compression appropriate to the quality tier. Texture paths must be
project-relative. Do not embed absolute DCC paths.

Recommended naming:

```text
<character-or-garment>_<material>_<map>_<resolution>.<ext>
basehuman_skin_albedo_4k.png
basehuman_skin_normal_4k.png
jersey_base_roughness_2k.png
```

Tint masks must allow current skin tone, hair color, uniform colors, flags,
numbers, trim, and wordmarks to remain data-driven. Close-up skin/eye shaders
may use richer Godot materials, but must degrade predictably at lower quality.

## LOD and performance contract

Use node or mesh suffixes `_LOD0`, `_LOD1`, and `_LOD2`; optional distant
`_LOD3`/impostor data is permitted. All LODs share the same semantic skeleton
roles, modules, material slots, and external anchors.

Initial target budgets, to be measured and revised with the first asset:

| Tier | Intended use | Geometry target |
| --- | --- | --- |
| LOD0 | Player POV, close dialogue, hero shots | 80k–150k triangles for complete clothed player |
| LOD1 | Active play and medium cameras | 35k–70k triangles |
| LOD2 | Sideline/background players | 10k–25k triangles |
| LOD3/impostor | Distant non-hero figures | Under 5k triangles or impostor |

These are contract targets, not implemented counts. Texture residency, material
draw calls, blend-shape cost, skeleton count, shadow casting, and update rate
must be profiled on the RTX 3080 Ti target and a representative CPU fallback.

## Player POV rules

The full character remains visible to broadcast/sideline cameras. Player POV
must exclude only head-obstructing visual groups (`Head`, eyes, teeth, hair,
head accessories) from its camera layer while keeping torso, arms, hands, legs,
flags, and football visible. `EyeAnchor` follows head motion with camera-side
stabilization; eyeball gaze, blink, expression, and visemes must never rotate it.

The head modules must therefore remain separately cullable. A monolithic body
mesh with head vertices in the same unseparable surface does not satisfy this
contract unless it has a verified per-camera visibility solution.

## Import validation and fallback

Run:

```powershell
godot --headless --path . -- --validate-character-visuals
```

`ImportedCharacterAssetValidator` checks existence/loadability, the render
skeleton, skinned meshes, required node names, bone names, anchors, and blend
shapes. Missing twist helpers and disabled shadows are warnings. Missing required
content is incompatible.

Until a contract-complete asset and retarget binder are integrated, the runtime
must log a concise reason and use `ProceduralLegacy`. It must not assemble a fake
human from primitives, silently accept an incomplete asset, download content, or
change player/project data.
