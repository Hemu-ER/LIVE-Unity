using System;
using System.Collections.Generic;
using UnityEngine;

namespace LIVE.Prototype.Editor
{
    public static class PrototypeRunSmokeCheck
    {
        private static int assertions;
        public static void Validate(BattlefieldPrototype board)
        {
            assertions = 0;
            var data = PrototypeGameData.Load();
            Check(data.Units.Length >= 9, "At least nine data-defined units.");
            for (int cost = 1; cost <= 3; cost++)
            {
                int count = 0;
                foreach (var definition in data.Units) if (definition.Cost == cost) count++;
                Check(count >= 3, "Three units per cost tier.");
            }
            ValidateMasteryAndEconomy(data);
            ValidateShopAndPool(data);
            ValidateMergingAndSales();
            ValidateBenchAndMovement();
            ValidateRunPhases(data);
            ValidateThreeVsThree(board);
            ValidateFiveRounds(board);
            ValidateTimeoutIntegration(board);
            ValidateLayout();
            board.RunController.ResetRun();
            Debug.Log($"RUN_SMOKE_CHECK_PASSED: {assertions} assertions; economy, mastery, odds, pool conservation, merges, bench, 3v3, five rounds, timeout and UI layout.");
        }

        private static void ValidateMasteryAndEconomy(PrototypeGameData data)
        {
            Check(PrototypeEconomy.Income(0, data.Rules) == 5, "Base income.");
            Check(PrototypeEconomy.Income(9, data.Rules) == 5, "Interest boundary below ten.");
            Check(PrototypeEconomy.Income(10, data.Rules) == 6, "Ten-credit interest.");
            Check(PrototypeEconomy.Income(20, data.Rules) == 7, "Twenty-credit interest.");
            Check(PrototypeEconomy.Income(30, data.Rules) == 8 && PrototypeEconomy.Income(100, data.Rules) == 8, "Interest cap.");
            for (int level = 2; level <= 20; level++)
            {
                var mastery = new PrototypeMastery();
                int threshold = PrototypeMastery.CumulativeForLevel(level);
                mastery.AddExp(threshold - 1);
                Check(mastery.Level == level - 1, "Below level-up boundary.");
                mastery.AddExp(1);
                Check(mastery.Level == level && mastery.CurrentExp == 0, "Exact level-up boundary.");
            }
            Check(PrototypeMastery.CumulativeForLevel(7) == 24 &&
                PrototypeMastery.CumulativeForLevel(14) == 59 &&
                PrototypeMastery.CumulativeForLevel(20) == 95, "Mastery tier thresholds.");
            var max = new PrototypeMastery();
            max.AddExp(int.MaxValue);
            Check(max.Level == 20 && max.TotalExp == 95 && max.NextLevelExp == 0, "Mastery cap.");
            int[,] expected = { {1,80,20,0}, {4,70,30,0}, {7,57,38,5}, {10,43,45,12},
                {13,30,47,23}, {16,20,43,37}, {19,14,38,48}, {20,10,30,60} };
            for (int i = 0; i < expected.GetLength(0); i++)
            {
                var odds = data.Odds(expected[i, 0]);
                for (int j = 0; j < 3; j++) Check(odds[j] == expected[i, j + 1], "Configured shop odds.");
            }
            int[,] prices = { {1,2,5}, {2,3,9}, {3,5,14} };
            for (int cost = 1; cost <= 3; cost++)
            for (int stars = 1; stars <= 3; stars++)
                Check(PrototypeEconomy.SalePrice(cost, stars) == prices[cost - 1, stars - 1], "Sale price table.");
            var stats = data.CombatStats(data.Units[0].Id, 2);
            Check(stats.MaxHealth == 180 && stats.AttackPower == 36 && stats.Defense == 5, "Star multipliers only HP and attack.");
        }

