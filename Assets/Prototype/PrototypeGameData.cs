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
                    string.IsNullOrWhiteSpace(definition.DisplayName) || definition.Cost < 1 || definition.Cost > 3 ||
                    definition.Stats == null || lookup.ContainsKey(definition.Id))
                    throw new InvalidOperationException("Invalid or duplicate unit definition.");
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
