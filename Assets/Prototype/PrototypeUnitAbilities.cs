using System;
using System.Collections.Generic;
using UnityEngine;

namespace LIVE.Prototype
{
    public sealed partial class PrototypeUnit
    {
        private PrototypeAbilitySettings abilities = new PrototypeAbilitySettings();
        private readonly List<PrototypeSkillRuntime> skills = new List<PrototypeSkillRuntime>();
        private readonly List<PrototypeActiveBuff> buffs = new List<PrototypeActiveBuff>();
        private PrototypeSkillRuntime casting;
        private PrototypeUnit castTarget;
        private float castRemaining;
        private bool combatStarted;
        public PrototypeActionState ActionState { get; private set; }
        public float SkillGauge { get; private set; }
        public float MaxSkillGauge => Mathf.Max(1, abilities.MaxSkillGauge);
        public int Shield { get; private set; }
        public PrototypeCombatStatistics Statistics { get; private set; } = new PrototypeCombatStatistics();
        public IReadOnlyList<PrototypeSkillRuntime> Skills => skills.AsReadOnly();

        public void ConfigureAbilities(PrototypeAbilitySettings definition)
        {
            // Deep copy prevents editing static definitions from mutating an in-progress instance.
            abilities = definition == null ? new PrototypeAbilitySettings() :
                JsonUtility.FromJson<PrototypeAbilitySettings>(JsonUtility.ToJson(definition));
            ResetAbilities();
        }

        private void ResetAbilities()
        {
            skills.Clear();
            foreach (var definition in abilities.Skills ?? Array.Empty<PrototypeSkillDefinition>())
                skills.Add(new PrototypeSkillRuntime(definition));
            buffs.Clear(); casting = null; castTarget = null; castRemaining = 0;
            SkillGauge = 0; Shield = 0; combatStarted = false;
            ActionState = PrototypeActionState.Idle;
            Statistics = new PrototypeCombatStatistics();
        }

        internal void MarkCombatStarted() { combatStarted = true; }
        public PrototypeCombatStats EffectiveStats
        {
            get
            {
                var effective = stats.CopyValidated();
                foreach (var buff in buffs)
                {
                    switch (buff.Stat)
                    {
                        case PrototypeBuffStat.AttackPower: effective.AttackPower += Mathf.RoundToInt(buff.Amount); break;
                        case PrototypeBuffStat.SkillAmplification: effective.SkillAmplification += buff.Amount; break;
                        case PrototypeBuffStat.Defense: effective.Defense += Mathf.RoundToInt(buff.Amount); break;
                        case PrototypeBuffStat.AttackSpeed: effective.AttackSpeed += buff.Amount; break;
                        case PrototypeBuffStat.CriticalChance: effective.CriticalChance += buff.Amount; break;
                        case PrototypeBuffStat.DefensePenetration: effective.DefensePenetration += buff.Amount; break;
                        case PrototypeBuffStat.MoveSpeed: effective.MoveSpeed += buff.Amount; break;
                    }
                }
                return effective.CopyValidated();
            }
        }

        public void GainGauge(float amount)
        {
            if (!IsAlive || combat == null || combat.State != PrototypeCombatState.Fighting ||
                float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0) return;
            SkillGauge = Mathf.Min(MaxSkillGauge, SkillGauge + amount);
        }

        private bool TickAbilities(float seconds)
        {
            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                buffs[i].Remaining -= seconds;
                if (buffs[i].Remaining <= 0) buffs.RemoveAt(i);
            }
            if (ActionState == PrototypeActionState.Casting)
            {
                castRemaining -= seconds;
                if (castRemaining <= 0)
                {
                    var skill = casting;
                    var target = castTarget;
                    casting = null; castTarget = null;
                    if (IsAlive && combat.State == PrototypeCombatState.Fighting && skill != null)
                    {
                        if (target == null || !target.IsAlive) target = SelectSkillTarget(skill.Definition);
                        ApplySkill(skill.Definition, target);
                    }
                    if (IsAlive && combat.State == PrototypeCombatState.Fighting) ActionState = PrototypeActionState.Idle;
                }
                return true;
            }
            if (IsMoving || (ActionState == PrototypeActionState.BasicAttacking && attackFlash > 0)) return false;
            if (attackFlash <= 0) ActionState = PrototypeActionState.Idle;
            Retarget();
            // Full-gauge casts take precedence; stable definition order resolves other simultaneous triggers.
            foreach (var runtime in skills)
                if (runtime.Definition.Trigger == PrototypeSkillTrigger.GaugeFull && TryBeginCast(runtime)) return true;
            foreach (var runtime in skills)
                if (runtime.Definition.Trigger != PrototypeSkillTrigger.GaugeFull && TryBeginCast(runtime)) return true;
            return false;
        }

