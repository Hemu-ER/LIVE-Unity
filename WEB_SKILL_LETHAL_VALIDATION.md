# Jenny / Ian lethal replacement validation

2026-10-04 / Unity6000.6.4f1 / batchmode+nographics / Play-mode PrototypeSmokeCheck / exit0. Base8440151. No compile errors. Latest web HEAD matches compatibility source2e4b699e961d43984ff0eba88d691038ea163560; only Jenny/Ian timing and state branches revisited. No UI/art/economy/scene/stat/fixture edits.

## Implemented definitions

Jenny Persona:after2 basic attacks reserve the next attack;3rd consumes AMP×[1.5,2,3],4th reserves/5th consumes etc. No extra damage or SkillCast on the second attack itself.
Jenny death replacement:first lethal,HP=maxHP×[.2,.3,.5],invulnerable1.5s,permanent AS×(1+[.3,.5,1]). Web directly mutates AS with no expiry.
Ian bound:start AP×.8 and form state1. First lethal liberation:HP=maxHP×[.4,.5,.7],AP multiplier replaced with1.2,permanent AS×(1+[.5,.75,1.5]),basic actualHP-damage lifesteal[.2,.3,.5],form state2. Not .8×1.2. No invented Ian invulnerability. All buffs/charges reset with combat reset.

## Generic contracts

OnLethalDamage operates before normal SetHealth(0)/NotifyDeath. The once charge is consumed before skill notification/effect execution. HP heal/status/buffs complete before LethalDamageReplaced and final DamageDealt notification. Stop at first successful replacement. Reentrant incoming damage during the synchronous replacement is ignored. Restored integer HP floors at1. Actual incoming HP loss and healing are recorded separately. No UnitDied,kill,retarget,occupancy release or elimination for a successful replacement. Movement interruption cancels only destination reservation via Grid.CancelReservation;current cell stays occupied continuously.

ReserveNextBasic is an opt-in Instant/AfterNAttacks/Self definition option. NextBasicReserved belongs to per-combat runtime. On threshold:set pending and remember attack count;on next actual basic:clear pending,publish consume,resolve effects on original hit target. Consuming does not reset the attack threshold counter. Dead target skips extra damage,never transfers to a fresh target. Reset/death/finish clear pending;lethal survival and CC do not. No character-name switch or custom handler.

Appended event types:NextAttackReserved,NextAttackConsumed,LethalDamageReplaced. Existing event ordinal/constructor contracts preserved. Successful replacement effects still emit existing SkillCast/HealApplied/StatusApplied/BuffApplied.

## Results

| Suite | Passed assertions |
|---|---:|
| New LethalSmokeCheck |176,692|
| Economy/mastery/shop/pool/merge/round |7,391|
| U01–U09 Ability |8,244|
| AI/BFS/occupancy/range/Dash |221,993|
| WebRoster |7,838|
| Mechanisms/Isol |34,976|
| Bianca/Garnet/Charlotte |130,164|
| Kenneth/Abigail/Sua/Marcus |151,380|

PrototypeSmokeCheck scene/core PASS. Python pinned web coefficient/state/reservation verification PASS. Existing tests only updated implemented-catalog expectations;prior numeric/behavior assertions remain.

New checks at1/2/3 stars:Persona1/2/3/4/5 cadence,exact extra damage,reservation persistence through CC/revival/reset,dead target;nonlethal exclusion,exact reviveHP,AS,permanence,1.5s invulnerability,second lethal death,Ian AP transition and actual basic lifesteal/no skill lifesteal. Shared checks verify locked target identity,current occupied cell,no first-death event/kill/finish,normal continued attacks,final death/kill/release,reset charges,first-success priority,moving interruption,1HP floor and observer reentry safety.

24 new seeded mixed3v3 runs(12×2):16 elimination,8 at existing60s Draw. No silent stalls;damage ongoing. Every live cell unique and AP finite;same seed reproduces result,ticks,statistics. Core fixtures call the existing deadline operation;unchanged run regression covers actual RunController timeout integration.

Both actual WebRoster characters tested through funded shop rerolls→Buy→bench→Move→Ready→combat. Ian first unlocks its3-cost shop odds through the real Invest command;no odds/cost override. Test injects one lethal hit into the purchased combat instance and verifies both skills. Production funds/HP/rules are untouched/reset after fixtures.

## Reproduce

Unity args:`-batchmode -nographics -projectPath <repo> -executeMethod LIVE.Prototype.Editor.PrototypeSmokeCheck.Run -logFile <log>`;omit -quit because the Play-mode runner exits itself.
`python Tools/verify_web_skills.py <pinned-reference-dir>`.
Manual:BattlefieldPrototype.unity→Play/WebRoster→buy Jenny or Ian (invest mastery until3-cost offers can appear)→deploy→Ready. Existing Reset Run restores normal initial states. No new UI.

## Status / limitations

Catalog32 pairs/64 IDs:10 characters/20 executable definitions,22 characters/44 pending. Only Jenny/Ian's four entries changed in this patch. Original A/B/C snapshot unchanged. Hyunwoo/Yuki next-hit submechanism now supported;Justina still needs immediate-AoE plus reservation composition validation. No other character port claimed.

This is in-place lethal prevention,not respawning a dead GameObject. Direct execute remains a direct death and bypasses damage-trigger replacements,matching prior Garnet rules. External observer-caused damage inside replacement is deliberately ignored rather than queued. Observers receive effect notifications during the transaction;they should not mutate battle rules. Existing Unity integer HP,star growth,Manhattan/stable action order,cooldown timing and immediate final elimination still differ from complete web simulation. Existing/new definitions retain their explicit precision policies. Limited-use empowerment and projectiles are future work.

Next recommended focused batch:Hyunwoo/Yuki for reservation reuse and resource counters;Justina once the simultaneous immediate AoE/reservation composition is verified.

## Files

New:Editor/PrototypeLethalSmokeCheck.cs(+meta),this report.
Runtime:PrototypeSkillDefinition.cs,PrototypeUnitAbilities.cs,PrototypeUnitMechanisms.cs,PrototypeCombatGrid.cs,PrototypeCombatEvents.cs,PrototypeGameData.cs.
Data:Resources/CharacterSkills.json (four requested definitions only).
Tests:PrototypeSmokeCheck registration;Mechanism/Roster/first/second batch catalog count expectations;Tools/verify_web_skills.py.
Docs:WEB_SKILL_COMPATIBILITY.md,PROTOTYPE.md,WEB_TO_UNITY_MIGRATION.md.
LIVE.unity,SampleScene.unity,WebRoster.json,U01–U09 fixture JSON unchanged.
