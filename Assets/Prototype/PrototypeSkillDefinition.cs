using System;

namespace LIVE.Prototype
{
    public enum PrototypeArchetype { Fighter, RangedAttack, RangedSkill, Tank, MeleeSkill, Assassin, Support, PveReference }
    public enum PrototypeSkillTrigger { GaugeFull, AfterNAttacks, HealthBelowPercent, OnKill, OnCombatStart }
    public enum PrototypeSkillTarget { CurrentTarget, Self, LowestHealthAlly }
    public enum PrototypeSkillEffectType { Damage, Heal, Shield, StatBuff, Dash }
    public enum PrototypeBuffStat { AttackPower, SkillAmplification, Defense, AttackSpeed, CriticalChance, DefensePenetration, MoveSpeed, NextAttackMultiplier }
    public enum PrototypeActionState { Idle, Moving, BasicAttacking, Casting, Dead }

    [Serializable]
    public sealed class PrototypeSkillEffect
    {
        public PrototypeSkillEffectType Type;
        public float BaseDamage;
        public float AttackRatio;
        public float SkillRatio;
        public PrototypeBuffStat Stat;
        public float BuffAmount;
        public float Duration = 3;
        public bool ConsumeOnAttack;
        public int DashCells = 2;
    }

    [Serializable]
    public sealed class PrototypeSkillDefinition
    {
        public string Id;
        public PrototypeSkillTrigger Trigger;
        public PrototypeSkillTarget Target;
        public int AttacksRequired = 3;
        public float HealthThreshold = 0.4f;
        public bool OncePerCombat;
        public float CastSeconds = 0.15f;
        // 0 uses the caster's AttackRange. Self/ally effects do not require enemy range.
        public int Range;
        public PrototypeSkillEffect[] Effects = Array.Empty<PrototypeSkillEffect>();
    }

    [Serializable]
    public sealed class PrototypeAbilitySettings
    {
        public float MaxSkillGauge = 100;
        public float GaugePerAttack = 20;
        public float GaugePerHit = 10;
        public PrototypeSkillDefinition[] Skills = Array.Empty<PrototypeSkillDefinition>();
    }

    // Runtime counters are per COMBAT instance, never written into the definition or owned unit.
    public sealed class PrototypeSkillRuntime
    {
        public PrototypeSkillDefinition Definition { get; }
        public int CastCount { get; internal set; }
        internal int LastAttackCount;
        internal int LastKillCount;
        public PrototypeSkillRuntime(PrototypeSkillDefinition definition) { Definition = definition; }
    }

    internal sealed class PrototypeActiveBuff
    {
        internal PrototypeBuffStat Stat;
        internal float Amount;
        internal float Remaining;
        internal bool ConsumeOnAttack;
    }
}