        private static void ValidateShopAndPool(PrototypeGameData data)
        {
            var run = new PrototypeRunModel(data, 1729);
            Check(run.Player.Credits == 5 && run.Player.OwnedUnits.Count == 1, "Starting economy and free unit.");
            var starter = run.Player.OwnedUnits[0];
            Check(data.Definition(starter.DefinitionId).Cost == 1 && starter.Stars == 1 &&
                starter.Location == PrototypeUnitLocation.Board && starter.Row == 1 && starter.Column == 1, "Free unit deployed centrally.");
            ConservesPool(run, data);
            int slot = AffordableSlot(run, data);
            string id = run.Shop.Slot(slot);
            int before = run.Pool.Available(id), credits = run.Player.Credits;
            Check(run.Buy(slot), "Buy succeeds.");
            Check(run.Shop.Slot(slot) == null && run.Player.Credits == credits - data.Definition(id).Cost &&
                run.Pool.Available(id) == before && run.Player.AtBench(0) != null, "Reserved copy transfers to ownership.");
            ConservesPool(run, data);
            credits = run.Player.Credits;
            Check(run.Reroll() && run.Player.Credits == credits - 2, "Reroll cost.");
            ConservesPool(run, data);
            var fresh = new PrototypeRunModel(data, 1729);
            Check(fresh.InvestMastery() && fresh.Player.Credits == 3 && fresh.Player.Mastery.TotalExp == 2, "Mastery investment.");
            Check(fresh.InvestMastery() && fresh.Player.Credits == 1 && fresh.Player.Mastery.Level == 2, "Investment level-up.");
            Check(!fresh.InvestMastery() && !fresh.Reroll() && fresh.Player.Credits == 1, "Insufficient credits are atomic.");

            var pool = new PrototypeSharedPool(data);
            var shop = new PrototypeShop(data, pool, new PrototypeRandom(9));
            for (int roll = 0; roll < 20; roll++)
            {
                shop.Roll(20);
                foreach (var definition in data.Units)
                {
                    int reserved = 0;
                    for (int i = 0; i < shop.Count; i++) if (shop.Slot(i) == definition.Id) reserved++;
                    Check(pool.Available(definition.Id) + reserved == pool.Capacity(definition.Id), "Reroll reservations conserved.");
                }
            }
            shop.ReleaseReservations();
            foreach (var definition in data.Units) Check(pool.Available(definition.Id) == pool.Capacity(definition.Id), "Shop release returns all copies.");

            var exhaustedData = Fixture(1, 100);
            exhaustedData.Rules.CopiesByCost[0] = 2;
            var exhausted = new PrototypeRunModel(exhaustedData, 1);
            int stocked = 0;
            for (int i = 0; i < exhausted.Shop.Count; i++) if (exhausted.Shop.Slot(i) != null) stocked++;
            Check(stocked == 1, "Exhausted pool never fabricates stock.");
            Check(exhausted.Buy(0) && exhausted.Reroll(), "Buy last eligible copy then reroll.");
            for (int i = 0; i < exhausted.Shop.Count; i++) Check(exhausted.Shop.Slot(i) == null, "No eligible stock leaves empty shop.");
            ConservesPool(exhausted, exhaustedData);

            var sameA = new PrototypeRunModel(data, 789);
            var sameB = new PrototypeRunModel(data, 789);
            Check(sameA.Player.OwnedUnits[0].DefinitionId == sameB.Player.OwnedUnits[0].DefinitionId, "Seeded starter.");
            for (int i = 0; i < 5; i++) Check(sameA.Shop.Slot(i) == sameB.Shop.Slot(i), "Seeded shop.");
        }

        private static void ValidateMergingAndSales()
        {
            var data = Fixture(1, 1000);
            var run = new PrototypeRunModel(data, 4);
            long originalId = run.Player.OwnedUnits[0].InstanceId;
            BuyCopies(run, 2);
            Check(run.Player.OwnedUnits.Count == 1 && run.Player.Find(originalId).Stars == 2 &&
                run.Player.Find(originalId).Location == PrototypeUnitLocation.Board, "Board + bench 3-copy merge preserves deployed instance.");
            ConservesPool(run, data);
            int available = run.Pool.Available("fixture-00");
            int credits = run.Player.Credits;
            Check(run.Sell(originalId) && run.Pool.Available("fixture-00") == available + 3 &&
                run.Player.Credits == credits + 2, "Two-star sale returns three originals.");

            run = new PrototypeRunModel(data, 4);
            BuyCopies(run, 8);
            Check(run.Player.OwnedUnits.Count == 1 && run.Player.Find(originalId).Stars == 3, "Nine copies cascade to three-star.");
            ConservesPool(run, data);
            available = run.Pool.Available("fixture-00");
            credits = run.Player.Credits;
            Check(run.Sell(originalId) && run.Pool.Available("fixture-00") == available + 9 &&
                run.Player.Credits == credits + 5, "Three-star sale returns nine originals.");
            ConservesPool(run, data);
            var maximum = new PrototypeRunModel(data, 3);
            for (int i = 0; i < 48; i++) maximum.InvestMastery();
            credits = maximum.Player.Credits;
            Check(maximum.Player.Mastery.Level == 20 && !maximum.InvestMastery() &&
                maximum.Player.Credits == credits, "Max mastery investment rejected without spending.");
        }

