using System;
using System.Collections.Generic;
using UnityEngine;

namespace LIVE.Prototype
{
    // Explicit opt-in extension boundary. No character-name branches or reflection-based handlers.
    public interface IPrototypeSkillHandler
    {
        void Execute(PrototypeUnit caster, PrototypeUnit target, PrototypeSkillDefinition definition);
    }

    public sealed partial class PrototypeUnit
    {
        private sealed class Status
        { public int Stacks; public double Expires; public PrototypeStatusKind Kind; }
        private sealed class DelayedEffect
        { public double Due; public PrototypeSkillDefinition Skill; public PrototypeSkillEffect Effect; public PrototypeUnit Anchor; }
        private readonly Dictionary<string, Status> statuses = new Dictionary<string, Status>(StringComparer.Ordinal);
        private readonly List<DelayedEffect> delayedEffects = new List<DelayedEffect>();
        private readonly Dictionary<string, double> chargeRefills = new Dictionary<string, double>(StringComparer.Ordinal);
        public bool ChargeRefillPending(string key) => chargeRefills.ContainsKey(key);

        private void StartCharges()
        {
            foreach (var runtime in skills)
            {
                var charge = runtime.Definition.Charge;
                if (charge == null || string.IsNullOrEmpty(charge.Key)) continue;
                ApplyStatus(charge.Key, charge.Capacity, charge.Capacity, 1, permanent: true, source: this, skillId: runtime.Definition.Id);
            }
        }

        private void RefillCharges()
        {
            foreach (var runtime in skills)
            {
                var charge = runtime.Definition.Charge;
                if (charge == null || string.IsNullOrEmpty(charge.Key) || !chargeRefills.TryGetValue(charge.Key, out var due) || Now + 0.000001 < due) continue;
                chargeRefills.Remove(charge.Key);
                ApplyStatus(charge.Key, charge.Capacity, charge.Capacity, 1, permanent: true, source: this, skillId: runtime.Definition.Id);
            }
        }

        private void ConsumeCharge(PrototypeSkillDefinition definition)
        {
            var charge = definition.Charge;
            if (charge == null || string.IsNullOrEmpty(charge.Key)) return;
            ApplyStatus(charge.Key, -1, charge.Capacity, 1, permanent: true, source: this, skillId: definition.Id);
            if (StatusStacks(charge.Key) == 0 && !chargeRefills.ContainsKey(charge.Key))
                chargeRefills.Add(charge.Key, Now + charge.RefillSeconds);
        }

        public int LastHealthDamage { get; private set; }
        public bool IsControlled => HasStatus(PrototypeStatusKind.CrowdControl);
        public double CombatAttackPower => Math.Max(0, ModifiedStat(stats.AttackPower, PrototypeBuffStat.AttackPower));
        public double CombatDefense => Math.Max(0, ModifiedStat(stats.Defense, PrototypeBuffStat.Defense));
        private double Now => combat == null ? 0 : combat.ElapsedSeconds;

        public int StatusStacks(string key) => key != null && statuses.TryGetValue(key, out var status) && status.Expires > Now + 0.000001 ? status.Stacks : 0;
        public bool HasStatus(PrototypeStatusKind kind)
        {
            foreach (var status in statuses.Values)
                if (status.Kind == kind && status.Stacks > 0 && status.Expires > Now + 0.000001) return true;
            return false;
        }

        public void ApplyStatus(string key, int delta, int limit, float duration, PrototypeStatusKind kind = PrototypeStatusKind.Generic, bool permanent = false, PrototypeUnit source = null, string skillId = null)
        {
            if (!IsAlive || string.IsNullOrEmpty(key) || limit < 1 || float.IsNaN(duration) || float.IsInfinity(duration) || (!permanent && duration <= 0)) return;
            if (kind == PrototypeStatusKind.CrowdControl && HasStatus(PrototypeStatusKind.CrowdControlImmune)) return;
            int before = StatusStacks(key);
            int count = (int)Math.Max(0, Math.Min(limit, (long)before + delta));
            if (count == 0)
            {
                statuses.Remove(key);
                if (before > 0) combat?.Publish(new PrototypeCombatEvent(PrototypeCombatEventType.StatusConsumed, source, this, before, skillId, effectKey: key));
                return;
            }
            double expiry = permanent ? double.PositiveInfinity : Now + duration;
            if (statuses.TryGetValue(key, out var previous)) expiry = Math.Max(expiry, previous.Expires);
            statuses[key] = new Status { Stacks = count, Expires = expiry, Kind = kind };
            if (delta < 0 && count < before)
                combat?.Publish(new PrototypeCombatEvent(PrototypeCombatEventType.StatusConsumed, source, this, before - count, skillId, effectKey: key));
            if (kind == PrototypeStatusKind.CrowdControl) InterruptAction();
            combat?.Publish(new PrototypeCombatEvent(PrototypeCombatEventType.StatusApplied, source, this, count, skillId, effectKey: key));
        }

