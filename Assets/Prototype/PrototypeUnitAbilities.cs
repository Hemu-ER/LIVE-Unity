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
        private PrototypeUnit basicHitTarget;
        private int survivingBasicHits;
        private bool resolvingLethalDamage;
        public PrototypeActionState ActionState { get; private set; }
        public float SkillGauge { get; private set; }
        public float MaxSkillGauge => Mathf.Max(1, abilities.MaxSkillGauge);
        public int Shield { get; private set; }
        public PrototypeCombatStatistics Statistics { get; private set; } = new PrototypeCombatStatistics();
        public IReadOnlyList<PrototypeSkillRuntime> Skills => skills.AsReadOnly();

        public void ConfigureAbilities(PrototypeAbilitySettings definition, int stars = 1)
        {
            // Deep copy prevents editing static definitions from mutating an in-progress instance.
            abilities = definition == null ? new PrototypeAbilitySettings() :
                JsonUtility.FromJson<PrototypeAbilitySettings>(JsonUtility.ToJson(definition));
            foreach (var skill in abilities.Skills)
            foreach (var effect in skill.Effects)
            {
                int index = Mathf.Clamp(stars, 1, 3) - 1;
                if (effect.AttackRatioByStar?.Length == 3) effect.AttackRatio = effect.AttackRatioByStar[index];
                if (effect.SkillRatioByStar?.Length == 3) effect.SkillRatio = effect.SkillRatioByStar[index];
                if (effect.BuffAmountByStar?.Length == 3) effect.BuffAmount = effect.BuffAmountByStar[index];
                if (effect.TargetMaxHealthRatioByStar?.Length == 3) effect.TargetMaxHealthRatio = effect.TargetMaxHealthRatioByStar[index];
                if (effect.ExecuteThresholdByStar?.Length == 3) effect.ExecuteThreshold = effect.ExecuteThresholdByStar[index];
                if (effect.HealCasterRatioByStar?.Length == 3) effect.HealCasterRatio = effect.HealCasterRatioByStar[index];
                if (effect.SourceMaxHealthRatioByStar?.Length == 3) effect.SourceMaxHealthRatio = effect.SourceMaxHealthRatioByStar[index];
            }
            foreach (var skill in abilities.Skills)
                foreach (var modifier in skill.StackModifiers ?? Array.Empty<PrototypeStackModifier>())
                    if (modifier.AmountPerStackByStar?.Length == 3) modifier.AmountPerStack = modifier.AmountPerStackByStar[Mathf.Clamp(stars, 1, 3) - 1];
            ResetAbilities();
        }

        private void ResetAbilities()
        {
            skills.Clear(); statuses.Clear(); delayedEffects.Clear(); chargeRefills.Clear(); LastHealthDamage = 0;
            basicHitTarget = null; survivingBasicHits = 0; resolvingLethalDamage = false;
            foreach (var definition in abilities.Skills ?? Array.Empty<PrototypeSkillDefinition>())
                skills.Add(new PrototypeSkillRuntime(definition) { NextTriggerAt = definition.FirstTriggerSeconds });
            buffs.Clear(); casting = null; castTarget = null; castRemaining = 0;
            SkillGauge = 0; Shield = 0; combatStarted = false;
            ActionState = PrototypeActionState.Idle;
            Statistics = new PrototypeCombatStatistics();
        }

        internal void MarkCombatStarted()
        {
            combatStarted = true;
            StartCharges();
            foreach (var runtime in skills)
                if (runtime.Definition.Trigger == PrototypeSkillTrigger.OnCombatStart && runtime.Definition.Execution == PrototypeSkillExecution.Instant) TryBeginCast(runtime);
        }
        public PrototypeCombatStats EffectiveStats
        {
            get
            {
                var effective = stats.CopyValidated();
                effective.AttackPower = PrototypeDamageCalculator.RoundAmount(CombatAttackPower);
                effective.Defense = PrototypeDamageCalculator.RoundAmount(CombatDefense);
                effective.SkillAmplification = (float)ModifiedStat(stats.SkillAmplification, PrototypeBuffStat.SkillAmplification);
                effective.AttackSpeed = (float)ModifiedStat(stats.AttackSpeed, PrototypeBuffStat.AttackSpeed);
                effective.CriticalChance = (float)ModifiedStat(stats.CriticalChance, PrototypeBuffStat.CriticalChance);
                effective.DefensePenetration = (float)ModifiedStat(stats.DefensePenetration, PrototypeBuffStat.DefensePenetration);
                effective.MoveSpeed = (float)ModifiedStat(stats.MoveSpeed, PrototypeBuffStat.MoveSpeed);
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
                if (buffs[i].Permanent) continue;
                if (Now + 0.000001 >= buffs[i].Expires) buffs.RemoveAt(i);
            }
            TickMechanisms();
            if (!IsAlive || combat.State != PrototypeCombatState.Fighting || IsControlled) return true;
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
                if (runtime.Definition.Execution == PrototypeSkillExecution.Cast && runtime.Definition.Trigger == PrototypeSkillTrigger.GaugeFull && TryBeginCast(runtime)) return true;
            foreach (var runtime in skills)
                if (runtime.Definition.Execution == PrototypeSkillExecution.Cast && runtime.Definition.Trigger != PrototypeSkillTrigger.GaugeFull && TryBeginCast(runtime)) return true;
            return false;
        }

        private bool TriggerReady(PrototypeSkillRuntime runtime)
        {
            var definition = runtime.Definition;
            if (definition.OncePerCombat && (definition.ReserveNextBasic ? runtime.ReservationActivationCount : runtime.CastCount) > 0) return false;
            switch (definition.Trigger)
            {
                case PrototypeSkillTrigger.GaugeFull: return SkillGauge >= MaxSkillGauge;
                case PrototypeSkillTrigger.AfterNAttacks: return (definition.CountOnlySurvivingHits ? survivingBasicHits : Statistics.BasicAttackCount) - runtime.LastAttackCount >= Mathf.Max(1, definition.AttacksRequired);
                case PrototypeSkillTrigger.HealthBelowPercent: return (float)CurrentHealth / MaxHealth <= definition.HealthThreshold;
                case PrototypeSkillTrigger.OnKill: return Statistics.Kills > runtime.LastKillCount;
                case PrototypeSkillTrigger.OnCombatStart: return combatStarted && runtime.CastCount == 0;
                case PrototypeSkillTrigger.Periodic: return Now + 0.000001 >= runtime.NextTriggerAt;
                case PrototypeSkillTrigger.StatusAtLeast: return StatusStacks(definition.RequiredStatus) >= definition.RequiredStacks;
                default: return false;
            }
        }

        private PrototypeUnit SelectSkillTarget(PrototypeSkillDefinition definition)
        {
            switch (definition.Target)
            {
                case PrototypeSkillTarget.Self: return this;
                case PrototypeSkillTarget.NearestEnemy: return combat.FindNearestEnemy(this);
                case PrototypeSkillTarget.LowestHealthAlly: return combat.LowestHealthAlly(this);
                default: Retarget(); return Target;
            }
        }

        private bool InSkillRange(PrototypeSkillDefinition definition, PrototypeUnit target)
        {
            if (target == null || !target.IsAlive) return false;
            return definition.IgnoreRange || (definition.Target != PrototypeSkillTarget.CurrentTarget && definition.Target != PrototypeSkillTarget.NearestEnemy) ||
                PrototypeCombatGrid.Distance(Cell, target.Cell) <= (definition.Range > 0 ? definition.Range : EffectiveStats.AttackRange);
        }

        private bool TryBeginCast(PrototypeSkillRuntime runtime, bool reactive = false)
        {
            if (!IsAlive || combat.State != PrototypeCombatState.Fighting ||
                (IsControlled && !runtime.Definition.CanRunWhileControlled) || Now + 0.000001 < runtime.NextReadyAt ||
                (runtime.Definition.OncePerCombat && (runtime.Definition.ReserveNextBasic ? !reactive && runtime.ReservationActivationCount > 0 : runtime.CastCount > 0)) || (!reactive && !TriggerReady(runtime))) return false;
            var charge = runtime.Definition.Charge;
            if (charge != null && !string.IsNullOrEmpty(charge.Key) && StatusStacks(charge.Key) < 1) return false;
            if (runtime.Definition.ReserveNextBasic && !reactive)
            {
                if (!string.IsNullOrEmpty(runtime.Definition.RequiredSkillId) &&
                    !skills.Exists(source => source.Definition.Id == runtime.Definition.RequiredSkillId && source.CastCount > 0 && source.LastAttackCount == Statistics.BasicAttackCount)) return false;
                if (runtime.NextBasicReserved) return false;
                runtime.NextBasicReserved = true;
                runtime.ReservedAttacksRemaining = Math.Max(1, runtime.Definition.ReservedAttackCount);
                runtime.ReservationActivationCount++;
                runtime.LastAttackCount = runtime.Definition.CountOnlySurvivingHits ? survivingBasicHits : Statistics.BasicAttackCount;
                combat.Publish(new PrototypeCombatEvent(PrototypeCombatEventType.NextAttackReserved, this, skillId: runtime.Definition.Id));
                return false; // Reservation is not a cast or an immediate extra hit.
            }
            if (!string.IsNullOrEmpty(runtime.Definition.CustomHandlerKey) && !combat.SkillHandlers.ContainsKey(runtime.Definition.CustomHandlerKey)) return false;
            if (!string.IsNullOrEmpty(runtime.Definition.RequiredHitStatus) &&
                (basicHitTarget == null || !basicHitTarget.IsAlive || basicHitTarget.StatusStacks(runtime.Definition.RequiredHitStatus) < runtime.Definition.RequiredStacks)) return false;
            var target = SelectSkillTarget(runtime.Definition);
            if (!InSkillRange(runtime.Definition, target)) return false;
            if (runtime.Definition.Execution == PrototypeSkillExecution.Cast)
            { casting = runtime; castTarget = target; castRemaining = Mathf.Max(0, runtime.Definition.CastSeconds); ActionState = PrototypeActionState.Casting; }
            runtime.NextReadyAt = Now + runtime.Definition.CooldownSeconds;
            if (runtime.Definition.Trigger == PrototypeSkillTrigger.Periodic)
                runtime.NextTriggerAt = runtime.Definition.CoalesceMissedPeriods
                    ? runtime.Definition.FirstTriggerSeconds + (Math.Floor((Now - runtime.Definition.FirstTriggerSeconds + 0.000001) / runtime.Definition.IntervalSeconds) + 1) * runtime.Definition.IntervalSeconds
                    : runtime.NextTriggerAt + runtime.Definition.IntervalSeconds;
            ConsumeCharge(runtime.Definition);
            runtime.CastCount++;
            if (!runtime.Definition.ReserveNextBasic) runtime.LastAttackCount = runtime.Definition.CountOnlySurvivingHits ? survivingBasicHits : Statistics.BasicAttackCount;
            runtime.LastKillCount = Statistics.Kills;
            SkillGauge = 0;
            if (runtime.Definition.ConsumeHitStatus)
                basicHitTarget.ApplyStatus(runtime.Definition.RequiredHitStatus, -runtime.Definition.RequiredStacks, int.MaxValue, 1, source: this, skillId: runtime.Definition.Id);
            Statistics.SkillCastCount++;
            combat.Publish(new PrototypeCombatEvent(PrototypeCombatEventType.SkillCast, this, target, skillId: runtime.Definition.Id));
            if (runtime.Definition.Execution == PrototypeSkillExecution.Instant) ApplySkill(runtime.Definition, target);
            return true;
        }

        private void ApplySkill(PrototypeSkillDefinition skill, PrototypeUnit target)
        {
            if (!InSkillRange(skill, target)) return;
            if (!string.IsNullOrEmpty(skill.CustomHandlerKey))
            { combat.SkillHandlers[skill.CustomHandlerKey].Execute(this, target, skill); return; }
            if (skill.ResolvePerTarget)
            {
                // Web effects mutate the caster between allies; keep heal -> buff ordering per target.
                foreach (var recipient in EffectTargets(skill.Effects[0], target))
                foreach (var effect in skill.Effects) ApplyResolvedEffect(skill, effect, target, recipient);
                return;
            }
            foreach (var effect in skill.Effects)
            {
                if (!IsAlive || combat.State != PrototypeCombatState.Fighting) break;
                if (effect.DelaySeconds > 0)
                    delayedEffects.Add(new DelayedEffect { Due = Now + effect.DelaySeconds, Skill = skill, Effect = effect, Anchor = target });
                else ApplyResolvedEffect(skill, effect, target);
            }
        }

        private void DashToward(PrototypeUnit target, int cells, string skillId)
        {
            if (IsMoving) InterruptAction();
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
            int damage = PrototypeDamageCalculator.Mitigate(CombatAttackPower * Math.Max(0, multiplier),
                target.CombatDefense, effective.DefensePenetration, critical);
            double lifesteal = Math.Max(0, ModifiedStat(0, PrototypeBuffStat.Lifesteal));
            string lifestealSkillId = null;
            foreach (var buff in buffs) if (buff.Stat == PrototypeBuffStat.Lifesteal && (buff.Permanent || buff.Expires > Now + 0.000001)) lifestealSkillId = buff.SkillId;
            target.ReceiveDamage(damage, this, critical, isBasic: true);
            buffs.RemoveAll(buff => buff.ConsumeOnAttack);
            // This heal belongs to the already resolved hit, including the killing blow.
            if (IsAlive) ApplyHeal(PrototypeDamageCalculator.RoundAmount(target.LastHealthDamage * lifesteal), this, lifestealSkillId);
            if (IsAlive && combat.State == PrototypeCombatState.Fighting)
            {
                basicHitTarget = target;
                if (target.IsAlive) survivingBasicHits++;
                foreach (var runtime in skills)
                    if (runtime.NextBasicReserved && (!runtime.Definition.PreserveReservationOnLethalBasic || target.IsAlive))
                    {
                        runtime.ReservedAttacksRemaining = Math.Max(0, runtime.ReservedAttacksRemaining - 1);
                        runtime.NextBasicReserved = runtime.ReservedAttacksRemaining > 0;
                        if (runtime.Definition.RestartCountOnConsume) runtime.LastAttackCount = Statistics.BasicAttackCount;
                        combat.Publish(new PrototypeCombatEvent(PrototypeCombatEventType.NextAttackConsumed, this, target, skillId: runtime.Definition.Id));
                        if (TryBeginCast(runtime, true))
                            foreach (var followup in skills)
                                if (followup.Definition.Trigger == PrototypeSkillTrigger.OnReservedBasicResolved && followup.Definition.RequiredSkillId == runtime.Definition.Id)
                                    TryBeginCast(followup, true);
                    }
                FireReactiveSkills(PrototypeSkillTrigger.OnBasicHit);
                foreach (var runtime in skills)
                    if (runtime.Definition.Execution == PrototypeSkillExecution.Instant && runtime.Definition.Trigger == PrototypeSkillTrigger.AfterNAttacks &&
                        !(runtime.Definition.ReserveNextBasic && !string.IsNullOrEmpty(runtime.Definition.RequiredSkillId))) TryBeginCast(runtime);
                // Linked reservations are armed only after their immediate source has resolved.
                foreach (var runtime in skills)
                    if (runtime.Definition.ReserveNextBasic && !string.IsNullOrEmpty(runtime.Definition.RequiredSkillId)) TryBeginCast(runtime);
            }
            basicHitTarget = null;
            GainGauge(abilities.GaugePerAttack);
        }

        public int ReceiveDamage(int damage, PrototypeUnit source, bool critical = false, string skillId = null, bool isBasic = false)
        {
            if (resolvingLethalDamage) return 0; // Observers cannot reenter a replacement transaction.
            LastHealthDamage = 0;
            if (!IsAlive || damage <= 0) return 0;
            if (HasStatus(PrototypeStatusKind.Invulnerable)) { RecordPrevention(damage, source, skillId); return 0; }
            int incoming = damage;
            double reduction = ModifiedStat(0, PrototypeBuffStat.DamageReduction) + (isBasic ? ModifiedStat(0, PrototypeBuffStat.BasicDamageReduction) : 0);
            damage = PrototypeDamageCalculator.RoundAmount(damage * (1 - Math.Clamp(reduction, 0, 1)));
            RecordPrevention(incoming - damage, source, skillId);
            int absorbed = Mathf.Min(Shield, damage);
            Shield -= absorbed;
            int hpLoss = Mathf.Min(Mathf.Max(0, currentHealth - (HasStatus(PrototypeStatusKind.Immortal) ? 1 : 0)), damage - absorbed);
            int actual = absorbed + hpLoss;
            currentHealth -= hpLoss;
            LastHealthDamage = hpLoss;
            if (currentHealth == 0)
            {
                InterruptAction();
                resolvingLethalDamage = true;
                try
                {
                    foreach (var runtime in skills)
                    {
                        if (runtime.Definition.Trigger != PrototypeSkillTrigger.OnLethalDamage) continue;
                        if (TryBeginCast(runtime, true) && currentHealth > 0)
                        {
                            combat.Publish(new PrototypeCombatEvent(PrototypeCombatEventType.LethalDamageReplaced, this, this, currentHealth, runtime.Definition.Id));
                            break; // One successful replacement per incoming lethal hit.
                        }
                    }
                }
                finally { resolvingLethalDamage = false; }
                if (HasStatus(PrototypeStatusKind.Immortal) && currentHealth == 0) { currentHealth = 1; actual--; LastHealthDamage--; }
            }
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
            else
            {
                // Nonlethal health reactions resolve before the next incoming damage/execute effect.
                foreach (var runtime in skills)
                    if (runtime.Definition.ReactAfterDamage) TryBeginCast(runtime);
                if (actual > 0) GainGauge(abilities.GaugePerHit);
            } // Shield-only hits also generate gauge; lethal hits do not.
            return actual;
        }

        private void RecordPrevention(int amount, PrototypeUnit source, string skillId)
        {
            if (amount <= 0) return;
            Statistics.DamagePrevented += amount;
            combat?.Publish(new PrototypeCombatEvent(PrototypeCombatEventType.DamagePrevented, source, this, amount, skillId));
        }

        public int ApplyHeal(int amount, PrototypeUnit source = null, string skillId = null)
        {
            if (!IsAlive || amount <= 0) return 0;
            int actual = Mathf.Min(amount, MaxHealth - currentHealth);
            currentHealth += actual;
            if (source != null) source.Statistics.HealingDone += actual;
            RefreshHealth();
            combat?.Publish(new PrototypeCombatEvent(PrototypeCombatEventType.HealApplied, source, this, actual, skillId));
            return actual;
        }

        public int ApplyShield(int amount, PrototypeUnit source = null, string skillId = null)
        {
            if (!IsAlive || amount <= 0) return 0;
            int actual = (int)Math.Min(amount, (long)int.MaxValue - Shield);
            Shield += actual;
            if (source != null) source.Statistics.ShieldGranted += actual;
            combat?.Publish(new PrototypeCombatEvent(PrototypeCombatEventType.ShieldApplied, source, this, actual, skillId));
            return actual;
        }

        public void ApplyBuff(PrototypeSkillEffect effect, PrototypeUnit source = null, string skillId = null)
        {
            if (!IsAlive || (!effect.Permanent && effect.Duration <= 0)) return;
            if (!string.IsNullOrEmpty(effect.Key)) buffs.RemoveAll(buff => buff.Key == effect.Key && buff.Stat == effect.Stat);
            buffs.Add(new PrototypeActiveBuff { Stat = effect.Stat, Amount = effect.BuffAmount,
                Expires = effect.Permanent ? double.PositiveInfinity : Now + effect.Duration, ConsumeOnAttack = effect.ConsumeOnAttack, Key = effect.Key, SkillId = skillId,
                Multiplicative = effect.Multiplicative, Permanent = effect.Permanent });
            combat?.Publish(new PrototypeCombatEvent(PrototypeCombatEventType.BuffApplied, source, this, skillId: skillId, effectKey: effect.Key));
        }

        private void CancelAbilities(bool died)
        {
            statuses.Clear(); delayedEffects.Clear(); chargeRefills.Clear();
            foreach (var runtime in skills) { runtime.NextBasicReserved = false; runtime.ReservedAttacksRemaining = 0; }
            casting = null; castTarget = null; buffs.Clear(); SkillGauge = 0; Shield = 0;
            ActionState = died || !IsAlive ? PrototypeActionState.Dead : PrototypeActionState.Idle;
        }
    }
}