        private static void ValidateBenchAndMovement()
        {
            var data = Fixture(12, 1000);
            var run = new PrototypeRunModel(data, 98);
            var unique = new HashSet<string> { run.Player.OwnedUnits[0].DefinitionId };
            for (int tries = 0; tries < 100 && run.Player.EmptyBench() >= 0; tries++)
            {
                for (int slot = 0; slot < 5 && run.Player.EmptyBench() >= 0; slot++)
                {
                    string id = run.Shop.Slot(slot);
                    if (id == null || unique.Contains(id)) continue;
                    Check(run.Buy(slot), "Fill bench with distinct units.");
                    unique.Add(id);
                }
                if (run.Player.EmptyBench() >= 0) Check(run.Reroll(), "Find distinct bench stock.");
            }
            Check(run.Player.EmptyBench() == -1, "Eight-slot bench full.");
            run.Reroll();
            string reserved = run.Shop.Slot(0);
            int credits = run.Player.Credits, available = run.Pool.Available(reserved);
            Check(!run.Buy(0) && run.Player.Credits == credits && run.Shop.Slot(0) == reserved &&
                run.Pool.Available(reserved) == available, "Full-bench purchase fails atomically before merging.");
            var mover = run.Player.AtBench(0);
            Check(run.Move(mover.InstanceId, PrototypeUnitLocation.Board, 0, 0), "Bench to board.");
            Check(run.Move(mover.InstanceId, PrototypeUnitLocation.Board, 0, 1), "Board to board.");
            Check(!run.Move(mover.InstanceId, PrototypeUnitLocation.Board, 0, 3), "Cannot deploy on enemy side.");
            Check(!run.Move(mover.InstanceId, PrototypeUnitLocation.Board, 1, 1), "Cannot deploy on occupied cell.");
            Check(run.Move(mover.InstanceId, PrototypeUnitLocation.Bench, 0), "Board to bench.");
            var second = run.Player.AtBench(1);
            Check(run.Move(second.InstanceId, PrototypeUnitLocation.Board, 2, 0), "Free bench destination.");
            Check(run.Move(mover.InstanceId, PrototypeUnitLocation.Bench, 1), "Bench to bench.");
            ConservesPool(run, data);
        }

        private static void ValidateRunPhases(PrototypeGameData data)
        {
            var run = new PrototypeRunModel(data, 100);
            int credits = run.Player.Credits;
            run.Tick(29);
            Check(run.Phase == PrototypeRunPhase.Prep, "Prep timer not yet expired.");
            run.Tick(1);
            Check(run.Phase == PrototypeRunPhase.Combat, "Prep timeout starts combat.");
            Check(!run.Buy(0) && !run.Reroll() && !run.InvestMastery() &&
                !run.Sell(run.Player.OwnedUnits[0].InstanceId) &&
                !run.Move(run.Player.OwnedUnits[0].InstanceId, PrototypeUnitLocation.Bench, 0), "Combat rejects all prep commands.");
            Check(run.Player.Credits == credits, "Rejected commands preserve credits.");
            run.Tick(60);
            Check(run.Phase == PrototypeRunPhase.Result && run.History[0].Outcome == PrototypeRoundOutcome.Draw, "Combat timeout draw.");
            Check(run.Player.Credits == credits + 5 && run.Player.Mastery.TotalExp == 4, "Round income and natural EXP.");
            Check(!run.CompleteCombat(PrototypeRoundOutcome.Win), "Result rewards cannot be duplicated.");
            run.Tick(2);
            Check(run.Phase == PrototypeRunPhase.Transition, "Result to transition.");
            run.Tick(1);
            Check(run.Phase == PrototypeRunPhase.Prep && run.Round == 2, "Next round prep.");
            ConservesPool(run, data);
            run.Reset(100);
            Check(run.Round == 1 && run.Player.Credits == 5 && run.History.Count == 0 && run.Player.Mastery.Level == 1, "Reset run is complete.");
            for (int round = 1; round <= 5; round++)
            {
                var team = data.EnemyTeam(round);
                Check(team.Units.Length >= 1 && team.Units.Length <= 9, "Valid enemy count.");
                var cells = new HashSet<int>();
                foreach (var enemy in team.Units)
                    Check(enemy.Column >= 3 && enemy.Column < 6 && cells.Add(enemy.Row * 6 + enemy.Column), "Enemy side and unique cells.");
            }
            Check(data.EnemyTeam(5).Units.Length > data.EnemyTeam(1).Units.Length, "Enemy progression.");
        }

