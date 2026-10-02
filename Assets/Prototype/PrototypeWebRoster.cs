using System;
using System.Linq;
using UnityEngine;

namespace LIVE.Prototype
{
    public enum PrototypeDataMode { TestFixtures, WebRoster }

    // Source fields retain their original names; absent fields are not invented in the snapshot.
    [Serializable] public sealed class PrototypeWebStats
    { public int hp, atk, amp, range, def; public float @as; }
    [Serializable] public sealed class PrototypeWebAsset { public string sd; }
    [Serializable] public sealed class PrototypeWebCharacter
    {
        public string id, name, role, main;
        public int cost;
        public string[] affiliations;
        public PrototypeWebStats baseStats;
        public bool implemented, pveOnly;
        public PrototypeWebAsset asset;
        public bool Playable => !pveOnly && cost >= 1 && cost <= 3;
    }
    [Serializable] public sealed class PrototypeWebRoster
    {
        public string SourceRepository, SourceCommit, SourceRosterBlob;
        public PrototypeWebCharacter[] Entries;
        public static PrototypeWebRoster Load()
        {
            var asset = Resources.Load<TextAsset>("WebRoster");
            if (asset == null) throw new InvalidOperationException("Missing WebRoster.json");
            var source = JsonUtility.FromJson<PrototypeWebRoster>(asset.text);
            if (source.Entries == null || source.Entries.Length == 0 || string.IsNullOrEmpty(source.SourceCommit))
                throw new InvalidOperationException("Invalid web roster provenance/data");
            return source;
        }

        public PrototypeGameData CreateGameData()
        {
            // Retain the existing Unity economy, pool, growth and enemy formation rules.
            var data = PrototypeGameData.Load();
            data.Units = Entries.Select(Convert).ToArray();
            string[] enemyMapping = { "hyunwoo", "kenneth", "abigail", "adela", "yuki", "rio", "dailin", "nicky", "shurin" };
            foreach (var round in data.EnemyRounds)
                foreach (var enemy in round.Units)
                    enemy.UnitId = enemyMapping[int.Parse(enemy.UnitId.Substring("test-unit-".Length)) - 1];
            data.Validate();
            return data;
        }

        private static PrototypeUnitDefinition Convert(PrototypeWebCharacter row)
        {
            if (row == null || row.baseStats == null || row.affiliations == null || row.asset == null)
                throw new InvalidOperationException("Incomplete web character");
            // Runtime policy, NOT additional source stats: no crit/penetration; moveInterval=.5 -> 2 cells/s.
            return new PrototypeUnitDefinition
            {
                Id = row.id, DisplayName = row.name, Cost = row.cost, DataMode = PrototypeDataMode.WebRoster,
                Role = row.role, Affiliations = (string[])row.affiliations.Clone(), MainStat = row.main,
                PveOnly = row.pveOnly, WebImplemented = row.implemented, AssetReferenceId = row.asset.sd,
                UnityImplementationStatus = "BasicCombatOnly", Archetype = Role(row.role),
                Stats = new PrototypeCombatStats { MaxHealth = row.baseStats.hp, AttackPower = row.baseStats.atk,
                    SkillAmplification = row.baseStats.amp, Defense = row.baseStats.def,
                    AttackSpeed = row.baseStats.@as, AttackRange = row.baseStats.range,
                    MoveSpeed = 2, CriticalChance = 0, DefensePenetration = 0 },
                Abilities = new PrototypeAbilitySettings { Skills = Array.Empty<PrototypeSkillDefinition>() }
            };
        }

        private static PrototypeArchetype Role(string role)
        {
            switch (role)
            {
                case "전사": return PrototypeArchetype.Fighter;
                case "원거리 평타": return PrototypeArchetype.RangedAttack;
                case "원거리 스킬": return PrototypeArchetype.RangedSkill;
                case "탱커": return PrototypeArchetype.Tank;
                case "근거리 스킬": return PrototypeArchetype.MeleeSkill;
                case "암살자": return PrototypeArchetype.Assassin;
                case "서포터": return PrototypeArchetype.Support;
                case "야생동물": return PrototypeArchetype.PveReference;
                default: throw new InvalidOperationException("Unmapped web role: " + role);
            }
        }
    }
}
