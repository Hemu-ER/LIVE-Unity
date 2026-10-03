using System;

namespace LIVE.Prototype
{
    public static class PrototypeDamageCalculator
    {
        public const double CriticalMultiplier = 2.0;
        // Shared defense/rounding pipeline for basics and skill coefficients. No skill crits by default.
        public static int Calculate(int attackPower, int defense) => Mitigate(attackPower, defense, 0, false);
        public static int Mitigate(double raw, double defense, double penetration, bool critical = false)
        {
            double effectiveDefense = Math.Max(0, defense - Math.Max(0, penetration));
            double damage = Math.Max(0, raw) * 100.0 / (100.0 + effectiveDefense);
            if (critical) damage *= CriticalMultiplier;
            return RoundAmount(damage, 1);
        }
        public static double Coefficients(PrototypeSkillEffect effect, PrototypeCombatStats stats) =>
            Math.Max(0, effect.BaseDamage + stats.AttackPower * (double)effect.AttackRatio + stats.SkillAmplification * (double)effect.SkillRatio);
        public static double Coefficients(PrototypeSkillEffect effect, double attack, double amplification, int sourceMaxHealth, int targetMaxHealth, int targetHealth) =>
            Math.Max(0, effect.BaseDamage + attack * effect.AttackRatio + amplification * effect.SkillRatio +
                sourceMaxHealth * (double)effect.SourceMaxHealthRatio + targetMaxHealth * (double)effect.TargetMaxHealthRatio + targetHealth * (double)effect.TargetCurrentHealthRatio);
        public static int RoundAmount(double amount, int minimum = 0) =>
            double.IsNaN(amount) ? minimum : (int)Math.Max(minimum, Math.Min(int.MaxValue, Math.Floor(amount + 0.5)));
        public static bool RollCritical(float chance, PrototypeRandom random) =>
            chance >= 1 || (chance > 0 && random.Next(1000000) < chance * 1000000.0);
    }
}
