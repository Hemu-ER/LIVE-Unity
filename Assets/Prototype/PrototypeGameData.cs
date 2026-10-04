using System;
using System.Collections.Generic;
using UnityEngine;

namespace LIVE.Prototype
{
    [Serializable]
    public sealed class PrototypeUnitDefinition
    {
        public string Id;
        public string DisplayName;
        public int Cost;
        public PrototypeCombatStats Stats;
        public PrototypeArchetype Archetype;
        public PrototypeDataMode DataMode;
        public string Role, MainStat, AssetReferenceId, UnityImplementationStatus;
        public string[] Affiliations = Array.Empty<string>();
        public bool PveOnly, WebImplemented;
        // Unbound extension points, never synthesized from character names.
        public string ActiveDefinitionReference, PassiveDefinitionReference;
        public bool Playable => !PveOnly && Cost >= 1 && Cost <= 3;
        public PrototypeAbilitySettings Abilities = new PrototypeAbilitySettings();
    }

    [Serializable]
    public sealed class PrototypeShopOdds
    {
        public int MinLevel;
        public int MaxLevel;
        public int[] Weights;
    }

    public enum PrototypeOpponentType { Player, Wildlife }

    [Serializable]
    public sealed class PrototypeEnemyEntry
    {
        public string UnitId;
        public int Stars = 1;
        public int Row;
        public int Column;
    }

    [Serializable]
    public sealed class PrototypeEnemyRound
    {
        public PrototypeOpponentType OpponentType;
        public PrototypeEnemyEntry[] Units;
    }

    [Serializable]
    public sealed class PrototypeRules
    {
        public int StartingCredits = 5;
        public int RoundIncome = 5;
        public int InterestCap = 3;
        public int NaturalMasteryExp = 4;
        public int InvestCost = 2;
        public int InvestExp = 2;
        public int RerollCost = 2;
        public int BenchSize = 8;
        public int ShopSize = 5;
        public float PrepSeconds = 30;
        public float CombatSeconds = 60;
        public float ResultSeconds = 2;
        public float TransitionSeconds = 1;
        public int[] CopiesByCost = { 18, 15, 12 };
        public float[] StarMultipliers = { 1f, 1.8f, 3.2f };
        public PrototypeShopOdds[] ShopOdds;
    }

    // Authored JSON is the source of truth; this is static definition data, never owned/combat state.
    [Serializable]
    public sealed class PrototypeGameData
    {
        public PrototypeRules Rules;
        public PrototypeUnitDefinition[] Units;
        public PrototypeEnemyRound[] EnemyRounds;
        private Dictionary<string, PrototypeUnitDefinition> lookup;

        public static PrototypeGameData Load()
        {
            var asset = Resources.Load<TextAsset>("PrototypeGameData");
            if (asset == null) throw new InvalidOperationException("Missing PrototypeGameData.json.");
            var data = JsonUtility.FromJson<PrototypeGameData>(asset.text);
            data.Validate();
            return data;
        }

