using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LIVE.Prototype
{
    // Ratios use 0.1 for 10%; CriticalChance is an additive percentage-point ratio.
    public enum PrototypeItemStat { AttackPower, SkillAmplification, MaxHealth, Defense, AttackSpeedPercent, CriticalChance, DefensePenetration }
    [Serializable] public sealed class PrototypeItemModifier { public PrototypeItemStat Stat; public float Amount; }
    [Serializable] public sealed class PrototypeItemDefinition
    {
        public string Id, DisplayName, AssetKey, EffectId, EffectDescription;
        public bool Completed;
        public int SalePrice;
        public PrototypeItemModifier[] Modifiers;
        public string Summary => string.Join(" / ", Modifiers.Select(m => m.Stat == PrototypeItemStat.AttackSpeedPercent ? $"AS +{m.Amount*100:0.#}%" :
            m.Stat == PrototypeItemStat.CriticalChance ? $"Crit +{m.Amount*100:0.#}%p" : $"{m.Stat} +{m.Amount:0.#}"));
    }
    [Serializable] public sealed class PrototypeItemRecipe { public string First, Second, Result; }
    [Serializable] public sealed class PrototypeItemCatalog
    {
        public PrototypeItemDefinition[] Items;
        public PrototypeItemRecipe[] Recipes;
        public static PrototypeItemCatalog Load()
        {
            var asset = Resources.Load<TextAsset>("PrototypeItems");
            if (asset == null) throw new InvalidOperationException("Missing PrototypeItems data.");
            var catalog = JsonUtility.FromJson<PrototypeItemCatalog>(asset.text); catalog.Validate(); return catalog;
        }
        public PrototypeItemDefinition Find(string id) => Items.FirstOrDefault(i => i.Id == id);
        public string Combine(string first, string second) => Recipes.FirstOrDefault(r =>
            (r.First == first && r.Second == second) || (r.First == second && r.Second == first))?.Result;
        public void Validate()
        {
            if (Items == null || Recipes == null || Items.Length != 35 || Recipes.Length != 28 || Items.Count(i=>!i.Completed)!=7)
                throw new InvalidOperationException("Expected 7 base items and 28 completed recipes.");
            var ids = new HashSet<string>(); var pairs = new HashSet<string>(); var results = new HashSet<string>();
            foreach (var item in Items)
            {
                if (string.IsNullOrEmpty(item.Id) || !ids.Add(item.Id) || string.IsNullOrEmpty(item.DisplayName) || string.IsNullOrEmpty(item.AssetKey) ||
                    item.SalePrice != (item.Completed ? 5 : 2) || item.Modifiers == null || item.Modifiers.Any(m => !Enum.IsDefined(typeof(PrototypeItemStat),m.Stat) || float.IsNaN(m.Amount) || float.IsInfinity(m.Amount)))
                    throw new InvalidOperationException("Invalid item definition.");
            }
            foreach (var r in Recipes)
            {
                var a=Find(r.First);var b=Find(r.Second);var result=Find(r.Result);
                var key=string.CompareOrdinal(r.First,r.Second)<=0?r.First+"/"+r.Second:r.Second+"/"+r.First;
                if(a==null||b==null||a.Completed||b.Completed||result==null||!result.Completed||!pairs.Add(key)||!results.Add(r.Result))
                    throw new InvalidOperationException("Invalid or duplicate item recipe.");
            }
        }
        // Always derive from a fresh character/star snapshot; never write to character definitions.
        public PrototypeCombatStats Derive(PrototypeCombatStats basis, IEnumerable<PrototypeOwnedItem> equipped)
        {
            var s=basis.CopyValidated();var amounts=new float[7];
            foreach(var item in equipped) foreach(var m in Find(item.DefinitionId).Modifiers) amounts[(int)m.Stat]+=m.Amount;
            s.AttackPower+=Mathf.RoundToInt(amounts[0]);s.SkillAmplification+=amounts[1];s.MaxHealth+=Mathf.RoundToInt(amounts[2]);s.Defense+=Mathf.RoundToInt(amounts[3]);
            s.AttackSpeed*=1+amounts[4];s.CriticalChance+=amounts[5];s.DefensePenetration+=amounts[6];return s.CopyValidated();
        }
    }
}
