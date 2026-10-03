# First web skill batch validation

2026-10-03, Unity 6000.6.4f1, batchmode/nographics, Play-mode PrototypeSmokeCheck, **exit 0**. Base: e972b82. No compile errors. Existing UI, LIVE.unity, SampleScene.unity, WebRoster stats and U01–U09 fixture JSON unchanged.

## Implemented

Exactly Bianca, Garnet and Charlotte active/passive definitions added to the existing CharacterSkills catalog. Isol retained unchanged. Catalog: 32 character pairs, 64 unique IDs, 8 executable definitions (4 characters), 56 pending (28 characters). These are actual WebRoster definitions used by the existing shop/pool; no cloned test characters or custom character handler.

- Bianca:4s then every8s, current target AMP×[3.3,4.5,7.5]+target maxHP×[.12,.17,.27]. On nonlethal received damage at<=50%HP, once per combat heal own maxHP×.5 immediately and reduce incoming damage90% for3s.
- Garnet:first periodic tick once, nearest enemy; CC1s,permanent DEF×.9,maxHP×[.10,.15,.25] fixed damage,then execute if remainingHP fraction<=[.10,.15,.25]. Passive basic-only reduction[.15,.25,.40].
- Charlotte:every10s all living allies invulnerable1s. Every third basic attack immediately heal self/allies within Chebyshev2 for current AMP×[.60,.90,1.50],AP/AMP×(1+[.10,.15,.30]) for3s with keyed refresh. For each ally, heal precedes buffs; current AMP is reevaluated after buffing the caster, matching the web loop. No healing-song synergy.

## Small reusable extensions

TargetMaxHealthRatioByStar / ExecuteThresholdByStar, NearestEnemy selector, explicit IgnoreRange, opt-in ReactAfterDamage, instant attack-count evaluation after basic hits, opt-in ResolvePerTarget for effect sequencing. Timed buffs use absolute combat expiry and stat reads ignore expired buffs, removing cross-unit tick-order duration drift. Existing default cast behavior is retained.

Appended events (existing enum ordinals/arguments preserved): StatusApplied, BuffApplied, DamagePrevented, UnitExecuted. Heal carries optional SkillId. New DamagePrevented/Executions statistics; existing actual damage/heal/kill contracts remain. Execute is direct death, not fabricated damage, and releases occupancy through existing SetHealth/NotifyDeath.

## Results

| Suite | Assertions | Result |
|---|---:|---|
| New WebBatchSmokeCheck | 130,164 | PASS |
| Existing MechanismSmokeCheck (including Isol) | 34,976 | PASS |
| Economy/mastery/shop/pool/merge/round RunSmokeCheck | 7,391 | PASS |
| U01–U09 AbilitySmokeCheck | 8,244 | PASS |
| Targeting/BFS/range/occupancy/Dash AiSmokeCheck | 221,993 | PASS;64 seeded3v3,zero timeout draws |
| WebRoster import/purchase/sale/bench/deploy/merge/round | 7,838 | PASS |
| Scene/combat PrototypeSmokeCheck | — | PASS |
| Python direct source coefficient/trigger check | — | PASS |

New suite checks all three star levels, timing before/on deadlines, health threshold and lethal exclusions, immediate response, once limit, fixed damage ignoring defense, execute threshold/+1HP, death/occupancy/event counts, basic-vs-skill reduction, Chebyshev diagonal/outside/dead/excluded enemy, self heal, per-target live AMP, third/sixth attack, keyed refresh/expiry, invulnerability expiry and event attribution.

For each of the three real characters: seeded funded rerolls → actual shop Buy → bench → Move onto board → Ready → runtime active/passive observed. Extra test HP/funds stay within disposable test state and are reset; production economy is untouched.

16 mixed3v3 simulations (8 seeds ×2), all finish within60s, every live unit has a unique cell and finite AP. Same seed/state reproduces winner, tick count and existing combat statistics. Existing16 Isol simulations and64 AI simulations also pass.

## Reproduce

Unity args: `-batchmode -nographics -projectPath <repo> -executeMethod LIVE.Prototype.Editor.PrototypeSmokeCheck.Run -logFile <log>`; omit `-quit` because Play smoke exits itself.
`python Tools/verify_web_skills.py <web-reference-dir>` compares against combat-engine.js/game.js/roster.js at compatibility document SHA.
Manual: open BattlefieldPrototype.unity, Play in WebRoster mode, buy desired character, deploy, Ready. Existing Reset Run resets passive limits and timers. No new UI controls.

## Technical debt and next batch

Full web-engine equivalence remains outside scope: existing integer HP/rounding, Unity star growth, Manhattan target distance/target lock, basic attack cadence and per-unit tick ordering remain. Web ally iteration uses units array order; Unity resolves per-target effects in stable CombatId order for deterministic behavior. With Charlotte self-buffs this can affect which allies receive pre/post-buff healing, so reproduction comparisons must align order. Unit death immediately ends an eliminated battle; subsequent same-tick effects are cancelled by the existing controller. Source-specific buff prevention attribution and expiry events are not yet exposed; DamagePrevented records aggregate mitigation, not each contributing buff. Summons/transfer/modes remain pending.

Next: Kenneth/Abigail/Sua/Marcus, verifying attack-count/next-hit and source-owned mark contracts first; then Jenny/Ian lethal replacements. Do not infer completion for any of the remaining28 characters.

## Changed files

Runtime: PrototypeSkillDefinition.cs, PrototypeUnitAbilities.cs, PrototypeUnitMechanisms.cs, PrototypeCombatEvents.cs, PrototypeGameData.cs.
Data: Resources/CharacterSkills.json (six new executable definitions only; Isol unchanged).
Tests: new Editor/PrototypeWebBatchSmokeCheck.cs (+meta); existing PrototypeSmokeCheck.cs registration, Mechanism/Roster catalog expectations, Tools/verify_web_skills.py.
Docs: WEB_SKILL_COMPATIBILITY.md, WEB_TO_UNITY_MIGRATION.md, PROTOTYPE.md, this report.