        public void Validate()
        {
            if (Rules == null || Units == null || EnemyRounds == null || EnemyRounds.Length == 0)
                throw new InvalidOperationException("Incomplete prototype data.");
            lookup = new Dictionary<string, PrototypeUnitDefinition>(StringComparer.Ordinal);
            foreach (var definition in Units)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id) ||
                    string.IsNullOrWhiteSpace(definition.DisplayName) || definition.Cost < (definition.PveOnly ? 0 : 1) || definition.Cost > 3 ||
                    definition.Stats == null || lookup.ContainsKey(definition.Id))
                    throw new InvalidOperationException("Invalid or duplicate unit definition.");
                ValidateAbilities(definition);
                lookup.Add(definition.Id, definition);
            }
            for (int level = 1; level <= 20; level++)
            {
                int matches = 0;
                foreach (var odds in Rules.ShopOdds)
                {
                    if (level < odds.MinLevel || level > odds.MaxLevel) continue;
                    matches++;
                    if (odds.Weights == null || odds.Weights.Length != 3 ||
                        odds.Weights[0] < 0 || odds.Weights[1] < 0 || odds.Weights[2] < 0 ||
                        odds.Weights[0] + odds.Weights[1] + odds.Weights[2] != 100)
                        throw new InvalidOperationException("Shop odds must sum to 100.");
                }
                if (matches != 1) throw new InvalidOperationException("Shop odds must cover each mastery level exactly once.");
            }
            if (Rules.CopiesByCost.Length != 3 || Rules.StarMultipliers.Length != 3 ||
                Rules.BenchSize < 1 || Rules.ShopSize < 1 || Rules.PrepSeconds <= 0 || Rules.CombatSeconds <= 0)
                throw new InvalidOperationException("Invalid rule settings.");
            foreach (var round in EnemyRounds)
            {
                var cells = new HashSet<int>();
                foreach (var unit in round.Units)
                    if (!lookup.ContainsKey(unit.UnitId) || unit.Stars < 1 || unit.Stars > 3 ||
                        unit.Row < 0 || unit.Row >= 3 || unit.Column < 3 || unit.Column >= 6 ||
                        !cells.Add(unit.Row * 6 + unit.Column))
                        throw new InvalidOperationException("Invalid enemy team placement.");
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void ValidateAbilities(PrototypeUnitDefinition unit)
        {
            var stats = unit.Stats;
            var abilities = unit.Abilities;
            if (!Enum.IsDefined(typeof(PrototypeArchetype), unit.Archetype) ||
                stats.MaxHealth < 1 || stats.AttackPower < 0 || stats.Defense < 0 || stats.AttackRange < 1 ||
                !Finite(stats.AttackSpeed) || stats.AttackSpeed <= 0 || !Finite(stats.MoveSpeed) || stats.MoveSpeed <= 0 ||
                !Finite(stats.SkillAmplification) || stats.SkillAmplification < 0 ||
                !Finite(stats.CriticalChance) || stats.CriticalChance < 0 || stats.CriticalChance > 1 ||
                !Finite(stats.DefensePenetration) || stats.DefensePenetration < 0 || abilities == null ||
                !Finite(abilities.MaxSkillGauge) || abilities.MaxSkillGauge <= 0 ||
                !Finite(abilities.GaugePerAttack) || abilities.GaugePerAttack < 0 ||
                !Finite(abilities.GaugePerHit) || abilities.GaugePerHit < 0 || abilities.Skills == null)
                throw new InvalidOperationException("Invalid combat stats/abilities: " + unit.Id);
            foreach (var followup in abilities.Skills)
                if (followup != null && followup.Trigger == PrototypeSkillTrigger.OnReservedBasicResolved &&
                    !Array.Exists(abilities.Skills, candidate => candidate != null && candidate.Id == followup.RequiredSkillId && candidate.ReserveNextBasic))
                    throw new InvalidOperationException("Missing reserved skill for followup: " + unit.Id);
            foreach (var reservation in abilities.Skills)
                if (reservation != null && reservation.ReserveNextBasic && !string.IsNullOrEmpty(reservation.RequiredSkillId) &&
                    !Array.Exists(abilities.Skills, source => source != null && source.Id == reservation.RequiredSkillId && !source.ReserveNextBasic &&
                        source.Trigger == PrototypeSkillTrigger.AfterNAttacks && source.Execution == PrototypeSkillExecution.Instant))
                    throw new InvalidOperationException("Missing immediate attack-count source for reservation: " + unit.Id);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var skill in abilities.Skills)
            {
                if (skill == null || string.IsNullOrWhiteSpace(skill.Id) || !ids.Add(skill.Id) ||
                    !Enum.IsDefined(typeof(PrototypeSkillTrigger), skill.Trigger) ||
                    !Enum.IsDefined(typeof(PrototypeSkillTarget), skill.Target) ||
                    !Finite(skill.CastSeconds) || skill.CastSeconds < 0 || skill.Range < 0 || skill.AttacksRequired < 1 ||
                    !Finite(skill.HealthThreshold) || skill.HealthThreshold < 0 || skill.HealthThreshold > 1 ||
                    skill.Effects == null || skill.Effects.Length == 0)
                    throw new InvalidOperationException("Invalid skill: " + unit.Id);
                ValidateMechanisms(skill);
                foreach (var effect in skill.Effects)
                    if (effect == null || !Enum.IsDefined(typeof(PrototypeSkillEffectType), effect.Type) ||
                        !Enum.IsDefined(typeof(PrototypeBuffStat), effect.Stat) ||
                        !Finite(effect.BaseDamage) || effect.BaseDamage < 0 ||
                        !Finite(effect.AttackRatio) || effect.AttackRatio < 0 ||
                        !Finite(effect.SkillRatio) || effect.SkillRatio < 0 ||
                        !Finite(effect.BuffAmount) || !Finite(effect.Duration) || effect.Duration <= 0 || effect.DashCells < 0)
                        throw new InvalidOperationException("Invalid effect: " + skill.Id);
            }
        }

        public static void ValidateMechanisms(PrototypeSkillDefinition skill)
        {
            if (!Enum.IsDefined(typeof(PrototypeSkillExecution), skill.Execution) ||
                !Finite(skill.FirstTriggerSeconds) || skill.FirstTriggerSeconds < 0 ||
                !Finite(skill.IntervalSeconds) || skill.IntervalSeconds <= 0 ||
                !Finite(skill.CooldownSeconds) || skill.CooldownSeconds < 0 ||
                (skill.Trigger == PrototypeSkillTrigger.StatusAtLeast && (string.IsNullOrEmpty(skill.RequiredStatus) || skill.RequiredStacks < 1)) ||
                ((skill.Trigger == PrototypeSkillTrigger.OnBasicHit || skill.Trigger == PrototypeSkillTrigger.OnLethalDamage) && skill.Execution != PrototypeSkillExecution.Instant) ||
                (skill.Trigger == PrototypeSkillTrigger.OnLethalDamage && (!skill.OncePerCombat || skill.Target != PrototypeSkillTarget.Self)))
                throw new InvalidOperationException("Invalid trigger configuration: " + skill.Id);
            if (skill.Charge != null && !string.IsNullOrEmpty(skill.Charge.Key) &&
                (skill.Trigger != PrototypeSkillTrigger.OnBasicHit || skill.Execution != PrototypeSkillExecution.Instant || skill.Charge.Capacity < 1 ||
                 !Finite(skill.Charge.RefillSeconds) || skill.Charge.RefillSeconds <= 0))
                throw new InvalidOperationException("Charge requires immediate hit trigger and valid capacity/refill");
            if (skill.Trigger == PrototypeSkillTrigger.OnReservedBasicResolved &&
                (skill.Execution != PrototypeSkillExecution.Instant || string.IsNullOrEmpty(skill.RequiredSkillId)))
                throw new InvalidOperationException("Reserved-hit followup requires a source skill");
            if (skill.RestartCountOnConsume && !skill.ReserveNextBasic)
                throw new InvalidOperationException("Consume counter reset requires a reservation");
            foreach (var modifier in skill.ReservationModifiers ?? Array.Empty<PrototypeReservationModifier>())
                if (!skill.ReserveNextBasic || modifier == null || !Enum.IsDefined(typeof(PrototypeBuffStat), modifier.Stat) ||
                    !Finite(modifier.Multiplier) || modifier.Multiplier <= 0)
                    throw new InvalidOperationException("Invalid reservation-bound modifier");
            if (skill.ReserveNextBasic && (skill.Trigger != PrototypeSkillTrigger.AfterNAttacks || skill.Execution != PrototypeSkillExecution.Instant || skill.Target != PrototypeSkillTarget.Self))
                throw new InvalidOperationException("Next basic reservation requires instant attack-count/self definition");
            if ((!string.IsNullOrEmpty(skill.RequiredHitStatus) && (skill.Trigger != PrototypeSkillTrigger.OnBasicHit || skill.RequiredStacks < 1)) ||
                (skill.ConsumeHitStatus && string.IsNullOrEmpty(skill.RequiredHitStatus)) ||
                (skill.CountOnlySurvivingHits && skill.Trigger != PrototypeSkillTrigger.AfterNAttacks))
                throw new InvalidOperationException("Invalid hit trigger configuration");
            if (skill.ReactAfterDamage && (skill.Execution != PrototypeSkillExecution.Instant || skill.Trigger != PrototypeSkillTrigger.HealthBelowPercent))
                throw new InvalidOperationException("Damage reactions require an instant health condition");
            foreach (var effect in skill.Effects)
            {
                if (skill.ResolvePerTarget && (effect.DelaySeconds != 0 || effect.Area != skill.Effects[0].Area || effect.Radius != skill.Effects[0].Radius || effect.ChebyshevRadius != skill.Effects[0].ChebyshevRadius))
                    throw new InvalidOperationException("Per-target effects require a shared selector and no delay");
                if (!Enum.IsDefined(typeof(PrototypeEffectAnchor), effect.Anchor) ||
                    (effect.Anchor == PrototypeEffectAnchor.BasicHitTarget && ((skill.Trigger != PrototypeSkillTrigger.OnBasicHit && !skill.ReserveNextBasic && !(skill.Trigger == PrototypeSkillTrigger.AfterNAttacks && skill.Execution == PrototypeSkillExecution.Instant)) || effect.DelaySeconds != 0)))
                    throw new InvalidOperationException("Basic-hit anchor requires immediate hit trigger");
                if (!Enum.IsDefined(typeof(PrototypeEffectArea), effect.Area) || !Enum.IsDefined(typeof(PrototypeStatusKind), effect.StatusKind) ||
                    effect.Radius < 0 || !Finite(effect.DelaySeconds) || effect.DelaySeconds < 0 ||
                    !Finite(effect.SourceMaxHealthRatio) || effect.SourceMaxHealthRatio < 0 ||
                    !Finite(effect.TargetMaxHealthRatio) || effect.TargetMaxHealthRatio < 0 ||
                    !Finite(effect.TargetCurrentHealthRatio) || effect.TargetCurrentHealthRatio < 0 ||
                    !Finite(effect.HealCasterRatio) || effect.HealCasterRatio < 0 ||
                    !Finite(effect.ExecuteThreshold) || effect.ExecuteThreshold < 0 || effect.ExecuteThreshold > 1 ||
                    (effect.Type == PrototypeSkillEffectType.Status && (string.IsNullOrEmpty(effect.Key) || effect.StackLimit < 1)) ||
                    (skill.Trigger == PrototypeSkillTrigger.OnLethalDamage && (effect.Area != PrototypeEffectArea.Single || effect.DelaySeconds > 0 ||
                        (effect.Type != PrototypeSkillEffectType.Heal && effect.Type != PrototypeSkillEffectType.Status && effect.Type != PrototypeSkillEffectType.StatBuff))))
                    throw new InvalidOperationException("Invalid mechanism effect: " + skill.Id);
                foreach (var values in new[] { effect.AttackRatioByStar, effect.SkillRatioByStar, effect.BuffAmountByStar, effect.TargetMaxHealthRatioByStar, effect.ExecuteThresholdByStar, effect.HealCasterRatioByStar, effect.SourceMaxHealthRatioByStar })
                {
                    if (values == null || values.Length == 0) continue;
                    if (values.Length != 3) throw new InvalidOperationException("Expected three star coefficients: " + skill.Id);
                    foreach (float value in values) if (!Finite(value)) throw new InvalidOperationException("Nonfinite star coefficient");
                }
            }
        }

        public PrototypeUnitDefinition Definition(string id) => lookup[id];
        public PrototypeEnemyRound EnemyTeam(int round) => EnemyRounds[Math.Min(Math.Max(round - 1, 0), EnemyRounds.Length - 1)];

        public int[] Odds(int masteryLevel)
        {
            foreach (var odds in Rules.ShopOdds)
                if (masteryLevel >= odds.MinLevel && masteryLevel <= odds.MaxLevel)
                    return (int[])odds.Weights.Clone();
            throw new ArgumentOutOfRangeException(nameof(masteryLevel));
        }

        public PrototypeCombatStats CombatStats(string id, int stars)
        {
            var stats = Definition(id).Stats.CopyValidated();
            float scale = Rules.StarMultipliers[Math.Clamp(stars, 1, 3) - 1];
            stats.MaxHealth = Math.Max(1, (int)Math.Round(stats.MaxHealth * scale, MidpointRounding.AwayFromZero));
            stats.AttackPower = Math.Max(0, (int)Math.Round(stats.AttackPower * scale, MidpointRounding.AwayFromZero));
            return stats;
        }
    }
}
