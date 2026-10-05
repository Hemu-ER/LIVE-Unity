# L.I.V.E. item foundation

## Scope and data

`Assets/Prototype/Resources/PrototypeItems.json` contains exactly seven bases, 28
completed definitions and every unordered pair (including equal materials). No unique
combat effects execute in this patch. Since completed-item stat overrides were not
specified, their ordinary modifiers are the sum of their two materials. This is explicit
data, not a recursive crafting rule; completed items cannot be crafting inputs.

| Base | Modifier |
|---|---|
| 검집 | AP +10 |
| 금팔찌 | Skill amplification +15 |
| 방탄 조끼 | Max HP +100 |
| 사슬 갑옷 | Defense +10 |
| 화살통 | Attack speed +10% of base/star attack speed |
| 전자 부품 | Critical chance +10 percentage points |
| 철사 | Flat defense penetration +8 |

Definitions include stable ID, display name, base/completed flag, sale price, modifier
array, AssetKey, EffectId and EffectDescription. Prices are 2/5 Credits. Catalog validation
rejects duplicates, invalid inputs/results and incomplete recipe counts. Recipe lookup is
order-independent. `AssetKey` is `Items/<id>` for future Sprite/PNG replacement under
Resources; current placeholders are diamond glyphs and names, not final art.

## Ownership and commands

- `PrototypeOwnedItem` has a run-local increasing instance ID, definition ID, owner unit
  ID (zero for inventory) and slot. `PrototypeItemInventory` is independent of combat
  GameObjects. Inventory has 12 slots and each owned unit has three equipment slots.
- `PrototypeRunModel` item commands are implemented in `PrototypeRunItems.cs`.
  Controller wrappers synchronize the scene. `EquipItem(itemId, unitId, slot=-1)` also
  transfers directly from another owned unit. Duplicate completed items are legal.
- `UnequipItem`, `CombineItems`, `SellItem` and all equip/transfer operations require Prep.
  Invalid operations leave credits, ownership, slots and revision unchanged (feedback may
  change). Inventory combine retains the first instance/slot and consumes the second.
- A second base equipped to a unit combines into its existing base slot even if all three
  slots are occupied. It never occupies a separate equipment slot alongside another base.
  Explicit target-slot operations reject an occupied unrelated completed-item slot.
- `AcquireItem(definitionId)` is a reward API returning Rejected/Stored/AutoSold. It may
  receive rewards in any phase; if the bag is full only the incoming reward is sold.
  Existing items are untouched. The UI test grant is restricted to Prep.
- Unit sale preflights capacity for ALL equipped items. Insufficient space rejects the
  entire sale before shared-pool, credits, roster or item mutation; no item auto-sale.
- Unit merging preserves the existing deployed/oldest survivor rule and its equipment.
  Absorbed units' equipment returns to inventory without auto-combining or selling. A
  purchase that would cause a merge without enough return space is rejected BEFORE
  consuming its shop reservation or credits, including recursive merges.
- Items survive moving between bench/board, combat death and rounds. Reset Run clears them.

## Derived stats and future effects

`PrototypeItemCatalog.Derive(baseStats, equippedItems)` copies base/star stats and sums
flat modifiers. AS ratios add together, then multiply base/star AS once. Critical chance
adds percentage points then uses the existing [0,1] clamp. Existing combat stat validation
and runtime skill modifiers are reused. Items do not mutate character definitions and do
not get multiplied again by star growth. `Run.DerivedStats(ownedId)` feeds both the selected
unit inspector and actual allied combat-instance construction, in Prep and on Ready.

Future extension points:

1. Add a modifier kind/resolution to `PrototypeItemStat` and `Catalog.Derive` when adding
   supported core stats such as percent AP/AMP/HP/DEF, percent penetration or crit damage.
