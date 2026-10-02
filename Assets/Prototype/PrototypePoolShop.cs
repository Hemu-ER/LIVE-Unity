using System;
using System.Collections.Generic;

namespace LIVE.Prototype
{
    public sealed class PrototypeSharedPool
    {
        private readonly Dictionary<string, int> available = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> capacity = new Dictionary<string, int>(StringComparer.Ordinal);
        public PrototypeSharedPool(PrototypeGameData data)
        {
            foreach (var definition in data.Units)
            {
                if (!definition.Playable) continue;
                int count = data.Rules.CopiesByCost[definition.Cost - 1];
                available.Add(definition.Id, count);
                capacity.Add(definition.Id, count);
            }
        }
        public int Available(string id) => available[id];
        public int Capacity(string id) => capacity[id];
        public bool TryTake(string id, int copies = 1)
        {
            if (copies <= 0 || !available.ContainsKey(id) || available[id] < copies) return false;
            available[id] -= copies;
            return true;
        }
        public void Return(string id, int copies)
        {
            if (copies <= 0 || !available.ContainsKey(id) || available[id] + copies > capacity[id])
                throw new InvalidOperationException("Shared pool return exceeds the reserved/owned copies.");
            available[id] += copies;
        }
    }

    public sealed class PrototypeShop
    {
        private readonly PrototypeGameData data;
        private readonly PrototypeSharedPool pool;
        private readonly PrototypeRandom random;
        private readonly string[] slots;
        private readonly List<PrototypeUnitDefinition> definitions;
        public int Count => slots.Length;
        public string Slot(int index) => index >= 0 && index < Count ? slots[index] : null;

        public PrototypeShop(PrototypeGameData data, PrototypeSharedPool pool, PrototypeRandom random)
        {
            this.data = data; this.pool = pool; this.random = random;
            slots = new string[data.Rules.ShopSize];
            definitions = new List<PrototypeUnitDefinition>(data.Units);
            definitions.RemoveAll(unit => !unit.Playable);
            definitions.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
        }

        public void ReleaseReservations()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null) pool.Return(slots[i], 1);
                slots[i] = null;
            }
        }

        public void Roll(int masteryLevel)
        {
            ReleaseReservations();
            for (int i = 0; i < slots.Length; i++) slots[i] = ReserveOne(masteryLevel);
        }

        private string ReserveOne(int masteryLevel)
        {
            int[] odds = data.Odds(masteryLevel);
            int[] costStock = new int[3];
            foreach (var unit in definitions) costStock[unit.Cost - 1] += pool.Available(unit.Id);
            int totalWeight = 0;
            for (int cost = 0; cost < 3; cost++)
            {
                if (costStock[cost] == 0) odds[cost] = 0;
                totalWeight += odds[cost];
            }
            // Exhausted tiers are removed and the remaining eligible tier odds renormalized.
            if (totalWeight == 0) return null;
            int pick = random.Next(totalWeight), chosenCost = 0;
            for (; chosenCost < 3; chosenCost++) { if (pick < odds[chosenCost]) break; pick -= odds[chosenCost]; }
            int stockPick = random.Next(costStock[chosenCost]);
            foreach (var unit in definitions)
            {
                if (unit.Cost != chosenCost + 1) continue;
                int stock = pool.Available(unit.Id);
                if (stockPick < stock)
                {
                    if (!pool.TryTake(unit.Id)) throw new InvalidOperationException("Failed shop reservation.");
                    return unit.Id;
                }
                stockPick -= stock;
            }
            throw new InvalidOperationException("Shared pool selection failed.");
        }

        internal string Consume(int index)
        {
            string id = Slot(index);
            if (id != null) slots[index] = null; // Already removed from pool when reserved.
            return id;
        }
    }
}
