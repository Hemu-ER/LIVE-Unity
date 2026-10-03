using System;
using System.Linq;
using UnityEngine;

namespace LIVE.Prototype.Editor
{
    public static class PrototypeRosterSmokeCheck
    {
        private static int assertions;
        public static void Validate(BattlefieldPrototype board)
        {
            assertions = 0;
            var source = PrototypeWebRoster.Load();
            var data = source.CreateGameData();
            Check(source.Entries.Length == 36 && source.Entries.Count(r => r.Playable) == 32, "Pinned web roster counts");
            Check(data.Units.Length == source.Entries.Length && data.Units.Count(u => u.Playable) == 32, "All data and playable count preserved");
            Check(data.Units.Select(u => u.Id).Distinct().Count() == data.Units.Length, "No duplicate stable IDs");
            var pool = new PrototypeSharedPool(data);
            foreach (var row in source.Entries)
            {
                var unit = data.Definition(row.id);
                Check(unit.Id == row.id && unit.DisplayName == row.name && unit.Cost == row.cost, "ID/name/cost preserved: " + row.id);
                Check(unit.Role == row.role && unit.Affiliations.SequenceEqual(row.affiliations), "Role and affiliations preserved: " + row.id);
                Check(unit.Stats.MaxHealth == row.baseStats.hp && unit.Stats.AttackPower == row.baseStats.atk &&
                    unit.Stats.SkillAmplification == row.baseStats.amp && unit.Stats.Defense == row.baseStats.def &&
                    unit.Stats.AttackSpeed == row.baseStats.@as && unit.Stats.AttackRange == row.baseStats.range, "All source stats preserved: " + row.id);
                Check(unit.PveOnly == row.pveOnly && unit.Playable == row.Playable && unit.WebImplemented == row.implemented &&
                    unit.MainStat == row.main && unit.AssetReferenceId == row.asset.sd, "Source metadata preserved: " + row.id);
                Check(unit.DataMode == PrototypeDataMode.WebRoster && unit.Abilities.Skills.Length == (new[] { "isol", "bianca", "garnet", "charlotte" }.Contains(unit.Id) ? 2 : 0) &&
                    (!row.Playable || (!string.IsNullOrEmpty(unit.ActiveDefinitionReference) && !string.IsNullOrEmpty(unit.PassiveDefinitionReference))),
                    "Only four ported characters executable; all playable characters have references");
                if (row.Playable) Check(pool.Capacity(row.id) == data.Rules.CopiesByCost[row.cost - 1], "Existing per-cost capacity");
                else Check(!pool.TryTake(row.id), "PvE cannot be taken from player pool");
            }
            var shop = new PrototypeShop(data, pool, new PrototypeRandom(331));
            for (int level = 1; level <= 20; level++)
            for (int roll = 0; roll < 10; roll++)
            {
                shop.Roll(level);
                Check(shop.Count == 5, "Five slots");
                for (int slot = 0; slot < shop.Count; slot++)
                    Check(data.Definition(shop.Slot(slot)).Playable, "No PvE/test fixture in real shop");
                foreach (var unit in data.Units.Where(u => u.Playable))
                    Check(pool.Available(unit.Id) + Enumerable.Range(0, shop.Count).Count(i => shop.Slot(i) == unit.Id) == pool.Capacity(unit.Id), "Reroll pool conservation");
            }

            var controller = board.RunController;
            controller.SetDataMode(PrototypeDataMode.WebRoster);
            // Fund command-based integration without bypassing shop reservations or merge logic.
            controller.Data.Rules.StartingCredits = 10000;
            controller.ResetRun();
            var run = controller.Run;
            var starter = run.Player.OwnedUnits.Single();
            int credits = run.Player.Credits;
            Check(controller.Reroll() && run.Player.Credits == credits - 2, "Real roster paid reroll");
            string boughtId = run.Shop.Slot(0);
            Check(controller.Buy(0), "Real roster purchase");
            var bought = run.Player.AtBench(0);
            Check(bought != null && bought.DefinitionId == boughtId, "Purchased unit enters bench");
            Check(controller.Move(bought.InstanceId, PrototypeUnitLocation.Board, 0, 0), "Deploy real unit");
            Check(controller.Move(bought.InstanceId, PrototypeUnitLocation.Bench, 1), "Return real unit to bench");
            int stock = run.Pool.Available(boughtId);
            Check(controller.Sell(bought.InstanceId) && run.Player.Find(bought.InstanceId) == null && run.Pool.Available(boughtId) == stock + 1, "Sale returns stock");

            int purchases = 0;
            for (int roll = 0; roll < 1000 && purchases < 2; roll++)
            {
                for (int slot = 0; slot < run.Shop.Count && purchases < 2; slot++)
                    if (run.Shop.Slot(slot) == starter.DefinitionId)
                    { Check(controller.Buy(slot), "Buy identical real character through shop"); purchases++; }
                if (purchases < 2) Check(controller.Reroll(), "Find merge copies with funded reroll");
            }
            Check(purchases == 2 && run.Player.OwnedUnits.Count == 1 && starter.Stars == 2, "Three real copies merge into 2-star, preserving deployed instance");
            var definition = controller.Data.Definition(starter.DefinitionId);
            Check(controller.Label(starter.DefinitionId, 2) == definition.DisplayName + " **", "Real display name reaches board/bench label");
            Check(board.Units.Where(u => u.Faction == "A").Single().MaxHealth == Mathf.RoundToInt(definition.Stats.MaxHealth * 1.8f), "Existing star scaling");
            Check(controller.Ready() && run.Phase == PrototypeRunPhase.Combat, "Enter real roster combat");
            for (int tick = 0; tick < 3100 && run.Phase == PrototypeRunPhase.Combat; tick++) controller.Step(0.02f);
            Check(run.Phase == PrototypeRunPhase.Result && run.History.Count == 1, "Combat ends and awards result");
            Check(board.Units.All(u => u.Statistics.SkillCastCount == 0), "Imported characters use basic combat only");
            Check(board.Units.Any(u => u.Statistics.BasicAttackCount > 0), "Real basic attacks executed");
            controller.Step(controller.Data.Rules.ResultSeconds);
            controller.Step(controller.Data.Rules.TransitionSeconds);
            Check(run.Phase == PrototypeRunPhase.Prep && run.Round == 2 && starter.Stars == 2 && run.Player.Find(starter.InstanceId) != null,
                "Next Prep preserves owned/merged character after disposable combat");
            Check(board.Units.All(u => u.CurrentHealth == u.MaxHealth), "Next Prep rebuilds healthy combat instances");
            controller.SetDataMode(PrototypeDataMode.TestFixtures);
            Check(controller.Data.Units.Length == 9 && controller.Data.Units.All(u => u.Id.StartsWith("test-unit-")), "Fixture mode remains isolated");
            controller.SetDataMode(PrototypeDataMode.WebRoster);
            Check(controller.Run.Player.Credits == 5 && controller.Data.Units.Count(u => u.Playable) == 32, "Real mode reset restores default rules");
            Debug.Log($"ROSTER_SMOKE_CHECK_PASSED: {assertions} assertions; 36 source records, 32 playable, 4 PvE excluded, field parity, shop/reroll/buy/bench/deploy/sell/merge/2-star/combat/next Prep and fixture isolation.");
        }
        private static void Check(bool value, string message)
        { assertions++; if (!value) throw new Exception("Roster smoke: " + message); }
    }
}
