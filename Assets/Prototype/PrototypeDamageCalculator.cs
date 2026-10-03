using System;
using System.Globalization;

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
            Math.Max(0, effect.BaseDamage + stats.AttackPower * Ratio(effect, effect.AttackRatio) + stats.SkillAmplification * Ratio(effect, effect.SkillRatio));
        public static double Coefficients(PrototypeSkillEffect effect, double attack, double amplification, int sourceMaxHealth, int targetMaxHealth, int targetHealth) =>
            Math.Max(0, effect.BaseDamage + attack * Ratio(effect, effect.AttackRatio) + amplification * Ratio(effect, effect.SkillRatio) +
                sourceMaxHealth * Ratio(effect, effect.SourceMaxHealthRatio) + targetMaxHealth * Ratio(effect, effect.TargetMaxHealthRatio) + targetHealth * Ratio(effect, effect.TargetCurrentHealthRatio));
        private static double Ratio(PrototypeSkillEffect effect, float value) =>
            effect.PreserveAuthoredPrecision ? AuthoredRatio(value) : (double)value;
        // Opt-in keeps already-shipped definitions' rounding unchanged.
        // Keep JsonUtility's float-compatible schema, but use its shortest round-trip decimal
        // when promoting authored ratios to double (0.45f must not turn 22.5 into 22.4999994).
        public static double AuthoredRatio(float value) =>
            double.Parse(value.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        public static int RoundAmount(double amount, int minimum = 0) =>
            double.IsNaN(amount) ? minimum : (int)Math.Max(minimum, Math.Min(int.MaxValue, Math.Floor(amount + 0.5)));
        public static bool RollCritical(float chance, PrototypeRandom random) =>
            chance >= 1 || (chance > 0 && random.Next(1000000) < chance * 1000000.0);
    }
}
