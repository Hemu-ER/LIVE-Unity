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
        private readonly List<PrototypeUnit> actionOrder = new List<PrototypeUnit>();
        private float quietSeconds;
        public int StallCount { get; private set; }
        public string LastStallDiagnostic { get; private set; }
        public float StallThresholdSeconds { get; set; } = 5f;
        private int randomSeed;
        public PrototypeRandom Random { get; private set; }
        public double ElapsedSeconds { get; private set; }
        public readonly Dictionary<string, IPrototypeSkillHandler> SkillHandlers = new Dictionary<string, IPrototypeSkillHandler>(StringComparer.Ordinal);
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
            actionOrder.Clear();
            actionOrder.AddRange(units);
            actionOrder.Sort((a, b) => StringComparer.Ordinal.Compare(a.CombatId, b.CombatId));
            for (int i = 0; i < actionOrder.Count; i++) actionOrder[i].Bind(this, i);
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
            // Battle-stable identity order, independent of registration/MonoBehaviour update order.
            ElapsedSeconds += seconds;
            quietSeconds += seconds;
            foreach (var unit in actionOrder)
            {
                if (State != PrototypeCombatState.Fighting) break;
                if (unit != null && unit.IsAlive) unit.Tick(seconds);
            }
            if (State == PrototypeCombatState.Fighting && quietSeconds >= Mathf.Max(0.1f, StallThresholdSeconds))
            {
                StallCount++;
                var diagnostic = new System.Text.StringBuilder($"LIVE combat stall: {quietSeconds:F2}s without meaningful events; seed={randomSeed}");
                foreach (var unit in actionOrder)
                    if (unit != null && unit.IsAlive) diagnostic.Append($" | {unit.CombatId} cell={unit.Cell} state={unit.ActionState} target={unit.Target?.CombatId} HP={unit.CurrentHealth} shield={unit.Shield}");
                LastStallDiagnostic = diagnostic.ToString();
                Debug.LogWarning(LastStallDiagnostic, this);
                quietSeconds = 0;
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
            ElapsedSeconds = 0;
            quietSeconds = 0; StallCount = 0; LastStallDiagnostic = null;
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

        public bool IsValidEnemy(PrototypeUnit seeker, PrototypeUnit candidate) =>
            candidate != null && candidate.IsAlive && candidate.Faction != seeker.Faction && units.Contains(candidate);

        public PrototypeUnit FindNearestEnemy(PrototypeUnit seeker)
        {
            PrototypeUnit best = null;
            foreach (var candidate in units)
            {
                if (!IsValidEnemy(seeker, candidate)) continue;
                if (best == null || CompareTargets(seeker, candidate, best) < 0) best = candidate;
            }
            return best;
        }

        private static int CompareTargets(PrototypeUnit seeker, PrototypeUnit a, PrototypeUnit b)
        {
            int comparison = PrototypeCombatGrid.Distance(seeker.Cell, a.Cell).CompareTo(PrototypeCombatGrid.Distance(seeker.Cell, b.Cell));
            if (comparison != 0) return comparison;
            comparison = a.CurrentHealth.CompareTo(b.CurrentHealth);
            return comparison != 0 ? comparison : StringComparer.Ordinal.Compare(a.CombatId, b.CombatId);
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
            if (message.Type == PrototypeCombatEventType.UnitMoved || message.Type == PrototypeCombatEventType.SkillCast ||
                message.Type == PrototypeCombatEventType.UnitDied ||
                ((message.Type == PrototypeCombatEventType.DamageDealt || message.Type == PrototypeCombatEventType.HealApplied ||
                  message.Type == PrototypeCombatEventType.ShieldApplied) && message.Amount > 0)) quietSeconds = 0;
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
