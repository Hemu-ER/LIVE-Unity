using System;
using System.Collections.Generic;
using UnityEngine;

namespace LIVE.Prototype.Editor
{
    // Exercises the same runtime as Play, without a View or timing-dependent coroutines.
    public static class PrototypeAbilitySmokeCheck
    {
        private static int assertions;
        public static void Validate()
        {
            assertions = 0;
            var data = PrototypeGameData.Load();
            Check(data.Units.Length == 9, "Nine migrated definitions");
            foreach (var definition in data.Units)
            {
                var one = data.CombatStats(definition.Id, 1);
                var two = data.CombatStats(definition.Id, 2);
                var three = data.CombatStats(definition.Id, 3);
                Check(one.MaxHealth > 0 && one.AttackPower > 0 && one.AttackSpeed > 0 && one.MoveSpeed > 0 &&
                    one.AttackRange >= 1 && one.Defense >= 0 && one.SkillAmplification >= 0 &&
                    one.CriticalChance >= 0 && one.CriticalChance <= 1 && one.DefensePenetration >= 0, "All stats load");
                Check(two.MaxHealth == Mathf.RoundToInt(one.MaxHealth * 1.8f) &&
                    two.AttackPower == Mathf.RoundToInt(one.AttackPower * 1.8f) &&
                    three.MaxHealth == Mathf.RoundToInt(one.MaxHealth * 3.2f) &&
                    three.AttackPower == Mathf.RoundToInt(one.AttackPower * 3.2f), "Star growth");
                Check(one.SkillAmplification == two.SkillAmplification && one.AttackSpeed == three.AttackSpeed &&
                    one.CriticalChance == three.CriticalChance && one.Defense == two.Defense, "Stars only scale HP/AP");
                Check(definition.Abilities.Skills.Length > 0, "Each character has a skill");
            }
            FormulaAndTiming();
            GaugeAndCasting();
            TriggersAndEffects(data);
            DeterministicTeams(data);
            Debug.Log($"ABILITY_SMOKE_CHECK_PASSED: {assertions} assertions; stats, stars, formulas, cadence, range, gauge, five triggers, five effects, cast interruption, events, statistics, seeded teams/reset.");
        }

        private static void FormulaAndTiming()
        {
            Check(PrototypeDamageCalculator.Mitigate(20, 100, 0) == 10, "Defense mitigation");
            Check(PrototypeDamageCalculator.Mitigate(20, 100, 50) == 13, "Flat penetration");
            Check(PrototypeDamageCalculator.Mitigate(20, 100, 200) == 20, "Defense floors at zero");
            Check(PrototypeDamageCalculator.Mitigate(20, 5, 0, true) == 38, "Critical before integer rounding");
            Check(PrototypeDamageCalculator.Mitigate(0, 100, 0) == 1, "Minimum damage");
            var effect = new PrototypeSkillEffect { BaseDamage = 10, AttackRatio = 0.5f, SkillRatio = 2 };
            Check(PrototypeDamageCalculator.Coefficients(effect, new PrototypeCombatStats { AttackPower = 20, SkillAmplification = 30 }) == 80, "Skill AP/amp coefficients");
            effect.AttackRatio = effect.SkillRatio = 0;
            Check(PrototypeDamageCalculator.Coefficients(effect, new PrototypeCombatStats()) == 10, "Flat-only skill");
            for (int crit = 0; crit <= 1; crit++)
            using (var f = new Fixture())
            {
                f.A.Stats.CriticalChance = crit;
                f.A.Stats.AttackSpeed = 2;
                f.Start(); f.Step();
                Check(f.B.CurrentHealth == 1000 - (crit == 1 ? 40 : 20), "0/100 percent critical in actual attack");
                Check(f.Events.Exists(e => e.Type == PrototypeCombatEventType.CriticalHit) == (crit == 1), "Critical event");
                Check(f.A.Statistics.BasicAttackCount == 1 && !f.A.IsMoving, "Range 3 attacks from distance");
                for (int i = 0; i < 20; i++) f.Step();
                Check(f.A.Statistics.BasicAttackCount == 1, "No attack before half-second cooldown");
                for (int i = 0; i < 6; i++) f.Step();
                Check(f.A.Statistics.BasicAttackCount == 2, "Two attacks/second cadence");
            }
        }

