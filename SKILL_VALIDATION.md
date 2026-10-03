# Skill foundation validation

2026-10-03 / Unity 6000.6.4f1 / Windows batchmode + nographics / exit 0.
Source web HEAD verified: 2e4b699e961d43984ff0eba88d691038ea163560.

| Suite | Passed assertions | Coverage |
|---|---:|---|
| MechanismSmokeCheck | 34,976 | 64 IDs/32 pairs, JSON, timers/cooldown, attack-count/hit/health/stack triggers, buff/status expiry, CC/immunity, invulnerability, lethal revive, row/column/adjacent, reduction, immortal expiry, delayed/reset, lifesteal, Isol 1–3 stars and real shop/deploy/combat |
| RunSmokeCheck | 7,391 | Economy, mastery, pool, shop, bench, merges, rounds, timeout |
| AbilitySmokeCheck | 8,244 | U01–U09, stats, events, gauge, skills, deterministic teams |
| AiSmokeCheck | 221,993 | Targeting, BFS, range, occupancy, Dash, casting, 64 seeded 3v3; zero timeout draws |
| RosterSmokeCheck | 7,838 | 36 source records/32 playable/4 PvE excluded; source stats, shop/buy/sell/merge/combat/reset |
| PrototypeSmokeCheck | pass | Play-mode scene, 18 cells, HP bars, combat and reset |
| verify_web_skills.py | pass | Direct web source names/references, Isol coefficient arrays and start/periodic code rules |

New deterministic simulations: 8 seeds × 2 repeats, 3v3 Isol; equal result/tick/statistics per seed, every live cell unique, finite AP, all finish within 60 seconds. These are runtime tests without rendering assertions; no UI/art change was made.

## Reproduce

Run Unity with `-batchmode -nographics -projectPath <repo> -executeMethod LIVE.Prototype.Editor.PrototypeSmokeCheck.Run -logFile <path>`. Omit `-quit`: the Play-mode smoke check exits itself. For source comparison, run `python Tools/verify_web_skills.py <pinned-web-source-directory>` with combat-engine.js, game.js and roster.js at the recorded SHA.

Open BattlefieldPrototype.unity and Play in WebRoster mode. Purchase/deploy 아이솔; passive applies once at combat start; bomb hits all living enemies every10 seconds. Existing Reset Run and fixture mode remain available. The automated funded shop test does not alter production rules.

## Files

New: PrototypeCharacterSkills.cs, PrototypeUnitMechanisms.cs, Resources/CharacterSkills.json, Editor/PrototypeMechanismSmokeCheck.cs (+Unity metas), Tools/verify_web_skills.py, WEB_SKILL_COMPATIBILITY.md, this report.
Changed: BattlefieldPrototype.cs, PrototypeCombatController.cs, PrototypeDamageCalculator.cs, PrototypeGameData.cs, PrototypeRunController.cs, PrototypeSkillDefinition.cs, PrototypeUnitAbilities.cs, PrototypeWebRoster.cs, Editor/PrototypeRosterSmokeCheck.cs, Editor/PrototypeSmokeCheck.cs, PROTOTYPE.md, WEB_TO_UNITY_MIGRATION.md.
LIVE.unity / SampleScene.unity / fixture JSON unchanged.

## Limits / technical debt

Only Isol's two executable definitions are ported. Remaining62 references are pending. Full web combat parity is not claimed: existing Unity star growth, integer HP/rounding, Manhattan AI, attack cadence, role/synergy omissions remain. Dynamic aura, source-owned target marks, damage interception/transfer, real multi-hit basics, summon lifecycle, post-death triggers and bespoke modes need follow-up. The generic instant path intentionally allows passive/timed effects while an action is active; CC execution requires explicit opt-in. Lethal replacement healing publishes before the final damage event; consumers requiring a different transaction ordering need a dedicated pre/post damage contract. Delayed effects cancel with their caster or dead anchor; they do not persist independently after death. Range/area effects are deterministic but future projectile/summon scheduling needs explicit ownership.