2. Resolve each equipped instance via `Run.Items.Equipped(ownedId)` and
   `Run.ItemCatalog.Find(item.DefinitionId).EffectId`. Bind a per-combat handler keyed by
   EffectId during `PrototypeRunController.BuildCombatInstances`. Keep duplicate items'
   instance IDs distinct; never key ownership solely by definition ID.
3. Handlers can subscribe to the existing `PrototypeCombatController.EventRaised`
   contract and use the existing generic status/buff/reservation APIs. Their transient
   counters must be fresh per combat and cleaned on death/end/reset. No handler or unique
   damage/buff effect has been invented or enabled here. EffectDescription explicitly says
   pending until the actual design is supplied.

## Playtest UI

Open BattlefieldPrototype and Play. Existing HUD/board/bench/shop/action rectangles stay
in place. Inventory is in the right margin; equipment is below the left unit inspector.

1. In Prep, use `>` to choose a base and `TEST + <name>` to grant it through the reward API.
   This is a development convenience, not a new shop or reward system.
2. Click an item to select it; click it again to clear. Click another inventory base as the
   second selection, then `Combine 2` to craft. The text shows both materials.
3. Select an allied unit, select an item, then `Equip / Transfer`, or click its equipment
   slot. A second base automatically replaces the existing base with the recipe result.
4. Select equipped gear (clear the previous item selection first), then `Return to bag`
   to unequip. To transfer, retain the item selection, select another unit, and Equip.
5. `Sell` in the item panel sells the selected item; the original unit Sell button sells
   the unit and returns its gear only if all fit. Feedback explains rejection.
6. Base/completed glyphs, names, stat summary and sale price are shown. Actions lock
   outside Prep. This is the allowed click fallback; drag-and-drop is not implemented.

## Validation

Unity 6000.6.4f1, batchmode Play Mode:
`LIVE.Prototype.Editor.PrototypeSmokeCheck.Run` (do not pass `-quit`).
Final test exit code 0; no C# compilation errors or test/runtime exceptions.
Item suite: **522 assertions**, including all recipes in both orders, exact base stats,
same-instance rejection, completed-input rejection, capacity and overflow, duplicate gear,
same-slot/automatic crafting, transfer, partial-return rejection with two free slots for
three items, atomic merge preflight, phase locks, reset/round/death persistence, actual
combat stats, deterministic repeated equipped combat and real uGUI pointer commands.

All prior suites passed unchanged in scope: economy/shop/shared pool/merges/rounds,
U01-U09, AI/Chebyshev/BFS/occupancy, all 15 real characters, WebRoster and original UI.
Existing assertions: **1,164,220**; including items: **1,164,742**. Counts include repeated
per-tick invariants, not independent test-case counts.

## Limitations

No unique item effect, drop table, wildlife reward, disk save/load, final icons or drag UI.
Merge preflight mirrors the existing deterministic merge ordering; any future merge-rule
change must update/test both preflight and execution together. Test grants deliberately
remain visible during Prep. Completed ordinary stats currently use material sums and can
be replaced by authored values without changing inventory rules.


## Player visual verification

Development Windows player build and `-live-visual-check <output> -live-items-check`
completed with exit code 0 / VISUAL_PROBE_PASSED. Captured populated inventory and
selected equipment at 1920x1080, 1600x900 and 1280x720, plus Combat and Result. Inspected
Prep captures; long names wrap into two lines, and item panels stay outside the board,
bench and shop. References: `ITEM_VALIDATION/prep-1280x720.png` and
`ITEM_VALIDATION/prep-1920x1080.png`.

Changed/new files: PrototypeItems.json (+meta), PrototypeItemData.cs (+meta),
PrototypeItemInventory.cs (+meta), PrototypeRunItems.cs (+meta), PrototypeItemHud.cs
(+meta), PrototypeItemSmokeCheck.cs (+meta); small integrations in PrototypeRunModel,
PrototypePlayerRoster, PrototypeRunController, PrototypeRunHud, PrototypeSmokeCheck,
PrototypeVisualProbe; this guide, PROTOTYPE.md and the two captures.
