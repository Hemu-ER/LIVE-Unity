using System;
using System.Collections.Generic;

namespace LIVE.Prototype
{
    public enum PrototypeUnitLocation { Bench, Board }

    public sealed class PrototypeOwnedUnit
    {
        public long InstanceId { get; }
        public string DefinitionId { get; }
        public int Stars { get; internal set; } = 1;
        public PrototypeUnitLocation Location { get; internal set; }
        public int BenchSlot { get; internal set; }
        public int Row { get; internal set; }
        public int Column { get; internal set; }
        internal PrototypeOwnedUnit(long instanceId, string definitionId)
        { InstanceId = instanceId; DefinitionId = definitionId; }
    }

    public sealed class PrototypePlayerState
    {
        private readonly List<PrototypeOwnedUnit> owned = new List<PrototypeOwnedUnit>();
        private readonly int benchSize;
        private long nextId = 1;
        public int Credits { get; internal set; }
        public PrototypeMastery Mastery { get; } = new PrototypeMastery();
        public IReadOnlyList<PrototypeOwnedUnit> OwnedUnits => owned.AsReadOnly();
        public int BenchSize => benchSize;
        public PrototypePlayerState(int startingCredits, int benchSize)
        { Credits = startingCredits; this.benchSize = benchSize; }

        public PrototypeOwnedUnit Find(long id) => owned.Find(unit => unit.InstanceId == id);
        public PrototypeOwnedUnit AtBench(int slot) => owned.Find(unit => unit.Location == PrototypeUnitLocation.Bench && unit.BenchSlot == slot);
        public PrototypeOwnedUnit AtBoard(int row, int column) => owned.Find(unit =>
            unit.Location == PrototypeUnitLocation.Board && unit.Row == row && unit.Column == column);
        public int EmptyBench()
        { for (int slot = 0; slot < benchSize; slot++) if (AtBench(slot) == null) return slot; return -1; }

        internal PrototypeOwnedUnit AddToBench(string id, int slot)
        {
            if (slot < 0 || slot >= benchSize || AtBench(slot) != null) throw new InvalidOperationException("No bench space.");
            var unit = new PrototypeOwnedUnit(nextId++, id) { Location = PrototypeUnitLocation.Bench, BenchSlot = slot };
            owned.Add(unit);
            return unit;
        }

        internal bool Move(long id, PrototypeUnitLocation location, int first, int second = 0)
        {
            var unit = Find(id);
            if (unit == null) return false;
            if (location == PrototypeUnitLocation.Board)
            {
                if (first < 0 || first >= 3 || second < 0 || second >= 3 || AtBoard(first, second) != null) return false;
                unit.Location = location; unit.Row = first; unit.Column = second; unit.BenchSlot = -1;
            }
            else
            {
                if (first < 0 || first >= benchSize || AtBench(first) != null) return false;
                unit.Location = location; unit.BenchSlot = first; unit.Row = unit.Column = -1;
            }
            return true;
        }

        internal void Remove(PrototypeOwnedUnit unit) => owned.Remove(unit);

        internal void MergeAll(Action<PrototypeOwnedUnit> beforeRemove = null)
        {
            // Re-scan after each merge for cascades. Prefer preserving a deployed unit, then oldest ID.
            bool merged;
            do
            {
                merged = false;
                var sorted = new List<PrototypeOwnedUnit>(owned);
                sorted.Sort((a, b) => a.InstanceId.CompareTo(b.InstanceId));
                foreach (var seed in sorted)
                {
                    if (seed.Stars >= 3) continue;
                    var group = owned.FindAll(unit => unit.DefinitionId == seed.DefinitionId && unit.Stars == seed.Stars);
                    if (group.Count < 3) continue;
                    group.Sort((a, b) =>
                    {
                        int location = (a.Location == PrototypeUnitLocation.Board ? 0 : 1).CompareTo(b.Location == PrototypeUnitLocation.Board ? 0 : 1);
                        return location != 0 ? location : a.InstanceId.CompareTo(b.InstanceId);
                    });
                    group[0].Stars++;
                    beforeRemove?.Invoke(group[1]); beforeRemove?.Invoke(group[2]);
                    owned.Remove(group[1]); owned.Remove(group[2]);
                    merged = true;
                    break;
                }
            } while (merged);
        }
    }
}