        private bool TriggerReady(PrototypeSkillRuntime runtime)
        {
            var definition = runtime.Definition;
            if (definition.OncePerCombat && runtime.CastCount > 0) return false;
            switch (definition.Trigger)
            {
                case PrototypeSkillTrigger.GaugeFull: return SkillGauge >= MaxSkillGauge;
                case PrototypeSkillTrigger.AfterNAttacks: return Statistics.BasicAttackCount - runtime.LastAttackCount >= Mathf.Max(1, definition.AttacksRequired);
                case PrototypeSkillTrigger.HealthBelowPercent: return (float)CurrentHealth / MaxHealth <= definition.HealthThreshold;
                case PrototypeSkillTrigger.OnKill: return Statistics.Kills > runtime.LastKillCount;
                case PrototypeSkillTrigger.OnCombatStart: return combatStarted && runtime.CastCount == 0;
                default: return false;
            }
        }

        private PrototypeUnit SelectSkillTarget(PrototypeSkillDefinition definition)
        {
            switch (definition.Target)
            {
                case PrototypeSkillTarget.Self: return this;
                case PrototypeSkillTarget.LowestHealthAlly: return combat.LowestHealthAlly(this);
                default: Retarget(); return Target;
            }
        }

        private bool InSkillRange(PrototypeSkillDefinition definition, PrototypeUnit target)
        {
            if (target == null || !target.IsAlive) return false;
            return definition.Target != PrototypeSkillTarget.CurrentTarget ||
                PrototypeCombatGrid.Distance(Cell, target.Cell) <= (definition.Range > 0 ? definition.Range : EffectiveStats.AttackRange);
        }

        private bool TryBeginCast(PrototypeSkillRuntime runtime)
        {
            if (!IsAlive || combat.State != PrototypeCombatState.Fighting || !TriggerReady(runtime)) return false;
            var target = SelectSkillTarget(runtime.Definition);
            if (!InSkillRange(runtime.Definition, target)) return false;
            casting = runtime; castTarget = target;
            castRemaining = Mathf.Max(0, runtime.Definition.CastSeconds);
            runtime.CastCount++;
            runtime.LastAttackCount = Statistics.BasicAttackCount;
            runtime.LastKillCount = Statistics.Kills;
            SkillGauge = 0;
            Statistics.SkillCastCount++;
            ActionState = PrototypeActionState.Casting;
            combat.Publish(new PrototypeCombatEvent(PrototypeCombatEventType.SkillCast, this, target, skillId: runtime.Definition.Id));
            return true;
        }

        private void ApplySkill(PrototypeSkillDefinition skill, PrototypeUnit target)
        {
            if (!InSkillRange(skill, target)) return; // Revalidate on release; interrupted/dead-target casts may fizzle.
            foreach (var effect in skill.Effects)
            {
                if (!IsAlive || combat.State != PrototypeCombatState.Fighting) break;
                var effective = EffectiveStats;
                double raw = PrototypeDamageCalculator.Coefficients(effect, effective);
                switch (effect.Type)
                {
                    case PrototypeSkillEffectType.Damage:
                        if (InSkillRange(skill, target)) target.ReceiveDamage(PrototypeDamageCalculator.Mitigate(raw, target.EffectiveStats.Defense, effective.DefensePenetration), this, false, skill.Id);
                        break;
                    case PrototypeSkillEffectType.Heal: target.ApplyHeal(PrototypeDamageCalculator.RoundAmount(raw), this); break;
                    case PrototypeSkillEffectType.Shield: target.ApplyShield(PrototypeDamageCalculator.RoundAmount(raw), this); break;
                    case PrototypeSkillEffectType.StatBuff: target.ApplyBuff(effect); break;
                    case PrototypeSkillEffectType.Dash: DashToward(target, effect.DashCells, skill.Id); break;
                }
            }
        }