        private void InterruptAction()
        {
            casting = null; castTarget = null; castRemaining = 0;
            if (IsMoving)
            {
                combat.Grid.CancelReservation(this);
                transform.position = combat.WorldPosition(Cell);
            }
            IsMoving = false; attackFlash = 0;
            if (character != null) character.localPosition = Vector3.zero;
            ActionState = IsAlive ? PrototypeActionState.Idle : PrototypeActionState.Dead;
        }

        private double ModifiedStat(double basis, PrototypeBuffStat stat)
        {
            double multiplier = 1;
            foreach (var buff in buffs)
            {
                if (buff.Stat != stat || (!buff.Permanent && Now + 0.000001 >= buff.Expires)) continue;
                if (buff.Multiplicative) multiplier *= Math.Max(0, 1 + buff.Amount);
                else basis += buff.Amount;
            }
            // Derived from reservation state: no separately timed buff can leak after cancellation.
            foreach (var runtime in skills)
                if (runtime.NextBasicReserved)
                    foreach (var modifier in runtime.Definition.ReservationModifiers ?? Array.Empty<PrototypeReservationModifier>())
                        if (modifier.Stat == stat) multiplier *= modifier.Multiplier;
            foreach (var runtime in skills)
                foreach (var modifier in runtime.Definition.StackModifiers ?? Array.Empty<PrototypeStackModifier>())
                    if (modifier.Stat == stat)
                        multiplier *= Math.Max(0, 1 + StatusStacks(modifier.Key) * PrototypeDamageCalculator.AuthoredRatio(modifier.AmountPerStack));
            return basis * multiplier;
        }

        private void TickMechanisms()
        {
            RefillCharges();
            // Copy before resolving: effects may kill a unit and clear its pending work.
            var due = delayedEffects.FindAll(effect => effect.Due <= Now + 0.000001);
            delayedEffects.RemoveAll(effect => effect.Due <= Now + 0.000001);
            foreach (var pending in due)
                if (IsAlive && combat.State == PrototypeCombatState.Fighting && pending.Anchor != null && pending.Anchor.IsAlive)
                    ApplyResolvedEffect(pending.Skill, pending.Effect, pending.Anchor);
            foreach (var runtime in skills)
                if (runtime.Definition.Execution == PrototypeSkillExecution.Instant && !runtime.Definition.ReactAfterDamage && runtime.Definition.Trigger != PrototypeSkillTrigger.OnBasicHit &&
                    runtime.Definition.Trigger != PrototypeSkillTrigger.OnLethalDamage) TryBeginCast(runtime);
        }

        private void FireReactiveSkills(PrototypeSkillTrigger trigger)
        {
            foreach (var runtime in skills)
                if (runtime.Definition.Trigger == trigger) TryBeginCast(runtime, true);
        }

