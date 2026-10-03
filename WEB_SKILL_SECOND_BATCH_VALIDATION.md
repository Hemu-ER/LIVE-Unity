# Second web skill batch validation

2026-10-03 / Unity6000.6.4f1 / batchmode+nographics Play-mode smoke / exit0. Base commit3bd8e96. Source web HEAD2e4b699e961d43984ff0eba88d691038ea163560 unchanged; inspected only relevant character branches and actual damage return. No compile errors. No UI, scene, roster stats, fixture data, economy rules or synergy changes.

## Implementation

Exactly Kenneth, Abigail, Sua and Marcus added (8 definitions). Existing Isol/Bianca/Garnet/Charlotte unchanged as data. Current catalog32 pairs/64 unique references:8 characters/16 executable skills,24 characters/48 pending.

- Kenneth:5 basic attacks, currentAP×[1,2,4] shield,AP/AS×(1+[.2,.3,.6])5s keyed refresh. Basic actual HP loss×[.1,.15,.25] heal; no skill lifesteal,shield absorption or overkill. Killing-blow lifesteal settles.
- Abigail:after3 surviving-target basic hits,all living enemies AMP×[1,1.5,2.5]; every such hit permanently multiplies current DEF by1−[.05,.08,.15],before the third-hit spin. Killing blows do not count in actual web code.
- Sua:4s then every4s,target row AMP×[1.15,1.6,2.8]; actual total HP loss×[.15,.25,.4] self heal. Every basic hit AMP×[.25,.45,.85] extra on original target then maxHP×[.008,.015,.03] self heal. No transfer of extra damage to a new target after death.
- Marcus:every10s when not CC,all living enemies AP×[1.5,2,3],survivors CC1s+shock. Basic hit consumes living original target shock once for AP×[1.5,2,3.5] additional damage and CC.5s.

## Generic changes

Original-hit effect anchor, optional surviving-hit counter, target-status precondition and atomic consumption on basic-hit triggers, star arrays for self-maxHP healing/damage-based healing. Shield/heal SkillId attribution and appended StatusConsumed event preserve existing event enum ordinals/signatures. Existing range/CC/ratio buff/shield/lifesteal mechanisms reused; no character-name branches.

Precision bug found by independent Sua2 test:float .45 promoted to double produced22.4999994 and rounded22. Opt-in PreserveAuthoredPrecision recovers authored decimal ratios before coefficient math, giving23 at22.5. Only the new four definitions opt in, preserving earlier character/fixture numeric behavior. No coefficient/balance adjustment.

## Automated results

| Suite | Assertions | Result |
|---|---:|---|
| New WebSecondBatchSmokeCheck |151,380|PASS|
| Run economy/shop/pool/mastery/bench/merge/round |7,391|PASS|
| U01–U09 Ability |8,244|PASS|
| AI/BFS/occupancy/range/Dash |221,993|PASS|
| WebRoster/import/shop/purchase/sale/merge |7,838|PASS|
| Mechanisms + Isol |34,976|PASS|
| Bianca/Garnet/Charlotte first batch |130,164|PASS|
| Scene/HP bar/core SmokeCheck |—|PASS|
| Python web source coefficient/trigger check |—|PASS|

All four active/passive pairs are tested independently at1/2/3 stars, plus combined ordering. Boundary checks include fifth/tenth attacks,3/4/5/10 second deadlines,shield-before-rage,nonstacking refresh,shred accumulation before spin,dead original target exclusion,actual damage excluding shield/overkill,CC delay,shock consumption exactly once,CC expiry while shock persists,extra-damage/heal/consumption statistics and events.

Each real character is obtained through funded seeded shop rerolls and Buy,asserted on bench,placed by Move,then Ready; both skills execute in runtime. Temporary test funds/HP are reset; no cloned test character or production stat edits.

24 new mixed3v3 runs (12 seeds×2):22 elimination finishes,2 existing60s timeout Draws. Draw cases had nonzero damage and zero silent-stall detections. Every tick checks unique live occupancy and finite AP; repeated seed matches result/ticks/statistics. Core fixtures invoke the same FinishAsDraw deadline operation as RunController; existing run tests separately verify deadline integration. Existing64 AI,16 Isol and16 first-batch simulations also retained.

## Reproduce

Unity6000.6.4f1 args:`-batchmode -nographics -projectPath <repo> -executeMethod LIVE.Prototype.Editor.PrototypeSmokeCheck.Run -logFile <log>`. Omit `-quit` because Play smoke exits itself.
Source check:`python Tools/verify_web_skills.py <pinned-web-reference-dir>`.
Manual:BattlefieldPrototype.unity→Play/WebRoster→buy one of the four→deploy→Ready. Existing Reset Run resets counters/status/buffs. No UI additions.

## Limits and next work

Unity still uses existing integer HP/rounding,star growth,Manhattan AI/stable action order and basic cooldown scheduling; this is skill-rule fidelity,not full web fight equivalence. Runtime hit context is synchronous; delayed hit-anchored effects are rejected. Shock is shared (web boolean),not source-owned. Existing immediate battle finish cancels new queued skill activations; already resolved damage-based recovery now settles. Other effect chains that kill the final enemy can still be truncated by existing finish behavior. Earlier and new definitions currently use different explicit precision policies; unify only with audited numeric regression expectations. Authored-ratio conversion uses invariant round-trip formatting,which can be cached if profiling warrants it. Defense modifiers remain a per-hit list; future very long/speed-boosted fights may benefit from compact accumulation.

Next recommend Jenny/Ian as a focused lethal-replacement batch:validate first-lethal timing,invulnerability,AP/AS stage changes,lifesteal and death/event/occupancy semantics. Complex mode/summon characters remain separate work.

## Files

New Editor/PrototypeWebSecondBatchSmokeCheck.cs (+meta),this report.
Runtime changed:PrototypeSkillDefinition.cs,PrototypeUnitAbilities.cs,PrototypeUnitMechanisms.cs,PrototypeDamageCalculator.cs,PrototypeCombatEvents.cs,PrototypeGameData.cs.
Data:Resources/CharacterSkills.json (only the requested eight new executable definitions).
Tests:PrototypeSmokeCheck registration;existing Mechanism/FirstBatch/Roster expectations updated for8 characters;source verification script extended.
Docs:WEB_SKILL_COMPATIBILITY.md,WEB_TO_UNITY_MIGRATION.md,PROTOTYPE.md.
