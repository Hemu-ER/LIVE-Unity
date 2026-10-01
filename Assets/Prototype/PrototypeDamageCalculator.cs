using System;

namespace LIVE.Prototype
{
    public static class PrototypeDamageCalculator
    {
        // Round to nearest integer, .5 rounds up. Double avoids integer overflow.
        public static int Calculate(int attackPower, int defense)
        {
            double damage = Math.Max(0, attackPower) * 100.0 / (100.0 + Math.Max(0, defense));
            return (int)Math.Max(1, Math.Min(int.MaxValue, Math.Floor(damage + 0.5)));
        }
    }
}
