using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LIVE.Prototype.Editor
{
    public static class PrototypeDistanceSmokeCheck
    {
        private static int assertions;
        public static void Validate()
        {
            assertions = 0;
            Check(PrototypeCombatGrid.Distance(new Vector2Int(1, 1), new Vector2Int(2, 2)) == 1, "Diagonal distance one");
            Check(PrototypeCombatGrid.Distance(new Vector2Int(1, 1), new Vector2Int(1, 2)) == 1, "Orthogonal distance one");
            Check(PrototypeCombatGrid.Distance(new Vector2Int(1, 1), new Vector2Int(2, 3)) == 2, "Mixed distance two");
            for (int r = 0; r < 3; r++) for (int c = 0; c < 6; c++)
            for (int s = 0; s < 3; s++) for (int d = 0; d < 6; d++)
                Check(PrototypeCombatGrid.Distance(new Vector2Int(r, c), new Vector2Int(s, d)) == Math.Max(Math.Abs(r - s), Math.Abs(c - d)), "Every board pair uses Chebyshev");
            using (var f = new Fixture())
            {
                var a = f.Add("A", 1, 1); var b = f.Add("B", 2, 2); f.Start(); f.Combat.Step(.02f);
                Check(a.Statistics.BasicAttackCount == 1 && b.CurrentHealth < b.MaxHealth, "Diagonal melee attacks on first tick");
                Check(!a.IsMoving && a.Cell == new Vector2Int(1, 1), "Diagonal melee does not move");
            }
            for (int reverse = 0; reverse < 2; reverse++)
            using (var f = new Fixture())
            {
                var a = f.Add("A", 1, 1);
                var diagonal = f.Add("B", 0, 2); var orthogonal = f.Add("B", 1, 2);
                var far = f.Add("B", 1, 3); f.Start(reverse != 0);
                Check(f.Combat.FindNearestEnemy(a) == diagonal, "Diagonal ties orthogonal: stable ID wins regardless of registration");
                orthogonal.SetHealth(900); far.SetHealth(1);
                Check(f.Combat.FindNearestEnemy(a) == orthogonal, "Distance then current HP then stable ID preserved");
            }
            // Both orthogonal side cells are occupied; the diagonal destination remains legal.
            using (var f = new Fixture())
            {
                var a = f.Add("A", 0, 0); a.Stats.MoveSpeed = 2;
                var side1 = f.Add("A", 0, 1, 6); var side2 = f.Add("A", 1, 0, 6);
                var b = f.Add("B", 2, 2, 6); f.Start();
                var destination = new Vector2Int(1, 1);
                Check(f.Combat.Grid.TryFindStep(a, b, out var next) && next == destination, "BFS takes one diagonal step through occupied corner sides");
                Check(!f.Combat.Grid.TryReserveStep(a, side1.Cell), "Cannot reserve occupied cell");
                Check(!f.Combat.Grid.TryReserveStep(a, new Vector2Int(2, 1)), "Cannot skip a cell");
                f.Combat.Step(.02f);
                Check(a.IsMoving && a.Cell == Vector2Int.zero && !f.Combat.Grid.IsFree(destination), "Origin occupied and diagonal destination reserved during travel");
                Check(!f.Combat.Grid.TryReserveStep(side1, destination), "Second mover cannot enter reserved destination");
                f.Combat.Step(.25f);
                Check(a.IsMoving && a.transform.position != f.Combat.WorldPosition(a.Cell), "Diagonal interpolation visible");
                f.Combat.Step(.25f);
                Check(!a.IsMoving && a.Cell == destination && f.Combat.Grid.Occupant(destination) == a, "Diagonal completes in one MoveSpeed step at exact destination");
                Check(f.Combat.Grid.IsFree(Vector2Int.zero), "Completed step releases origin");
                Check(f.Combat.Grid.Occupant(side1.Cell) == side1 && f.Combat.Grid.Occupant(side2.Cell) == side2, "Corner occupants unchanged");
                Check(a.Statistics.BasicAttackCount == 0, "No attack during movement completion");
                f.Combat.Step(.02f); Check(a.Statistics.BasicAttackCount == 1, "Attack from diagonal destination");
                f.Combat.ResetBattle(); Check(a.Cell == Vector2Int.zero && f.Combat.Grid.IsFree(destination), "Reset clears diagonal occupancy/reservation");
            }
            using (var f = new Fixture())
            {
                var a = f.Add("A", 0, 0); var b = f.Add("B", 2, 3, 6);
                f.Add("A", 0, 1, 6); f.Add("A", 1, 0, 6); f.Add("A", 1, 1, 6); f.Start();
                Check(!f.Combat.Grid.TryFindStep(a, b, out _), "BFS cannot pass through a completely occupied eight-neighbour boundary");
            }
            // Unflagged generic radius and explicit skill range now share the same geometry.
            using (var f = new Fixture())
            {
                var a = f.Add("A", 0, 0); var b = f.Add("B", 1, 1, 6);
                var diagonal = f.Add("B", 2, 2, 6); var far = f.Add("B", 2, 3, 6);
                a.ConfigureAbilities(new PrototypeAbilitySettings { Skills = new[] {
                    new PrototypeSkillDefinition { Id = "distance-radius", Trigger = PrototypeSkillTrigger.OnCombatStart,
                        Range = 1, Effects = new[] { new PrototypeSkillEffect { Type = PrototypeSkillEffectType.Damage,
                            Area = PrototypeEffectArea.AdjacentEnemies, Radius = 1, BaseDamage = 50 } } }
                } });
                f.Start(); for (int i = 0; i < 20; i++) f.Combat.Step(.02f);
                Check(diagonal.CurrentHealth < diagonal.MaxHealth, "Skill range one reaches diagonal anchor and radius includes diagonal recipient");
                Check(far.CurrentHealth == far.MaxHealth, "Radius excludes distance two recipient");
            }
            Debug.Log($"DISTANCE_SMOKE_CHECK_PASSED: {assertions} assertions; all board pairs, diagonal melee, targeting/ID ties, eight-way BFS, corner cutting, reservations, interpolation, occupancy, reset and skill geometry. Real Charlotte radius is additionally covered by WEB_BATCH_SMOKE_CHECK at all three stars.");
        }
        private static void Check(bool value, string message)
        { assertions++; if (!value) throw new InvalidOperationException("Distance: " + message); }
        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("Distance smoke fixture");
            private readonly List<PrototypeUnit> units = new List<PrototypeUnit>();
            public readonly PrototypeCombatController Combat;
            public Fixture() { Combat = root.AddComponent<PrototypeCombatController>(); Combat.enabled = false; }
            public PrototypeUnit Add(string faction, int row, int column, int range = 1)
            {
                var obj = new GameObject(faction); obj.transform.SetParent(root.transform);
                var unit = obj.AddComponent<PrototypeUnit>();
                unit.Initialize(faction, row, column, new PrototypeCombatStats { MaxHealth = 1000, AttackRange = range }, null, null);
                units.Add(unit); return unit;
            }
            public void Start(bool reverse = false)
            { Combat.Initialize(3, 6, reverse ? units.AsEnumerable().Reverse() : units, BattlefieldPrototype.Position, 1729); Combat.StartBattle(); }
            public void Dispose() => UnityEngine.Object.DestroyImmediate(root);
        }
    }
}