        private static void GaugeAndCasting()
        {
            using (var f = new Fixture())
            {
                var skill = Skill(PrototypeSkillTrigger.GaugeFull, PrototypeSkillTarget.CurrentTarget,
                    new PrototypeSkillEffect { Type = PrototypeSkillEffectType.Damage, BaseDamage = 10, AttackRatio = 0.5f, SkillRatio = 2 });
                f.A.Stats.SkillAmplification = 30;
                f.A.ConfigureAbilities(new PrototypeAbilitySettings { MaxSkillGauge = 80, Skills = new[] { skill } });
                f.Start(); f.Step();
                Check(f.A.SkillGauge == 30 && f.B.SkillGauge == 30, "Attack +20 and received hit +10");
                f.A.GainGauge(1000);
                Check(f.A.SkillGauge == 80, "Custom gauge cap");
                int attacks = f.A.Statistics.BasicAttackCount, health = f.B.CurrentHealth;
                var cell = f.A.Cell;
                for (int i = 0; i < 7 && f.A.ActionState != PrototypeActionState.Casting; i++) f.Step();
                Check(f.A.ActionState == PrototypeActionState.Casting && f.A.SkillGauge == 0, "Gauge cast priority and consume");
                for (int i = 0; i < 4; i++) f.Step();
                Check(f.A.Cell == cell && !f.A.IsMoving && f.A.Statistics.BasicAttackCount == attacks && f.B.CurrentHealth == health, "Casting blocks movement/basic and waits for release");
                for (int i = 0; i < 5; i++) f.Step();
                Check(f.B.CurrentHealth == health - 80 && f.A.Statistics.SkillCastCount == 1, "Actual skill formula and count");
                // Definition mutation cannot leak into the copied runtime.
                skill.Effects[0].BaseDamage = 900;
                Check(f.A.Skills[0].Definition.Effects[0].BaseDamage == 10, "Definition/runtime isolation");
                f.A.GainGauge(80); f.Step();
                Check(f.A.ActionState == PrototypeActionState.Casting, "Second cast begins");
                health = f.B.CurrentHealth;
                f.A.TakeDamage(10000); f.A.GainGauge(100);
                for (int i = 0; i < 20; i++) f.Step();
                Check(f.A.ActionState == PrototypeActionState.Dead && f.A.SkillGauge == 0 && f.B.CurrentHealth == health, "Death interrupts cast and blocks gauge/damage");
                Check(f.Combat.Grid.IsFree(cell) && f.Events[f.Events.Count - 1].Type == PrototypeCombatEventType.CombatFinished, "Death frees cell before final event");
                f.Combat.ResetBattle();
                Check(f.A.Statistics.SkillCastCount == 0 && f.A.Shield == 0 && f.A.SkillGauge == 0 && f.A.IsAlive, "Reset clears runtime only");
            }
            using (var f = new Fixture())
            {
                var retargetSkill = Skill(PrototypeSkillTrigger.OnCombatStart,
                    PrototypeSkillTarget.CurrentTarget, new PrototypeSkillEffect { Type = PrototypeSkillEffectType.Damage, BaseDamage = 40 });
                retargetSkill.Range = 4;
                f.A.ConfigureAbilities(new PrototypeAbilitySettings { Skills = new[] { retargetSkill } });
                var spare = f.Add("B", 0, 4);
                f.Start(); f.Step();
                Check(f.A.Target == f.B, "Cast starts on nearest enemy");
                f.B.TakeDamage(10000);
                Check(f.A.Target == spare, "Immediate retarget after target death while casting");
                for (int i = 0; i < 10; i++) f.Step();
                Check(f.Events.Exists(e => e.Type == PrototypeCombatEventType.DamageDealt && e.Target == spare &&
                    e.SkillId == retargetSkill.Id && e.Amount == 40), "Cast safely selects surviving enemy in range");
            }
        }

