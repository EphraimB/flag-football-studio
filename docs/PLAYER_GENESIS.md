# Player Genesis

[Back to README](../README.md) · [Characters](CHARACTERS.md) ·
[Digital-human migration](DIGITAL_HUMAN_MIGRATION.md) · [Validation](VALIDATION.md)

Player Genesis is the deterministic identity and creation-workflow foundation
for reusable players. Its long-term goal is a natural, locally directed creation
experience in which a person progressively materializes in one persistent 3D
viewport. This milestone does not include AI, likeness reconstruction,
photorealistic humans, or voice training.

## Stage flow

```text
Blueprint
  → Body Frame
  → Anatomical Form
  → Identity / Appearance
  → Hair / Details
  → Clothing / Uniform
  → Personality / Voice
  → Complete
```

Stages belong to the same persistent player identity. **Back** and **Continue**
change the creation stage; they do not generate a new player. Existing projects
migrate safely to **Complete** so loading an older file does not unexpectedly
turn every roster member into a hologram. **Begin Genesis** starts the selected
player at Blueprint.

The persisted stage is distinct from transient operation states:

```text
Waiting → Processing → Applying → Materializing → Ready
                                             └→ Failed (with a reason)
```

`GenesisStageController` supplies this model without depending on Godot. No
inference work occurs yet; the transient states are ready for bounded future
background operations and are intentionally not saved.

## Facts versus derived identity

`PlayerPhysicalFacts` contains information that a user can know directly:

- Height in metres
- Weight in kilograms
- Age
- Football position
- Dominant hand

Height and weight are facts, not suggestions for a future model. Semantic locks
can prevent a generated edit plan from modifying them. The current UI also
offers a simple Lean, Athletic, Muscular, Stocky, or Average body description.

`CharacterSpecification` stores derived visual identity separately. It is
versioned, Godot-independent, and contains semantic values in these categories:

| Category | Examples |
| --- | --- |
| Body identity | Frame/build, shoulders, torso, hips, limb proportions |
| Face identity | Head, jaw, chin, cheeks, eyes, nose, mouth, ears |
| Skin identity | Skin tone; future project-relative detail assets |
| Eye identity | Eye color; future production eye parameters |
| Hair identity | Style, length, volume, hairline, part, curl, color, tied styles |
| Clothing identity | Semantic presentation choice; `UniformDefinition` remains authoritative |
| Accessory identity | Existing supported accessory flags |

Animation, expression, gaze, blink, viseme, dialogue, and live camera state are
not character identity. Smiling or speaking therefore cannot change the stored
face. A hairstyle edit changes hair properties only.

The specification can retain safe project-relative references for future custom
head identity, facial textures, morph data, hair assets, and detail maps. These
references do not create a separate “user character” type.

## Semantic editing

Future tools operate on validated paths such as:

```text
facts.height_meters
body.frame
face.jaw_width
skin.tone
eyes.color
hair.curl_amount
clothing.presentation
accessories.flags
```

`CharacterSemanticPropertyRegistry` defines type, category, ranges, and allowed
values. `CharacterEditPlan` groups one or more requested operations under a
transaction ID and records affected categories, validation, and optional status
text. Supported operation families include setting facts, adjusting body/face,
setting appearance/hair/uniform/accessory concepts, and locking/unlocking
properties or categories.

`CharacterEditService` applies an entire plan to a clone. Invalid or locked
operations reject the transaction without partially changing identity.
`CharacterEditHistory` stores exact before/after specifications, supports grouped
undo/redo, and is transient by design. Locks and the resulting specification are
persisted; the undo stack is not.

`CharacterSpecificationAppearanceAdapter` currently maps semantic identity to
the established `PlayerAppearance`, `FaceAppearance`, and `HairAppearance`
models. It is the temporary bridge to the procedural renderer. A future imported
digital human will consume the same specification through
`CharacterVisualController` rather than changing the Genesis data model.

## Personality and voice separation