        private void DashToward(PrototypeUnit target, int cells, string skillId)
        {
            // Reuse reserved adjacent steps, including short detours around the target. Never jump through occupied cells.
            var visited = new HashSet<Vector2Int> { Cell };
            bool approaching = PrototypeCombatGrid.Distance(Cell, target.Cell) > Stats.AttackRange;
            for (int i = 0; i < Mathf.Clamp(cells, 0, 6) && target.IsAlive; i++)
            {
                if (!combat.Grid.TryFindStep(this, target, out var next, visited) || !combat.Grid.TryReserveStep(this, next)) break;
                var from = Cell;
                combat.Grid.CompleteStep(this, next); Cell = next;
                visited.Add(Cell);
                transform.position = combat.WorldPosition(Cell);
                combat.Publish(new PrototypeCombatEvent(PrototypeCombatEventType.UnitMoved, this, skillId: skillId, from: from, to: Cell));
                if (approaching && PrototypeCombatGrid.Distance(Cell, target.Cell) <= Stats.AttackRange) break;
            }
        }

        private void PerformBasicAttack()
        {
            var target = Target;
            if (!IsAlive || target == null || !target.IsAlive || combat.State != PrototypeCombatState.Fighting) return;
            var effective = EffectiveStats;
            double multiplier = 1;
            foreach (var buff in buffs) if (buff.Stat == PrototypeBuffStat.NextAttackMultiplier) multiplier += buff.Amount;
            bool critical = PrototypeDamageCalculator.RollCritical(effective.CriticalChance, combat.Random);
            ActionState = PrototypeActionState.BasicAttacking;
            Statistics.BasicAttackCount++;
            combat.Publish(new PrototypeCombatEvent(PrototypeCombatEventType.BasicAttackStarted, this, target));
            int damage = PrototypeDamageCalculator.Mitigate(effective.AttackPower * Math.Max(0, multiplier),
                target.EffectiveStats.Defense, effective.DefensePenetration, critical);
            target.ReceiveDamage(damage, this, critical);
            buffs.RemoveAll(buff => buff.ConsumeOnAttack);
            GainGauge(abilities.GaugePerAttack);
        }

        public int ReceiveDamage(int damage, PrototypeUnit source, bool critical = false, string skillId = null)
        {
            if (!IsAlive || damage <= 0) return 0;
            int absorbed = Mathf.Min(Shield, damage);
            Shield -= absorbed;
            int hpLoss = Mathf.Min(currentHealth, damage - absorbed);
            int actual = absorbed + hpLoss;
            currentHealth -= hpLoss;
            Statistics.DamageTaken += actual;
            if (source != null)
            {
                source.Statistics.DamageDealt += actual;
                if (currentHealth == 0 && source != this) source.Statistics.Kills++;
            }
            RefreshHealth();
            combat?.Publish(new PrototypeCombatEvent(PrototypeCombatEventType.DamageDealt, source, this, actual, skillId));
            if (critical) combat?.Publish(new PrototypeCombatEvent(PrototypeCombatEventType.CriticalHit, source, this, actual));
            if (currentHealth == 0) SetHealth(0);
            else GainGauge(abilities.GaugePerHit); // Shield-only hits also generate gauge; lethal hits do not.
            return actual;
        }

        public int ApplyHeal(int amount, PrototypeUnit source = null)
        {
            if (!IsAlive || amount <= 0) return 0;
            int actual = Mathf.Min(amount, MaxHealth - currentHealth);
            currentHealth += actual;
            if (source != null) source.Statistics.HealingDone += actual;
            RefreshHealth();
            combat?.Publish(new PrototypeCombatEvent(PrototypeCombatEventType.HealApplied, source, this, actual));
            return actual;
        }

        public int ApplyShield(int amount, PrototypeUnit source = null)
        {
            if (!IsAlive || amount <= 0) return 0;
            int actual = (int)Math.Min(amount, (long)int.MaxValue - Shield);
            Shield += actual;
            if (source != null) source.Statistics.ShieldGranted += actual;
            combat?.Publish(new PrototypeCombatEvent(PrototypeCombatEventType.ShieldApplied, source, this, actual));
            return actual;
        }

        public void ApplyBuff(PrototypeSkillEffect effect)
        {
            if (!IsAlive || effect.Duration <= 0) return;
            buffs.Add(new PrototypeActiveBuff { Stat = effect.Stat, Amount = effect.BuffAmount,
                Remaining = effect.Duration, ConsumeOnAttack = effect.ConsumeOnAttack });
        }

        private void CancelAbilities(bool died)
        {
            casting = null; castTarget = null; buffs.Clear(); SkillGauge = 0; Shield = 0;
            ActionState = died || !IsAlive ? PrototypeActionState.Dead : PrototypeActionState.Idle;
        }
    }
}
