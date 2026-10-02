using System.Collections.Generic;
using UnityEngine;

namespace LIVE.Prototype
{
    // Scene adapter only: persistent rules live in RunModel, combat continues to use the existing core.
    public sealed class PrototypeRunController : MonoBehaviour
    {
        [SerializeField] private int seed = 1729;
        private BattlefieldPrototype battlefield;
        private int displayedRevision = -1;
        private PrototypeRunPhase displayedPhase;
        public PrototypeGameData Data { get; private set; }
        public PrototypeRunModel Run { get; private set; }
        public PrototypeDataMode DataMode { get; private set; }

        public void Initialize(BattlefieldPrototype board)
        {
            battlefield = board;
            SetDataMode(PrototypeDataMode.WebRoster);
        }

        public void SetDataMode(PrototypeDataMode mode)
        {
            DataMode = mode;
            Data = mode == PrototypeDataMode.WebRoster ? PrototypeWebRoster.Load().CreateGameData() : PrototypeGameData.Load();
            Run = new PrototypeRunModel(Data, seed);
            displayedRevision = -1;
            Synchronize();
        }

        [ContextMenu("Prototype/Use U01-U09 regression fixtures")]
        private void UseFixtures() => SetDataMode(PrototypeDataMode.TestFixtures);
        [ContextMenu("Prototype/Use imported web roster")]
        private void UseWebRoster() => SetDataMode(PrototypeDataMode.WebRoster);

        private void FixedUpdate() => Step(Time.fixedDeltaTime);

        public void Step(float seconds)
        {
            if (Run == null || seconds <= 0) return;
            Synchronize();
            if (Run.Phase == PrototypeRunPhase.Combat)
            {
                // Advance only up to the remaining combat deadline.
                battlefield.Combat.Step(Mathf.Min(seconds, Run.RemainingSeconds));
                if (battlefield.Combat.State == PrototypeCombatState.Finished)
                {
                    var winner = battlefield.Combat.Winner;
                    Run.CompleteCombat(winner == null ? PrototypeRoundOutcome.Draw :
                        winner == "A" ? PrototypeRoundOutcome.Win : PrototypeRoundOutcome.Loss);
                }
                else Run.Tick(seconds);
            }
            else Run.Tick(seconds);
            Synchronize();
        }

        public void Synchronize()
        {
            if (Run == null || displayedRevision == Run.Revision) return;
            bool changedPhase = displayedRevision < 0 || displayedPhase != Run.Phase;
            if (Run.Phase == PrototypeRunPhase.Prep || (changedPhase && Run.Phase == PrototypeRunPhase.Combat))
            {
                BuildCombatInstances();
                if (Run.Phase == PrototypeRunPhase.Combat) battlefield.Combat.StartBattle();
            }
            else if (Run.Phase == PrototypeRunPhase.Result)
            {
                battlefield.Combat.FinishAsDraw(); // Stops the core on the 60-second deadline too.
                Debug.Log($"LIVE Round {Run.Round}: {Run.LastMessage}", this);
                Debug.Log(battlefield.Combat.StatisticsSummary(), this);
            }
            displayedRevision = Run.Revision;
            displayedPhase = Run.Phase;
        }

        private void BuildCombatInstances()
        {
            battlefield.ClearUnits();
            var combatants = new List<PrototypeUnit>();
            foreach (var owned in Run.Player.OwnedUnits)
            {
                if (owned.Location != PrototypeUnitLocation.Board) continue;
                combatants.Add(battlefield.CreateUnit("A", owned.Row, owned.Column,
                    Data.CombatStats(owned.DefinitionId, owned.Stars), Label(owned.DefinitionId, owned.Stars), Data.Definition(owned.DefinitionId).Abilities));
            }
            foreach (var enemy in Run.Enemies.Units)
                combatants.Add(battlefield.CreateUnit("B", enemy.Row, enemy.Column,
                    Data.CombatStats(enemy.UnitId, enemy.Stars), Label(enemy.UnitId, enemy.Stars), Data.Definition(enemy.UnitId).Abilities));
            battlefield.ConfigureCombat(combatants, unchecked(Run.Seed + Run.Round * 7919));
        }

        public string Label(string id, int stars) => Data.Definition(id).DisplayName.Replace("Test Unit ", "U") + " " + new string('*', stars);

        [ContextMenu("Prototype/Reset run")]
        public void ResetRun()
        {
            if (Run == null) return;
            Run.Reset(seed);
            displayedRevision = -1;
            Synchronize();
        }

        public bool Ready() { bool success = Run.Ready(); Synchronize(); return success; }
        public bool Buy(int slot) { bool success = Run.Buy(slot); Synchronize(); return success; }
        public bool Reroll() { bool success = Run.Reroll(); Synchronize(); return success; }
        public bool Invest() { bool success = Run.InvestMastery(); Synchronize(); return success; }
        public bool Sell(long id) { bool success = Run.Sell(id); Synchronize(); return success; }
        public bool Move(long id, PrototypeUnitLocation location, int first, int second = 0)
        { bool success = Run.Move(id, location, first, second); Synchronize(); return success; }
    }
}
