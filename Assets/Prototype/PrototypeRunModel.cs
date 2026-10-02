using System;
using System.Collections.Generic;

namespace LIVE.Prototype
{
    public enum PrototypeRunPhase { Setup, Prep, Combat, Result, Transition }
    public enum PrototypeRoundOutcome { Win, Loss, Draw }

    public sealed class PrototypeRoundRecord
    {
        public int Round { get; }
        public PrototypeRoundOutcome Outcome { get; }
        public int CreditsAwarded { get; }
        public PrototypeRoundRecord(int round, PrototypeRoundOutcome outcome, int credits)
        { Round = round; Outcome = outcome; CreditsAwarded = credits; }
    }

    // Command boundary: UI requests actions here; only this model mutates persistent game state.
    public sealed class PrototypeRunModel
    {
        private readonly PrototypeGameData data;
        private readonly List<PrototypeRoundRecord> history = new List<PrototypeRoundRecord>();
        private PrototypeRandom random;
        public int Seed { get; private set; }
        public int Round { get; private set; }
        public PrototypeRunPhase Phase { get; private set; }
        public float RemainingSeconds { get; private set; }
        public int Revision { get; private set; }
        public PrototypePlayerState Player { get; private set; }
        public PrototypeSharedPool Pool { get; private set; }
        public PrototypeShop Shop { get; private set; }
        public string LastMessage { get; private set; }
        public IReadOnlyList<PrototypeRoundRecord> History => history.AsReadOnly();
        public PrototypeEnemyRound Enemies => data.EnemyTeam(Round);

        public PrototypeRunModel(PrototypeGameData data, int seed)
        { this.data = data; Reset(seed); }

        public void Reset(int seed)
        {
            Phase = PrototypeRunPhase.Setup;
            Seed = seed; random = new PrototypeRandom(seed);
            Round = 1; history.Clear();
            Pool = new PrototypeSharedPool(data);
            Shop = new PrototypeShop(data, Pool, random);
            Player = new PrototypePlayerState(data.Rules.StartingCredits, data.Rules.BenchSize);
            var starters = new List<PrototypeUnitDefinition>();
            foreach (var unit in data.Units) if (unit.Playable && unit.Cost == 1 && Pool.Available(unit.Id) > 0) starters.Add(unit);
            starters.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            if (starters.Count == 0) throw new InvalidOperationException("No one-cost starter available.");
            string id = starters[random.Next(starters.Count)].Id;
            Pool.TryTake(id);
            var starter = Player.AddToBench(id, Player.EmptyBench());
            // Setup is empty; center is guaranteed free. Retain bench fallback if setup is extended later.
            Player.Move(starter.InstanceId, PrototypeUnitLocation.Board, 1, 1);
            EnterPrep();
        }

        private void EnterPrep()
        {
            Phase = PrototypeRunPhase.Prep;
            RemainingSeconds = data.Rules.PrepSeconds;
            Shop.Roll(Player.Mastery.Level); // One free refresh per Prep; old reservations returned.
            LastMessage = "Select a unit, then an empty bench slot or blue board cell.";
            Revision++;
        }

        public void Tick(float seconds)
        {
            if (seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            RemainingSeconds = Math.Max(0, RemainingSeconds - seconds);
            if (RemainingSeconds > 0) return;
            switch (Phase)
            {
                case PrototypeRunPhase.Prep: Ready(); break;
                case PrototypeRunPhase.Combat: CompleteCombat(PrototypeRoundOutcome.Draw); break;
                case PrototypeRunPhase.Result:
                    Phase = PrototypeRunPhase.Transition; RemainingSeconds = data.Rules.TransitionSeconds; Revision++; break;
                case PrototypeRunPhase.Transition: Round++; EnterPrep(); break;
            }
        }

        public bool Ready()
        {
            if (!CanPrepare()) return false;
            Phase = PrototypeRunPhase.Combat;
            RemainingSeconds = data.Rules.CombatSeconds;
            LastMessage = "Combat in progress.";
            Revision++;
            return true;
        }

        public bool CompleteCombat(PrototypeRoundOutcome outcome)
        {
            if (Phase != PrototypeRunPhase.Combat) return false;
            int income = PrototypeEconomy.Income(Player.Credits, data.Rules);
            Player.Credits += income;
            Player.Mastery.AddExp(data.Rules.NaturalMasteryExp);
            history.Add(new PrototypeRoundRecord(Round, outcome, income));
            Phase = PrototypeRunPhase.Result;
            RemainingSeconds = data.Rules.ResultSeconds;
            LastMessage = $"{outcome} | +{income} Credits | +{data.Rules.NaturalMasteryExp} Mastery EXP";
            Revision++;
            return true;
        }

        private bool CanPrepare()
        {
            if (Phase == PrototypeRunPhase.Prep) return true;
            LastMessage = "This action is only available during Prep.";
            return false;
        }

        public bool Buy(int slot)
        {
            if (!CanPrepare()) return false;
            string id = Shop.Slot(slot);
            if (id == null) { LastMessage = "Empty shop slot."; return false; }
            int bench = Player.EmptyBench(), cost = data.Definition(id).Cost;
            if (bench < 0) { LastMessage = "Bench is full (8/8)."; return false; }
            if (Player.Credits < cost) { LastMessage = "Not enough Credits."; return false; }
            Shop.Consume(slot);
            Player.Credits -= cost;
            Player.AddToBench(id, bench);
            Player.MergeAll();
            LastMessage = "Purchased " + data.Definition(id).DisplayName;
            Revision++;
            return true;
        }

        public bool Reroll()
        {
            if (!CanPrepare()) return false;
            if (Player.Credits < data.Rules.RerollCost) { LastMessage = "Not enough Credits."; return false; }
            Player.Credits -= data.Rules.RerollCost;
            Shop.Roll(Player.Mastery.Level);
            LastMessage = "Shop refreshed.";
            Revision++;
            return true;
        }

        public bool InvestMastery()
        {
            if (!CanPrepare()) return false;
            if (Player.Mastery.Level == PrototypeMastery.MaxLevel) { LastMessage = "Mastery is already at maximum."; return false; }
            if (Player.Credits < data.Rules.InvestCost) { LastMessage = "Not enough Credits."; return false; }
            Player.Credits -= data.Rules.InvestCost;
            Player.Mastery.AddExp(data.Rules.InvestExp);
            LastMessage = "Mastery investment completed.";
            Revision++;
            return true;
        }

        public bool Move(long id, PrototypeUnitLocation location, int first, int second = 0)
        {
            if (!CanPrepare()) return false;
            bool success = Player.Move(id, location, first, second);
            LastMessage = success ? "Unit moved." : "Choose an empty bench slot or a cell in your blue 3x3.";
            if (success) Revision++;
            return success;
        }

        public bool Sell(long id)
        {
            if (!CanPrepare()) return false;
            var unit = Player.Find(id);
            if (unit == null) { LastMessage = "Select an owned unit."; return false; }
            int price = PrototypeEconomy.SalePrice(data.Definition(unit.DefinitionId).Cost, unit.Stars);
            Pool.Return(unit.DefinitionId, PrototypeEconomy.OriginalCopies(unit.Stars));
            Player.Remove(unit);
            Player.Credits += price;
            LastMessage = $"Sold unit for {price} Credits.";
            Revision++;
            return true;
        }
    }
}
