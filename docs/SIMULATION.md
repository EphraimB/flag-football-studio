# Football Simulation

[Back to README](../README.md) · [Architecture](ARCHITECTURE.md) ·
[Validation](VALIDATION.md)

The simulator is a deterministic filmmaking system, not a stochastic football
game or general physics engine.

## Authored input

`PlayDefinition` stores exact formation positions, waypoint routes, defensive
assignments, quarterback, intended receiver, snap/throw/reaction/rusher timing,
pass depth/arc, and intended outcome. Route segments and length drive movement,
cuts, progress, and timing; unrouted players do not receive generic routes.

## Fixed-step timeline

`FootballPlaySimulator` produces Godot-independent player frames, assignments,
route progress, ball/possession states, discrete events, and `PlayOutcome`.
Identical data produces identical results.

It covers pre-snap delay, center-to-QB snap, route releases, defensive reaction,
rush, QB receive/drop/set/scan/throw, independent ball flight, catch/drop/
interception, pursuit, boundary/touchdown, and flag pull.

Routes preserve authored slant, out, in, go, curl, corner, post, and arbitrary
waypoint geometry. Man defenders pursue assigned receivers with delayed leverage;
zone defenders occupy landmarks and react to entrants; rushers pursue the QB
after their delay. Post-catch defenders pursue the carrier.

The QB releases on configured time or route milestone and faces the selected
target. Short/medium/deep settings produce distinct timing and arcs. The ball
travels independently until its authored terminal interaction.

## Outcomes and game state

Completion, incompletion, interception, dropped pass, post-catch flag pull,
touchdown, out of bounds, and QB/rusher flag-pull endings have distinct ball
states, movement, interactions, and final positions. Down, distance, possession,
score, and play result update deterministically from the saved outcome.

## Presentation synchronization

`PlaySequenceController` consumes complete frames instead of substituting
generic tweens. Animation, cameras, dialogue, footsteps, impacts, whistles, and
reactions follow the same timeline. Presentation can improve poses and
attachments but cannot change timestamps, player roots, ball flight,
possession, final positions, or outcomes.

## Direction convention

`PlayDirectionResolver` derives attack direction from possession and formation
separation; `FormationFacing` maps it to Godot local `-Z`. Offense faces its end
zone and defense faces offense. Possession changes reverse roles. Resets and
play switches derive facing anew instead of reusing rotations.

Play Director positive Y maps to world positive Z. Direction ticks display the
same 2D/3D convention.

## Diagnostics

```powershell
godot --headless --path . -- --diagnose-football-simulation
```

The trace logs formation, route waypoints/classification/progress, defensive
targets, QB release, pass target, sampled ball path, and final outcome.

## Current limitations

- Route classification is inferred; player ratings are assignment defaults.
- Zone landmarks and the eligible rusher are deterministic selections.
- Outcomes are authored rather than collision-probability results.
- Ball flight is an authored arc, not rigid-body aerodynamics.
- Blocking, contact rules, penalties, laterals, adaptive reads, stochastic skill
  checks, and officiating are not implemented.

