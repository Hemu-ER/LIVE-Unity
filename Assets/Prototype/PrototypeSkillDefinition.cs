using System;

namespace LIVE.Prototype
{
    public enum PrototypeArchetype { Fighter, RangedAttack, RangedSkill, Tank, MeleeSkill, Assassin, Support, PveReference }
    public enum PrototypeSkillTrigger { GaugeFull, AfterNAttacks, HealthBelowPercent, OnKill, OnCombatStart, Periodic, OnBasicHit, StatusAtLeast, OnLethalDamage }
    public enum PrototypeSkillTarget { CurrentTarget, Self, LowestHealthAlly }
    public enum PrototypeSkillEffectType { Damage, Heal, Shield, StatBuff, Dash, Status, Execute }
    public enum PrototypeBuffStat { AttackPower, SkillAmplification, Defense, AttackSpeed, CriticalChance, DefensePenetration, MoveSpeed, NextAttackMultiplier, DamageReduction, BasicDamageReduction, Lifesteal }
    public enum PrototypeSkillExecution { Cast, Instant }
    public enum PrototypeEffectArea { Single, AllEnemies, AllAllies, TargetRow, TargetColumn, AdjacentEnemies, NearbyAllies }
    public enum PrototypeStatusKind { Generic, CrowdControl, Invulnerable, Immortal, CrowdControlImmune }
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
        public PrototypeEffectArea Area;
        public int Radius = 1;
        public bool ChebyshevRadius;
        public bool Multiplicative, Permanent, TrueDamage;
        public string Key;
        public float DelaySeconds;
        public float SourceMaxHealthRatio, TargetMaxHealthRatio, TargetCurrentHealthRatio, HealCasterRatio;
        public PrototypeStatusKind StatusKind;
        public int StackDelta = 1, StackLimit = 1;
        public float ExecuteThreshold;
        public float[] AttackRatioByStar, SkillRatioByStar, BuffAmountByStar;
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
        public PrototypeSkillExecution Execution;
        public float FirstTriggerSeconds, IntervalSeconds = 1, CooldownSeconds;
        public bool CanRunWhileControlled;
        public string RequiredStatus, CustomHandlerKey;
        public int RequiredStacks = 1;
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
        internal double NextTriggerAt, NextReadyAt;
        public PrototypeSkillRuntime(PrototypeSkillDefinition definition) { Definition = definition; }
    }

    internal sealed class PrototypeActiveBuff
    {
        internal PrototypeBuffStat Stat;
        internal float Amount;
        internal float Remaining;
        internal bool ConsumeOnAttack;
        internal bool Multiplicative, Permanent;
        internal string Key;
    }
}
