using System;
using System.Collections.Generic;
using UnityEngine;

namespace LIVE.Prototype
{
    public enum PrototypeCombatState { Ready, Fighting, Finished }

    public sealed class PrototypeCombatController : MonoBehaviour
    {
        [SerializeField, Min(0)] private float readyDelay = 1f;
        private readonly List<PrototypeUnit> units = new List<PrototypeUnit>();
        private Func<int, int, Vector3> position;
        private float readyElapsed;
        private int randomSeed;
        public PrototypeRandom Random { get; private set; }
        public event Action<PrototypeCombatEvent> EventRaised;
        public PrototypeCombatState State { get; private set; } = PrototypeCombatState.Ready;
        public string Winner { get; private set; }
        public PrototypeCombatGrid Grid { get; private set; }
        public IReadOnlyList<PrototypeUnit> Units => units.AsReadOnly();

        public void Initialize(int rows, int columns, IEnumerable<PrototypeUnit> combatants, Func<int, int, Vector3> cellPosition, int seed = 1729)
        {
            randomSeed = seed;
            position = cellPosition ?? throw new ArgumentNullException(nameof(cellPosition));
            Grid = new PrototypeCombatGrid(rows, columns);
            units.Clear();
            foreach (var unit in combatants)
            {
                if (unit == null || units.Contains(unit)) throw new ArgumentException("Combatants must be unique and non-null.");
                unit.Bind(this, units.Count);
                units.Add(unit);
            }
            ResetBattle();
        }

        public Vector3 WorldPosition(Vector2Int cell) => position(cell.x, cell.y);
        private void FixedUpdate() => Step(Time.fixedDeltaTime);

        public void FinishAsDraw()
        {
            if (Grid == null || State == PrototypeCombatState.Finished) return;
            bool wasFighting = State == PrototypeCombatState.Fighting;
            Winner = null;
            State = PrototypeCombatState.Finished;
            foreach (var unit in units) if (unit != null) unit.StopCombat();
            if (wasFighting) Publish(new PrototypeCombatEvent(PrototypeCombatEventType.CombatFinished));
        }

        public void Step(float seconds)
        {
            if (Grid == null || State == PrototypeCombatState.Finished || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            if (State == PrototypeCombatState.Ready)
            {
                readyElapsed += seconds;
                if (readyElapsed >= readyDelay) StartBattle();
                return;
            }
            CheckOutcome();
            // Stable registration order resolves simultaneous movement/attack conflicts.
            foreach (var unit in units)
            {
                if (State != PrototypeCombatState.Fighting) break;
                if (unit != null && unit.IsAlive) unit.Tick(seconds);
            }
        }

        [ContextMenu("Prototype/Start battle")]
        public void StartBattle()
        {
            if (Grid == null || State != PrototypeCombatState.Ready) return;
            State = PrototypeCombatState.Fighting;
            foreach (var unit in units) if (unit != null && unit.IsAlive) unit.MarkCombatStarted();
            CheckOutcome();
        }

        [ContextMenu("Prototype/Reset battle")]
        public void ResetBattle()
        {
            if (Grid == null) return;
            State = PrototypeCombatState.Ready;
            Winner = null;
            readyElapsed = 0;
            Random = new PrototypeRandom(randomSeed);
            Grid.Clear();
            foreach (var unit in units)
            {
                if (unit == null) continue;
                unit.ResetForBattle();
                Grid.Place(unit, unit.Cell);
                Publish(new PrototypeCombatEvent(PrototypeCombatEventType.UnitSpawned, unit));
            }
        }

        public PrototypeUnit FindNearestEnemy(PrototypeUnit seeker)
        {
            PrototypeUnit best = null;
            foreach (var candidate in units)
            {
                if (candidate == null || !candidate.IsAlive || candidate.Faction == seeker.Faction) continue;
                if (best == null || CompareTargets(seeker, candidate, best) < 0) best = candidate;
            }
            return best;
        }

        private static int CompareTargets(PrototypeUnit seeker, PrototypeUnit a, PrototypeUnit b)
        {
            int comparison = PrototypeCombatGrid.Distance(seeker.Cell, a.Cell).CompareTo(PrototypeCombatGrid.Distance(seeker.Cell, b.Cell));
            if (comparison != 0) return comparison;
            comparison = a.Row.CompareTo(b.Row);
            if (comparison != 0) return comparison;
            comparison = a.Column.CompareTo(b.Column);
            return comparison != 0 ? comparison : a.SpawnOrder.CompareTo(b.SpawnOrder);
        }

        internal void NotifyDeath(PrototypeUnit dead)
        {
            Grid.Release(dead);
            Publish(new PrototypeCombatEvent(PrototypeCombatEventType.UnitDied, dead));
            foreach (var unit in units)
                if (unit != null && unit.IsAlive && unit.Target == dead) unit.Retarget();
            if (State == PrototypeCombatState.Fighting) CheckOutcome();
        }

        private void CheckOutcome()
        {
            string survivor = null;
            foreach (var unit in units)
            {
                if (unit == null || !unit.IsAlive) continue;
                if (survivor != null && survivor != unit.Faction) return;
                survivor = unit.Faction;
            }
            Winner = survivor;
            State = PrototypeCombatState.Finished;
            foreach (var unit in units) if (unit != null) unit.StopCombat();
            Publish(new PrototypeCombatEvent(PrototypeCombatEventType.CombatFinished));
            Debug.Log(Winner == null ? "LIVE: Draw (no survivors)." : $"LIVE: {Winner} wins.", this);
        }

        public PrototypeUnit LowestHealthAlly(PrototypeUnit caster)
        {
            PrototypeUnit best = null;
            foreach (var unit in units)
            {
                if (unit == null || !unit.IsAlive || unit.Faction != caster.Faction) continue;
                if (best == null || (long)unit.CurrentHealth * best.MaxHealth < (long)best.CurrentHealth * unit.MaxHealth ||
                    ((long)unit.CurrentHealth * best.MaxHealth == (long)best.CurrentHealth * unit.MaxHealth && unit.SpawnOrder < best.SpawnOrder)) best = unit;
            }
            return best;
        }

        internal void Publish(PrototypeCombatEvent message)
        {
            // Observers cannot cancel rules; a broken view must not interrupt simulation.
            if (EventRaised == null) return;
            foreach (Action<PrototypeCombatEvent> observer in EventRaised.GetInvocationList())
                try { observer(message); } catch (Exception exception) { Debug.LogException(exception); }
        }

        public string StatisticsSummary()
        {
            var text = new System.Text.StringBuilder("LIVE combat statistics\n");
            foreach (var unit in units)
                if (unit != null) text.AppendLine($"#{unit.SpawnOrder} {unit.Faction} {unit.DisplayLabel}: {unit.Statistics}");
            return text.ToString();
        }
    }
}