        private List<PrototypeUnit> EffectTargets(PrototypeSkillEffect effect, PrototypeUnit anchor)
        {
            if (effect.Anchor == PrototypeEffectAnchor.Caster) anchor = this;
            else if (effect.Anchor == PrototypeEffectAnchor.BasicHitTarget) anchor = basicHitTarget;
            if (anchor == null) return new List<PrototypeUnit>();
            if (effect.Area == PrototypeEffectArea.Single) return new List<PrototypeUnit> { anchor };
            var result = new List<PrototypeUnit>();
            foreach (var unit in combat.Units)
            {
                if (unit == null || !unit.IsAlive) continue;
                bool ally = unit.Faction == Faction;
                int distance = effect.ChebyshevRadius ? Mathf.Max(Mathf.Abs(unit.Row - anchor.Row), Mathf.Abs(unit.Column - anchor.Column)) : PrototypeCombatGrid.Distance(unit.Cell, anchor.Cell);
                bool include = false;
                switch (effect.Area)
                {
                    case PrototypeEffectArea.AllEnemies: include = !ally; break;
                    case PrototypeEffectArea.AllAllies: include = ally; break;
                    case PrototypeEffectArea.TargetRow: include = !ally && unit.Row == anchor.Row; break;
                    case PrototypeEffectArea.TargetColumn: include = !ally && unit.Column == anchor.Column; break;
                    case PrototypeEffectArea.AdjacentEnemies: include = !ally && distance <= effect.Radius; break;
                    case PrototypeEffectArea.NearbyAllies: include = ally && distance <= effect.Radius; break;
                }
                if (include) result.Add(unit);
            }
            result.Sort((a, b) => string.CompareOrdinal(a.CombatId, b.CombatId));
            return result;
        }

        private void ApplyResolvedEffect(PrototypeSkillDefinition skill, PrototypeSkillEffect effect, PrototypeUnit anchor, PrototypeUnit recipient = null)
        {
            long healthDamage = 0;
            foreach (var target in recipient != null ? new List<PrototypeUnit> { recipient } : EffectTargets(effect, anchor))
            {
                if (!IsAlive || combat.State != PrototypeCombatState.Fighting) break;
                if (target == null || !target.IsAlive) continue;
                var effective = EffectiveStats;
                double raw = PrototypeDamageCalculator.Coefficients(effect, CombatAttackPower, effective.SkillAmplification, MaxHealth, target.MaxHealth, target.CurrentHealth);
                switch (effect.Type)
                {
                    case PrototypeSkillEffectType.Damage:
                        if (effect.Area == PrototypeEffectArea.Single && !InSkillRange(skill, target)) break;
                        int damage = effect.TrueDamage ? PrototypeDamageCalculator.RoundAmount(raw, 1) :
                            PrototypeDamageCalculator.Mitigate(raw, target.CombatDefense, effective.DefensePenetration);
                        target.ReceiveDamage(damage, this, false, skill.Id);
                        healthDamage += target.LastHealthDamage;
                        break;
                    case PrototypeSkillEffectType.Heal: target.ApplyHeal(PrototypeDamageCalculator.RoundAmount(raw, skill.Trigger == PrototypeSkillTrigger.OnLethalDamage ? 1 : 0), this, skill.Id); break;
                    case PrototypeSkillEffectType.Shield: target.ApplyShield(PrototypeDamageCalculator.RoundAmount(raw), this, skill.Id); break;
                    case PrototypeSkillEffectType.StatBuff: target.ApplyBuff(effect, this, skill.Id); break;
                    case PrototypeSkillEffectType.Dash: DashToward(target, effect.DashCells, skill.Id); break;
                    case PrototypeSkillEffectType.Status: target.ApplyStatus(effect.Key, effect.StackDelta, effect.StackLimit, effect.Duration, effect.StatusKind, effect.Permanent, this, skill.Id); break;
                    case PrototypeSkillEffectType.Execute:
                        // Web Garnet's execute is a direct death after its threshold check, not damage.
                        if ((float)target.CurrentHealth / target.MaxHealth <= effect.ExecuteThreshold)
                        { Statistics.Kills++; Statistics.Executions++; combat.Publish(new PrototypeCombatEvent(PrototypeCombatEventType.UnitExecuted, this, target, target.CurrentHealth, skill.Id)); target.SetHealth(0); }
                        break;
                }
            }
            if (effect.HealCasterRatio > 0 && IsAlive)
                ApplyHeal(PrototypeDamageCalculator.RoundAmount(healthDamage * (effect.PreserveAuthoredPrecision ? PrototypeDamageCalculator.AuthoredRatio(effect.HealCasterRatio) : (double)effect.HealCasterRatio)), this, skill.Id);
        }
    }
}
