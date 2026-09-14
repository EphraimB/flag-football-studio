# Visual Presentation and Environment

[Back to README](../README.md) · [Studio Guide](STUDIO_GUIDE.md) ·
[Validation](VALIDATION.md)

Field, venue, spectators, lighting, and materials are presentation-only. They
do not change field dimensions, simulation coordinates, timing, or outcomes.

## Shared materials

`StudioMaterialLibrary` caches semantic role/color materials for skin, hair,
jersey/shorts fabric, turf, end zones, football leather/laces, flags, shoes, and
markings. Matching combinations reuse resources; procedural normal maps are
shared and quality updates cached resources in place.

Skin uses roughness variation and a modest subsurface-like approximation. Hair
uses restrained specular response. Uniforms use athletic-fabric detail. Turf
combines mowing strips, normal detail, markings, end-zone wordmarks, and fixed
wear patches. The football includes procedural seams and laces.

## Lighting and quality

`SportsLightingController` owns a procedural sky, directional sun, and four
field spotlights. Presets are Day, Golden Hour, Overcast, and Night / Field
Lights.

| Quality | Presentation budget |
| --- | --- |
| Preview | No procedural surface normals or MSAA; capped shadows and reduced venue detail |
| High | Material detail, skin approximation, 2× MSAA, and increased environment detail |
| Final | Stronger detail, anisotropic filtering, 4× MSAA, and largest shadow/venue budget |

Exposure (`0.60–1.40`) and shadow quality can be overridden. These settings are
transient and must not alter simulation or camera FOV.

## Venue architecture and presets

`VenueEnvironment` composes sidelines, benches, standing zones, equipment,
coolers/containers, cones, fencing, walkways, bleachers, field-light fixtures,
and a physical scoreboard. `VenueLayout` aligns Night lighting with visible
fixtures and reserves sideline-camera lanes. Gold and Navy occupy opposite
sidelines. The scoreboard follows score, quarter, and clock.

- **Practice Field:** smallest seating/crowd layout and fixture detail.
- **Community Field:** default community-scale venue.
- **College Field:** more seating rows/sections without changing the field.

Quality and spectator density adjust counts. Spectators and equipment can be
hidden independently.

## Spectators

`ProceduralSpectatorSystem` batches deterministic seated/standing variants into
three shared head/torso/leg `MultiMesh` resources. Stable seat indices vary skin
and clothing. This bounds material/instance growth and reserves usable physical
seating for future viewpoints. Spectators have no crowd AI; reactions are audio
presentation described in [Audio](AUDIO.md#venue-audio-layers).

## Current limitations

- Turf has no blade geometry, displacement, wetness, footprints, or dynamic wear.
- Football seams/laces lack stitched displacement.
- Skies lack modeled clouds, bounce probes, volumetric fog, and grading.
- Lights have no IES profiles or measured lux values.
- Venue primitives lack collision, weathering, signs, rail detail, and authored
  architecture.
- Spectators are rigid silhouettes without faces, animation, accessories, AI,
  reaction motion, or occlusion LOD.
- The scoreboard uses 3D labels rather than emissive pixel panels.