        private static void ValidateThreeVsThree(BattlefieldPrototype board)
        {
            board.ClearUnits();
            var units = new List<PrototypeUnit>();
            for (int row = 0; row < 3; row++)
                units.Add(board.CreateUnit("A", row, 1, new PrototypeCombatStats(), "3v3 A"));
            for (int row = 0; row < 3; row++)
                units.Add(board.CreateUnit("B", row, 4, new PrototypeCombatStats(), "3v3 B"));
            board.ConfigureCombat(units);
            board.Combat.StartBattle();
            bool died = false;
            for (int tick = 0; tick < 3000 && board.Combat.State == PrototypeCombatState.Fighting; tick++)
            {
                board.Combat.Step(0.02f);
                CheckOccupancy(board.Combat);
                foreach (var unit in units)
                {
                    died |= !unit.IsAlive;
                    Check(unit.Target == null || unit.Target.IsAlive, "Never retain a dead target.");
                }
            }
            Check(died && board.Combat.State == PrototypeCombatState.Finished, "3v3 combat completes.");
            var positions = units.ConvertAll(unit => unit.transform.position);
            var hp = units.ConvertAll(unit => unit.CurrentHealth);
            for (int tick = 0; tick < 100; tick++) board.Combat.Step(0.02f);
            for (int i = 0; i < units.Count; i++)
                Check(units[i].transform.position == positions[i] && units[i].CurrentHealth == hp[i], "No attacks after finish.");
        }

        private static void ValidateFiveRounds(BattlefieldPrototype board)
        {
            var controller = board.RunController;
            controller.ResetRun();
            var run = controller.Run;
            int slot = AffordableSlot(run, controller.Data);
            Check(controller.Buy(slot), "Integrated shop purchase.");
            var purchase = run.Player.AtBench(0);
            Check(controller.Move(purchase.InstanceId, PrototypeUnitLocation.Board, 0, 0), "Integrated bench deployment.");
            for (int round = 1; round <= 5; round++)
            {
                Check(run.Round == round && run.Phase == PrototypeRunPhase.Prep, "Expected sequential prep.");
                int ownedCount = run.Player.OwnedUnits.Count;
                Check(controller.Ready(), "Ready starts combat.");
                for (int tick = 0; tick < 3100 && run.Phase == PrototypeRunPhase.Combat; tick++)
                {
                    controller.Step(0.02f);
                    CheckOccupancy(board.Combat);
                }
                Check(run.Phase == PrototypeRunPhase.Result && run.History.Count == round, "Round resolves once.");
                Check(run.Player.OwnedUnits.Count == ownedCount, "Combat death does not delete ownership.");
                ConservesPool(run, controller.Data);
                controller.Step(2.1f);
                Check(run.Phase == PrototypeRunPhase.Transition, "Visible transition.");
                controller.Step(1.1f);
                Check(run.Phase == PrototypeRunPhase.Prep && run.Round == round + 1, "Five-round loop without editor restart.");
                foreach (var unit in board.Units) Check(unit.CurrentHealth == unit.MaxHealth, "Next prep resets combat damage.");
            }
            Check(run.Player.Mastery.TotalExp == 20, "Five natural mastery rewards.");
        }

