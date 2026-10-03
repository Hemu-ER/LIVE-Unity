using UnityEngine;

namespace LIVE.Prototype
{
    public enum PrototypeCombatEventType
    {
        UnitSpawned, UnitMoved, BasicAttackStarted, DamageDealt, CriticalHit,
        SkillCast, HealApplied, ShieldApplied, UnitDied, CombatFinished, StatusApplied, BuffApplied, DamagePrevented, UnitExecuted, StatusConsumed
    }

    public sealed class PrototypeCombatEvent
    {
        public PrototypeCombatEventType Type { get; }
        public PrototypeUnit Source { get; }
        public PrototypeUnit Target { get; }
        public int Amount { get; }
        public string SkillId { get; }
        public string EffectKey { get; }
        public Vector2Int From { get; }
        public Vector2Int To { get; }
        public PrototypeCombatEvent(PrototypeCombatEventType type, PrototypeUnit source = null,
            PrototypeUnit target = null, int amount = 0, string skillId = null,
            Vector2Int from = default, Vector2Int to = default, string effectKey = null)
        { Type = type; Source = source; Target = target; Amount = amount; SkillId = skillId; EffectKey = effectKey; From = from; To = to; }
    }

    public sealed class PrototypeCombatStatistics
    {
        // Effective damage includes shield absorption, excludes HP overkill.
        public long DamageDealt { get; internal set; }
        public long DamageTaken { get; internal set; }
        public long DamagePrevented { get; internal set; }
        public int Executions { get; internal set; }
        public long HealingDone { get; internal set; }
        public long ShieldGranted { get; internal set; }
        public int Kills { get; internal set; }
        public int BasicAttackCount { get; internal set; }
        public int SkillCastCount { get; internal set; }
        public override string ToString() =>
            $"Damage {DamageDealt}/{DamageTaken}, Heal {HealingDone}, Shield {ShieldGranted}, Kills {Kills}, Attacks {BasicAttackCount}, Skills {SkillCastCount}";
    }
}
