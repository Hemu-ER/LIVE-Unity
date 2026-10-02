using System;

namespace LIVE.Prototype
{
    public static class PrototypeEconomy
    {
        public static int Income(int creditsBeforeReward, PrototypeRules rules) =>
            rules.RoundIncome + Math.Min(rules.InterestCap, Math.Max(0, creditsBeforeReward) / 10);

        public static int OriginalCopies(int stars) => stars == 1 ? 1 : stars == 2 ? 3 : stars == 3 ? 9 :
            throw new ArgumentOutOfRangeException(nameof(stars));

        public static int SalePrice(int cost, int stars) =>
            stars == 1 ? cost : (cost * OriginalCopies(stars) + 1) / 2;
    }

    public sealed class PrototypeMastery
    {
        public const int MaxLevel = 20;
        public int TotalExp { get; private set; }
        public int Level
        {
            get
            {
                int level = 1, remaining = TotalExp;
                while (level < MaxLevel && remaining >= RequiredForNext(level))
                    remaining -= RequiredForNext(level++);
                return level;
            }
        }
        public int CurrentExp => Level == MaxLevel ? 0 : TotalExp - CumulativeForLevel(Level);
        public int NextLevelExp => RequiredForNext(Level);
        // Intervals are interpreted as the CURRENT level: 1->2 through 6->7 cost 4, etc.
        public static int RequiredForNext(int currentLevel) =>
            currentLevel >= MaxLevel ? 0 : currentLevel <= 6 ? 4 : currentLevel <= 13 ? 5 : 6;
        public static int CumulativeForLevel(int level)
        {
            int exp = 0;
            for (int current = 1; current < Math.Min(MaxLevel, level); current++) exp += RequiredForNext(current);
            return exp;
        }
        public void AddExp(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            TotalExp = (int)Math.Min(CumulativeForLevel(MaxLevel), (long)TotalExp + amount);
        }
    }

    // One seeded stream for all random choices; Reset Run resets this stream as well.
    public sealed class PrototypeRandom
    {
        private uint state;
        public PrototypeRandom(int seed) { state = unchecked((uint)seed); if (state == 0) state = 0x9e3779b9; }
        public int Next(int exclusiveMax)
        {
            if (exclusiveMax < 1) throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            uint bound = (uint)exclusiveMax;
            uint threshold = unchecked(0u - bound) % bound;
            uint value;
            do
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                value = state;
            } while (value < threshold);
            return (int)(value % bound);
        }
    }
}