        private static void ValidateTimeoutIntegration(BattlefieldPrototype board)
        {
            var controller = board.RunController;
            controller.ResetRun();
            Check(controller.Ready(), "Timeout fixture begins.");
            foreach (var unit in board.Units) { unit.Stats.MaxHealth = 100000; unit.SetHealth(100000); unit.Stats.AttackPower = 0; }
            for (int tick = 0; tick < 3100 && controller.Run.Phase == PrototypeRunPhase.Combat; tick++) controller.Step(0.02f);
            Check(controller.Run.Phase == PrototypeRunPhase.Result &&
                controller.Run.History[0].Outcome == PrototypeRoundOutcome.Draw &&
                board.Combat.State == PrototypeCombatState.Finished, "60-second draw stops combat core.");
            foreach (var unit in board.Units) Check(!unit.IsMoving, "Timeout cancels motion.");
            controller.ResetRun();
            Check(controller.Run.Player.Credits == 5 && controller.Run.Round == 1, "Reset after timeout.");
        }

        private static void ValidateLayout()
        {
            foreach (var size in new[] { new Vector2Int(1920,1080), new Vector2Int(1280,720), new Vector2Int(800,600), new Vector2Int(720,1280) })
            {
                var rect = PrototypeRunHud.CameraViewport(size.x, size.y);
                Check(rect.xMin >= 0 && rect.yMin >= 0 && rect.xMax <= 1 && rect.yMax <= 1, "Camera viewport inside screen.");
                float scale = PrototypeRunHud.Scale(size.x, size.y);
                Check(PrototypeRunHud.DesignWidth * scale <= size.x + 1 && PrototypeRunHud.DesignHeight * scale <= size.y + 1, "All HUD regions fit screen.");
            }
        }

        private static PrototypeGameData Fixture(int unitCount, int credits)
        {
            var data = PrototypeGameData.Load();
            data.Rules.StartingCredits = credits;
            data.Units = new PrototypeUnitDefinition[unitCount];
            for (int i = 0; i < unitCount; i++)
                data.Units[i] = new PrototypeUnitDefinition { Id = $"fixture-{i:00}", DisplayName = $"Fixture {i}", Cost = 1, Stats = new PrototypeCombatStats() };
            data.EnemyRounds = new[] { new PrototypeEnemyRound { Units = new[] { new PrototypeEnemyEntry { UnitId = "fixture-00", Stars = 1, Row = 1, Column = 4 } } } };
            data.Validate();
            return data;
        }

        private static void BuyCopies(PrototypeRunModel run, int count)
        {
            for (int copy = 0; copy < count; copy++)
            {
                int slot = -1;
                for (int i = 0; i < run.Shop.Count; i++) if (run.Shop.Slot(i) != null) { slot = i; break; }
                if (slot < 0) { Check(run.Reroll(), "Merge fixture reroll."); slot = 0; }
                Check(run.Buy(slot), "Merge fixture purchase.");
            }
        }

        private static int AffordableSlot(PrototypeRunModel run, PrototypeGameData data)
        {
            for (int i = 0; i < run.Shop.Count; i++)
                if (run.Shop.Slot(i) != null && data.Definition(run.Shop.Slot(i)).Cost <= run.Player.Credits) return i;
            throw new Exception("No affordable test shop slot.");
        }

        private static void ConservesPool(PrototypeRunModel run, PrototypeGameData data)
        {
            foreach (var definition in data.Units)
            {
                int total = run.Pool.Available(definition.Id);
                foreach (var owned in run.Player.OwnedUnits)
                    if (owned.DefinitionId == definition.Id) total += PrototypeEconomy.OriginalCopies(owned.Stars);
                for (int slot = 0; slot < run.Shop.Count; slot++) if (run.Shop.Slot(slot) == definition.Id) total++;
                Check(total == run.Pool.Capacity(definition.Id), "Pool = available + owned originals + reservations.");
            }
        }

        private static void CheckOccupancy(PrototypeCombatController combat)
        {
            var occupied = new HashSet<Vector2Int>();
            foreach (var unit in combat.Units)
                if (unit.IsAlive)
                    Check(combat.Grid.Contains(unit.Cell) && occupied.Add(unit.Cell) && combat.Grid.Occupant(unit.Cell) == unit, "Multi-unit valid unique occupancy.");
        }

        private static void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new Exception("Run smoke: " + message);
        }
    }
}
