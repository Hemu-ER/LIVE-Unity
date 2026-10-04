using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LIVE.Prototype.Editor
{
    public static class PrototypeAiSmokeCheck
    {
        private static int assertions;
        public static void Validate()
        {
            assertions = 0;
            TargetLock();
            RangeAndCadence();
            CollisionAndRouting();
            DashAndRangeChanges();
            StallDiagnostics();
            var data = PrototypeGameData.Load();
            int draws = 0;
            for (int seed = 1; seed <= 32; seed++)
            {
                string expected = null;
                for (int reversed = 0; reversed < 2; reversed++)
                using (var f = new Fixture())
                {
                    var random = new PrototypeRandom(seed);
                    for (int side = 0; side < 2; side++)
                    {
                        var cells = Enumerable.Range(0, 9).ToList();
                        for (int i = 0; i < 3; i++)
                        {
                            int choice = random.Next(cells.Count), cell = cells[choice]; cells.RemoveAt(choice);
                            var definition = data.Units[(seed + side * 3 + i) % 9];
                            var unit = f.Add(side == 0 ? "A" : "B", cell / 3, cell % 3 + side * 3,
                                data.CombatStats(definition.Id, 1));
                            unit.ConfigureAbilities(definition.Abilities);
                        }
                    }
                    f.Start(seed, reversed != 0);
                    int ticks = 0;
                    while (f.Combat.State == PrototypeCombatState.Fighting && ticks < 3000)
                    { f.Step(); ticks++; f.CheckGrid(); }
                    if (f.Combat.State == PrototypeCombatState.Fighting) { draws++; f.Combat.FinishAsDraw(); }
                    Check(f.Combat.State == PrototypeCombatState.Finished, "Simulation terminates by 60-second draw deadline");
                    var snapshot = ticks + ":" + f.Combat.Winner + ":" + string.Join("|", f.Units.OrderBy(u => u.CombatId)
                        .Select(u => u.CombatId + ":" + u.Cell + ":" + u.CurrentHealth + ":" + u.Statistics));
                    if (reversed == 0) expected = snapshot;
                    else Check(snapshot == expected, "Seed/state reproduce with reversed registration order");
                }
            }
            Debug.Log($"AI_SMOKE_CHECK_PASSED: {assertions} assertions, 64 seeded 3v3 simulations ({draws} timeout draws), target lock/HP/ID ties, range, cadence, reservation collisions, routing, finite positions and stall diagnostics.");
        }

        private static void TargetLock()
        {
            using (var f = new Fixture())
            {
                var a = f.Add("A", 1, 1);
                var highId = f.Add("B", 2, 3);
                var lowId = f.Add("B", 0, 3);
                var far = f.Add("B", 2, 5);
                f.Start();
                Check(f.Combat.FindNearestEnemy(a) == lowId, "Equal distance/HP resolves by immutable combat ID");
                highId.SetHealth(500); far.SetHealth(1);
                Check(f.Combat.FindNearestEnemy(a) == highId, "Distance before current HP; HP before ID");
                f.Step();
                Check(a.Target == highId, "Explicit current target selected");
                lowId.SetHealth(100);
                for (int i = 0; i < 4; i++) f.Step();
                Check(a.Target == highId, "Valid target retained even if another has less HP");
                highId.TakeDamage(10000);
                Check(a.Target == lowId, "Death immediately retargets, including during movement");
                Check(f.Combat.Grid.IsFree(highId.Cell), "Death frees origin and reservations");
            }
        }

        private static void RangeAndCadence()
        {
            int slow = 0;
            for (int speed = 1; speed <= 2; speed++)
            using (var f = new Fixture())
            {
                var a = f.Add("A", 1, 1, new PrototypeCombatStats { MaxHealth = 1000, AttackRange = 3, AttackSpeed = speed });
                f.Add("B", 1, 4, new PrototypeCombatStats { MaxHealth = 1000, AttackRange = 3 });
                f.Start(); var origin = a.Cell;
                for (int i = 0; i < 100; i++) { f.Step(); Check(a.Cell == origin && !a.IsMoving, "Ranged holds position in range"); }
                if (speed == 1) slow = a.Statistics.BasicAttackCount;
                else Check(a.Statistics.BasicAttackCount == slow * 2, "Double attack speed doubles attacks over two seconds");
            }
            using (var f = new Fixture())
            {
                var a = f.Add("A", 1, 0, new PrototypeCombatStats { MaxHealth = 1000, AttackRange = 3 });
                f.Add("B", 1, 5, new PrototypeCombatStats { MaxHealth = 1000, AttackRange = 3 });
                f.Start(); f.Step(); Check(a.IsMoving && a.Statistics.BasicAttackCount == 0, "Ranged approaches outside range");
                for (int i = 0; i < 120; i++) { f.Step(); f.CheckGrid(); }
                Check(a.Statistics.BasicAttackCount > 0, "Ranged attacks after approach");
            }
            using (var f = new Fixture())
            {
                var a = f.Add("A", 1, 1); var b = f.Add("B", 1, 4);
                f.Start(); f.Step(); Check(a.IsMoving && b.IsMoving, "Opposing melee approach");
                for (int i = 0; i < 120; i++) { f.Step(); f.CheckGrid(); }
                Check(a.Statistics.BasicAttackCount > 0 && PrototypeCombatGrid.Distance(a.Cell, b.Cell) == 1, "Melee only attacks adjacent");
            }
        }

        private static void CollisionAndRouting()
        {
            string expected = null;
            for (int reverse = 0; reverse < 2; reverse++)
            using (var f = new Fixture())
            {
                var first = f.Add("A", 0, 0);
                var second = f.Add("A", 2, 0);
                f.Add("A", 0, 1, new PrototypeCombatStats { MaxHealth = 1000, AttackRange = 6 });
                f.Add("A", 2, 1, new PrototypeCombatStats { MaxHealth = 1000, AttackRange = 6 });
                var enemy = f.Add("B", 1, 3);
                f.Start(reverse: reverse != 0);
                Check(f.Combat.Grid.TryFindStep(first, enemy, out var p) && p == new Vector2Int(1, 1), "First candidate routes around allied ranged blocker");
                Check(f.Combat.Grid.TryFindStep(second, enemy, out var q) && q == p, "Both initially compete for same cell");
                f.Step(); Check(first.IsMoving && !f.Combat.Grid.IsFree(p), "Lower ID reserves contested cell");
                for (int i = 0; i < 28; i++) { f.Step(); f.CheckGrid(); }
                Check(first.Cell == p && second.Cell != p, "Reservation winner completes without duplicate occupancy");
                string snapshot = first.Cell + ":" + second.Cell + ":" + first.Statistics + ":" + second.Statistics;
                if (reverse == 0) expected = snapshot; else Check(snapshot == expected, "Collision outcome independent of registration order");
            }
        }

        private static void DashAndRangeChanges()
        {
            using (var f = new Fixture())
            {
                var ranged = f.Add("A", 1, 0, new PrototypeCombatStats { MaxHealth = 1000, AttackRange = 3 });
                f.Add("A", 2, 5);
                var dasher = f.Add("B", 1, 3);
                dasher.ConfigureAbilities(DashAbility());
                f.Start(); f.Step();
                Check(!ranged.IsMoving && ranged.Statistics.BasicAttackCount == 1, "Ranged initially holds firing position");
                for (int i = 0; i < 11; i++) f.Step();
                Check(ranged.Target == dasher && PrototypeCombatGrid.Distance(ranged.Cell, dasher.Cell) > 3 && ranged.IsMoving,
                    "Ranged retains target and resumes approach after target dashes outside range");
                f.CheckGrid();
            }
            for (int blocked = 0; blocked < 2; blocked++)
            using (var f = new Fixture())
            {
                var dasher = f.Add("A", 1, 1);
                dasher.ConfigureAbilities(DashAbility());
                f.Add("A", 1, 2, new PrototypeCombatStats { MaxHealth = 1000, AttackRange = 6 });
                if (blocked == 1)
                    foreach (var cell in new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2),
                        new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(2, 1), new Vector2Int(2, 2) })
                        f.Add("A", cell.x, cell.y, new PrototypeCombatStats { MaxHealth = 1000, AttackRange = 6 });
                f.Add("B", 1, 4, new PrototypeCombatStats { MaxHealth = 1000, AttackRange = 6 });
                f.Start(); var origin = dasher.Cell;
                for (int i = 0; i < 10; i++) { f.Step(); f.CheckGrid(); }
                Check(blocked == 1 ? dasher.Cell == origin : dasher.Cell != origin, "Dash routes around occupied direct cell or stays when completely blocked");
                Check(dasher.Cell != new Vector2Int(1, 2), "Dash never enters occupied destination");
            }
        }

        private static PrototypeAbilitySettings DashAbility() => new PrototypeAbilitySettings
        {
            Skills = new[] { new PrototypeSkillDefinition { Id = "ai-dash", Trigger = PrototypeSkillTrigger.OnCombatStart,
                Range = 8, Effects = new[] { new PrototypeSkillEffect { Type = PrototypeSkillEffectType.Dash, DashCells = 2 } } } }
        };

        private static void StallDiagnostics()
        {
            using (var f = new Fixture())
            {
                var a = f.Add("A", 1, 1, new PrototypeCombatStats { MaxHealth = 1000, AttackRange = 3, AttackSpeed = 0.01f });
                f.Add("B", 1, 4, new PrototypeCombatStats { MaxHealth = 1000, AttackRange = 3, AttackSpeed = 0.01f });
                f.Start(); f.Step();
                f.Combat.StallThresholdSeconds = 0.5f;
                for (int i = 0; i < 26; i++) f.Step();
                Check(f.Combat.StallCount == 1 && f.Combat.LastStallDiagnostic.Contains(a.CombatId) &&
                    f.Combat.State == PrototypeCombatState.Fighting, "Stall emits useful diagnostic without forcing outcome");
                a.ApplyShield(1); for (int i = 0; i < 20; i++) f.Step();
                Check(f.Combat.StallCount == 1, "Shield change resets quiet interval");
                f.Combat.ResetBattle(); Check(f.Combat.StallCount == 0, "Reset clears diagnostic history");
            }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("AI smoke fixture");
            public readonly List<PrototypeUnit> Units = new List<PrototypeUnit>();
            public readonly PrototypeCombatController Combat;
            public Fixture() { Combat = root.AddComponent<PrototypeCombatController>(); Combat.enabled = false; }
            public PrototypeUnit Add(string faction, int row, int column, PrototypeCombatStats stats = null)
            {
                var obj = new GameObject(faction); obj.transform.SetParent(root.transform);
                var unit = obj.AddComponent<PrototypeUnit>();
                unit.Initialize(faction, row, column, stats ?? new PrototypeCombatStats { MaxHealth = 1000 }, null, null);
                Units.Add(unit); return unit;
            }
            public void Start(int seed = 1729, bool reverse = false)
            {
                Combat.Initialize(3, 6, reverse ? Units.AsEnumerable().Reverse() : Units, BattlefieldPrototype.Position, seed);
                Combat.StartBattle();
            }
            public void Step() => Combat.Step(0.02f);
            public void CheckGrid()
            {
                var cells = new HashSet<Vector2Int>();
                foreach (var unit in Units)
                {
                    var p = unit.transform.position;
                    Check(!float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsInfinity(p.x) && !float.IsInfinity(p.y), "Finite interpolated position");
                    if (unit.IsAlive) Check(Combat.Grid.Contains(unit.Cell) && cells.Add(unit.Cell) && Combat.Grid.Occupant(unit.Cell) == unit, "Unique in-bounds logical occupancy");
                }
            }
            public void Dispose() => UnityEngine.Object.DestroyImmediate(root);
        }
        private static void Check(bool value, string message)
        { assertions++; if (!value) throw new Exception("AI smoke: " + message); }
    }
}