Each player identity has three independent persisted branches:

```text
Player
  ├─ CharacterSpecification
  ├─ PlayerPersonalityProfile
  └─ PlayerVoiceProfile
```

`PlayerPersonalityProfile` stores normalized continuous traits for confidence,
talkativeness, competitiveness, encouragement, playfulness, emotional intensity,
calmness, and leadership. Quiet, Supportive, Tactical, Energetic, Competitive,
or Balanced labels are derived summaries, not replacements for those values.

Personality is never inferred from name, body, face, skin, hair, or voice.
Changing personality cannot modify `CharacterSpecification` or
`PlayerVoiceProfile`.

`PersonalityPresentationMapper` is the explicit one-way boundary for future
body-language, delivery, celebration, frustration, and ambient-speech tuning.
It receives only a personality profile and returns presentation parameters. It
has no access to `PlayDefinition`, simulator frames, outcomes, score, possession,
or timing. The existing ambient scheduler does not consume personality yet, and
authored Dialogue Director content remains highest priority.

## Future local Character Director

The provider-independent contract is:

```text
User input or safe asset reference
              ↓
ILocalCharacterDirector (future implementation)
              ↓
CharacterDirectionProposal
  ├─ inspectable CharacterEditPlan
  └─ optional PlayerPersonalityEditPlan
              ↓
validation, locks, and user confirmation
              ↓
CharacterSpecification / PlayerPersonalityProfile
              ↓
CharacterVisualController
```

An implementation receives snapshots and semantic input references, not Godot
nodes, shader parameters, or arbitrary scene paths. It proposes a deterministic
plan; the established edit service validates and applies it. No keyword parser,
LLM, image model, text-to-3D model, or cloud provider is registered now.

Future likeness input can be represented by a safe reference-image input plus
project-relative identity assets in the normal specification. Reconstruction,
consent/provenance handling, texture generation, and morph production remain
future work.

## Holographic presentation

The Genesis tab uses the existing central 3D viewport. The selected player can
display a presentation-only `GenesisHologramPresenter` layered over the active
character visual:

- Cyan translucent surface and luminous edge response
- Animated scan-line/grid impression
- Bottom-to-top materialization progression
- Height and body-landmark measurement guide
- Stage label and visible failure tint
- Smooth return to normal materials/transparency at Complete

The presenter uses `CharacterVisualController`; it is not a second character
implementation and owns no identity. Today it safely overlays the procedural
fallback. Later it can overlay the imported modular digital human without
changing stage or edit data. This is a restrained blueprint approximation, not
photorealism or anatomical generation.

## Player POV requirements

Genesis does not alter the eye, mouth, hand, catch, carry, or sole anchors.
Camera-specific head hiding still removes only obstructing head geometry from
Player POV while leaving the body visible. Free Look, action-camera mounts,
spatial mouth audio, football attachments, and broadcast visibility remain under
their existing presentation controllers.

## Implemented now versus future

| Implemented | Future |
| --- | --- |
| Versioned specification and physical facts | Local natural-language/image Character Director |
| Semantic edits, validation, locks, transactions, undo/redo | Likeness reconstruction and custom morph/texture production |
| Persisted personality traits and derived labels | Personality-aware deterministic ambient phrase selection |
| Provider-independent AI request/proposal interfaces | A concrete local model/provider |
| Genesis stage/progress and operation-state models | Long-running progress/cancellation UI for real model work |
| Players-workspace Genesis and Personality tabs | Full roster creation/removal workflow |
| Procedural-character hologram overlay | Imported photorealistic digital-human rendering |
| Safe defaults for older project JSON | Voice design/training |

## Validation

Run:

```powershell
godot --headless --path . -- --validate-player-genesis
```

The validator covers stages, exact facts, locks, semantic operations, atomic
plans, undo/redo, persistence, older-project defaults, personality ranges and
simulation isolation, voice/specification independence, the future director
contract, hologram routing, anchors, workspace presence, and Player POV.