        private static void TriggersAndEffects(PrototypeGameData data)
        {
            using (var f = new Fixture())
            {
                f.A.ConfigureAbilities(data.Units[1].Abilities);
                f.Start(); f.A.SetHealth(400); f.Step();
                Check(f.A.ActionState == PrototypeActionState.Casting, "Health threshold trigger");
                for (int i = 0; i < 9; i++) f.Step();
                Check(f.A.Shield == 70 && f.A.Statistics.ShieldGranted == 70, "Low-health one-time shield");
                int hp = f.A.CurrentHealth;
                f.A.ApplyShield(30, f.A); f.A.ReceiveDamage(80, f.B);
                Check(f.A.Shield == 20 && f.A.CurrentHealth == hp && f.A.MaxHealth == 1000, "Shield stacks and absorbs before HP");
                f.A.ReceiveDamage(25, f.B);
                Check(f.A.Shield == 0 && f.A.CurrentHealth == hp - 5, "Shield spillover to HP");
                for (int i = 0; i < 80; i++) f.Step();
                Check(f.A.Statistics.SkillCastCount == 1, "Health trigger once per combat");
                int restored = f.A.ApplyHeal(10000, f.A);
                Check(f.A.CurrentHealth == f.A.MaxHealth && f.A.Statistics.HealingDone == restored, "Heal clamp and effective healing stats");
                f.A.ApplyShield(10); f.Combat.FinishAsDraw();
                Check(f.A.Shield == 0 && f.A.SkillGauge == 0, "Finish clears transient defenses/gauge");
            }
            using (var f = new Fixture())
            {
                f.A.ConfigureAbilities(data.Units[3].Abilities);
                f.Start();
                for (int i = 0; i < 120; i++) f.Step();
                Check(f.A.Statistics.BasicAttackCount == 3 && f.A.Statistics.SkillCastCount == 1, "After three attacks cast");
                int before = f.B.CurrentHealth;
                while (f.A.Statistics.BasicAttackCount < 4) f.Step();
                Check(f.B.CurrentHealth == before - 36, "Next basic attack strengthened");
                before = f.B.CurrentHealth;
                while (f.A.Statistics.BasicAttackCount < 5) f.Step();
                Check(f.B.CurrentHealth == before - 20, "Next attack buff consumed exactly once");
            }
            using (var f = new Fixture())
            {
                f.A.ConfigureAbilities(data.Units[4].Abilities);
                f.Add("B", 0, 5);
                f.Start(); f.B.SetHealth(1); f.Step();
                for (int i = 0; i < 7 && f.A.ActionState != PrototypeActionState.Casting; i++) f.Step();
                Check(f.A.Statistics.Kills == 1 && f.A.ActionState == PrototypeActionState.Casting, "OnKill trigger");
                for (int i = 0; i < 10; i++) f.Step();
                Check(Mathf.Approximately(f.A.EffectiveStats.AttackSpeed, 1.5f), "AttackSpeed buff applied");
                for (int i = 0; i < 155; i++) f.Step();
                Check(Mathf.Approximately(f.A.EffectiveStats.AttackSpeed, 1), "Buff expires on simulation clock");
            }
            using (var f = new Fixture())
            {
                f.A.ConfigureAbilities(data.Units[7].Abilities);
                f.Start(); f.Step();
                Check(f.A.Statistics.SkillCastCount == 1 && f.A.Statistics.BasicAttackCount == 0, "OnCombatStart precedes basic");
                for (int i = 0; i < 9; i++) f.Step();
                Check(f.A.Shield == 50 && f.A.EffectiveStats.Defense == 15, "Composite shield and defense buff");
                f.Combat.ResetBattle();
                Check(f.A.EffectiveStats.Defense == 0 && f.A.Statistics.ShieldGranted == 0, "Reset clears buffs/statistics");
            }
            using (var f = new Fixture())
            {
                var ally = f.Add("A", 0, 0);
                f.A.ConfigureAbilities(data.Units[6].Abilities);
                f.A.Stats.SkillAmplification = 40;
                f.Start(); ally.SetHealth(500); f.A.SetHealth(900); f.A.GainGauge(100);
                for (int i = 0; i < 10; i++) f.Step();
                Check(ally.CurrentHealth == 559 && f.A.Statistics.HealingDone == 59, "Lowest-health ally skill healing");
                Check(f.Events.Exists(e => e.Type == PrototypeCombatEventType.HealApplied && e.Target == ally), "Heal event target");
            }
            using (var f = new Fixture())
            {
                f.A.Stats.AttackRange = 1;
                f.A.ConfigureAbilities(new PrototypeAbilitySettings { Skills = new[] { new PrototypeSkillDefinition {
                    Id = "dash", Trigger = PrototypeSkillTrigger.OnCombatStart, Range = 3,
                    Effects = new[] { new PrototypeSkillEffect { Type = PrototypeSkillEffectType.Dash, DashCells = 2 } } } } });
                f.Start(); var from = f.A.Cell;
                for (int i = 0; i < 10; i++) f.Step();
                Check(f.A.Cell != from && PrototypeCombatGrid.Distance(f.A.Cell, f.B.Cell) == 1, "Dash approaches enemy by adjacent cells");
                foreach (var e in f.Events)
                    if (e.Type == PrototypeCombatEventType.UnitMoved)
                        Check(PrototypeCombatGrid.Distance(e.From, e.To) == 1 && f.Combat.Grid.Contains(e.To), "Dash adjacency/bounds");
                Check(f.Combat.Grid.Occupant(f.A.Cell) == f.A && f.A.Cell != f.B.Cell, "Dash preserves occupancy");
            }
        }

