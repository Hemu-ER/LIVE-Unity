# Chebyshev board geometry validation

Validated on 2026-10-05 with Unity 6000.6.4f1, batchmode/nographics, Play Mode.
Entry point: `LIVE.Prototype.Editor.PrototypeSmokeCheck.Run` (omit `-quit`).
Unity exit code: **0**. No C# compilation errors or test/runtime exceptions in the final run.

## Audit and implementation

- `PrototypeCombatGrid.Distance` previously summed row/column deltas (Manhattan).
  It now returns `max(abs(rowDelta), abs(columnDelta))` (Chebyshev).
- Basic attack range (`PrototypeUnit.Tick`), nearest targeting
  (`PrototypeCombatController.CompareTargets`), skill range (`InSkillRange`),
  and Dash already called this shared function; no action/target controller rewrite.
- `PrototypeCombatGrid.TryFindStep` previously enumerated only four orthogonal
  neighbours. It now enumerates eight, each with one step cost. BFS still finds a
  shortest reachable attack position and sorts neighbours by target distance, row,
  then column. Targets still sort by distance, current HP, then stable CombatId.
- `TryReserveStep` now accepts a diagonal distance-one destination. Occupancy and
  reservations are unchanged: origin stays occupied during interpolation, destination
  is reserved, and completion transfers occupancy. No occupied/reserved destination
  can be entered or traversed. Diagonal corner cutting is explicitly allowed even if
  both orthogonal side cells are occupied; units are not walls. Dash reuses these rules.
- Diagonal interpolation uses the existing one-step progress/MoveSpeed, so it takes
  the same time as an orthogonal step (no world-distance multiplier).
- `PrototypeUnitMechanisms.EffectTargets` formerly used Manhattan for unflagged
  AdjacentEnemies/NearbyAllies and a separate Chebyshev expression for flagged effects.
  Both now call the shared function. The serialized ChebyshevRadius flag is retained.
  Explicit Chebyshev effects retain their range; rows/columns/all-target areas are unchanged.
- UI, character definitions, coefficients, scenes, economy and attack timing were not changed.

## Focused tests

`PrototypeDistanceSmokeCheck`: **348 assertions**. Includes all 324 ordered pairs of
3x6 cells; requested distances 1/1/2; immediate diagonal melee without movement;
diagonal versus orthogonal target ties in both registration orders; HP and stable ID
precedence; one diagonal BFS step past two occupied orthogonal neighbours; destination
reservation collision; interpolation and exact occupancy after 1/MoveSpeed; origin
release; reset; occupied-boundary rejection; diagonal skill range and radius exclusion.
The existing Web batch suite additionally validates Charlotte's explicit diagonal
Chebyshev radius-two heal/buff and outside-radius/dead exclusions at 1/2/3 stars.

## Existing regression fixture adjustments

- Ability retarget: move the spare enemy one column farther, retaining a strictly
  farther target within skill range four instead of a new Chebyshev stable-ID tie.
- AI collisions: place both movers and blockers to contest the same destination
  under eight-way routing; surround all eight neighbours for the fully blocked Dash
  case; keep the ranged target's Dash destination genuinely outside range three.
- Original board test: expect the shorter diagonal route and inspect the actual BFS
  reservation when checking reset/death cleanup rather than an obsolete hardcoded cell.
- Generic AoE and Justina: place the other-row enemy below the intended anchor so
  equal Chebyshev distance preserves the anchor's lower stable ID. Damage, coefficient,
  ordering, row exclusion and reservation assertions remain intact.
- First Web batch mixed simulation previously required elimination before 60 seconds.
  New geometry produced active long fights (diagnosed seed 1: 44/45 basic attacks by
  surviving opponents, zero stalls), not blocked paths. Its fixture now applies the
  existing RunController 60-second Draw operation, just like later character suites.
  Before a timeout draw it checks the full deadline, zero stalls and positive damage;
  deterministic repeated outcomes are still required. No runtime deadline changed.

## Final full regression results

Assertion counts include per-tick invariants, not independent test cases.

| Suite | Passed assertions |
|---|---:|
| DISTANCE | 348 |
| RUN | 8,636 |
| ABILITY | 8,600 |
| AI | 213,065 |
| ROSTER | 7,838 |
| MECHANISM | 32,431 |
| WEB_BATCH | 172,058 |
| WEB_SECOND_BATCH | 150,168 |
| LETHAL | 154,512 |
| CHARGE | 110,629 |
| JUSTINA | 100,345 |
| LAURA | 86,109 |
| NADINE | 119,341 |
| UI | 140 |
| **Total** | **1,164,220** |

The original uncounted board/full-loop smoke checks also passed. Coverage includes
all U01-U09, 15 implemented real characters, shop/pool/bench/deployment/merges,
economy/rounds, BFS/collisions/range, skills, reservations, stats and UI regression.
AI: 64 seeded 3v3 simulations with reversed registration, zero timeout draws.
First Web batch: 16 simulations, 10 legitimate deadline draws (seeds 1,2,4,5,7 twice).
Nadine mixed suite: 24 simulations, four deadline draws. Other reported later
character suites had zero deadline draws. Repeated seed/state results matched.

Only the two runtime geometry files changed; the remaining C# changes are tests.
The geometry intentionally changes paths, selected targets and some combat outcomes.
Historical web-only orthogonal effect descriptions remain documented; any future
explicit orthogonal shape needs a separate shape definition rather than a Manhattan
exception to the common board metric.