        private static void DeterministicTeams(PrototypeGameData data)
        {
            string previous = null;
            for (int roster = 0; roster < 3; roster++)
            using (var f = new Fixture(false))
            {
                for (int row = 0; row < 3; row++)
                for (int team = 0; team < 2; team++)
                {
                    var definition = data.Units[(roster * 3 + row + team) % 9];
                    var unit = f.Add(team == 0 ? "A" : "B", row, team == 0 ? 1 : 4, data.CombatStats(definition.Id, 1));
                    unit.ConfigureAbilities(definition.Abilities);
                }
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    f.Start();
                    int ticks = 0;
                    while (f.Combat.State == PrototypeCombatState.Fighting && ticks++ < 3000)
                    {
                        f.Step();
                        var cells = new HashSet<Vector2Int>();
                        foreach (var unit in f.Units)
                            if (unit.IsAlive) Check(cells.Add(unit.Cell) && f.Combat.Grid.Occupant(unit.Cell) == unit, "3v3 unique occupancy");
                    }
                    Check(f.Combat.State == PrototypeCombatState.Finished, "3v3 skill combat completes within limit");
                    long dealt = 0, taken = 0; int kills = 0, dead = 0, casts = 0;
                    foreach (var unit in f.Units)
                    { dealt += unit.Statistics.DamageDealt; taken += unit.Statistics.DamageTaken; kills += unit.Statistics.Kills; casts += unit.Statistics.SkillCastCount; if (!unit.IsAlive) dead++; }
                    Check(dealt > 0 && dealt == taken && kills == dead && casts > 0, "Team statistics reconcile");
                    Check(f.Events.FindAll(e => e.Type == PrototypeCombatEventType.UnitSpawned).Count == 6 &&
                        f.Events.FindAll(e => e.Type == PrototypeCombatEventType.CombatFinished).Count == 1, "Spawn/final events exactly once");
                    string snapshot = f.Combat.Winner + "|" + ticks + "|" + f.Combat.StatisticsSummary();
                    if (repeat == 1) Check(snapshot == previous, "Same seed reproduces every statistic/result/tick");
                    previous = snapshot;
                }
            }
        }

        private static PrototypeSkillDefinition Skill(PrototypeSkillTrigger trigger, PrototypeSkillTarget target, PrototypeSkillEffect effect) =>
            new PrototypeSkillDefinition { Id = "test-skill", Trigger = trigger, Target = target, Effects = new[] { effect } };

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("Ability smoke fixture");
            public readonly List<PrototypeUnit> Units = new List<PrototypeUnit>();
            public readonly List<PrototypeCombatEvent> Events = new List<PrototypeCombatEvent>();
            public PrototypeUnit A => Units[0];
            public PrototypeUnit B => Units[1];
            public readonly PrototypeCombatController Combat;
            public Fixture(bool defaultPair = true)
            {
                Combat = root.AddComponent<PrototypeCombatController>(); Combat.enabled = false;
                Combat.EventRaised += Events.Add;
                if (defaultPair) { Add("A", 1, 1); Add("B", 1, 4); }
            }
            public PrototypeUnit Add(string faction, int row, int column, PrototypeCombatStats stats = null)
            {
                var obj = new GameObject("Ability test " + faction); obj.transform.SetParent(root.transform);
                var unit = obj.AddComponent<PrototypeUnit>();
                unit.Initialize(faction, row, column, stats ?? new PrototypeCombatStats { MaxHealth = 1000, Defense = 0, AttackRange = 3 }, null, null);
                Units.Add(unit); return unit;
            }
            public void Start() { Events.Clear(); Combat.Initialize(3, 6, Units, BattlefieldPrototype.Position, 2468); Combat.StartBattle(); }
            public void Step() => Combat.Step(0.02f);
            public void Dispose() => UnityEngine.Object.DestroyImmediate(root);
        }
        private static void Check(bool value, string message)
        { assertions++; if (!value) throw new Exception("Ability smoke: " + message); }
    }
}
