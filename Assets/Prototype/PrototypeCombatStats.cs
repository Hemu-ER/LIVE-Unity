using System;
using UnityEngine;

namespace LIVE.Prototype
{
    [Serializable]
    public sealed class PrototypeCombatStats
    {
        [Min(1)] public int MaxHealth = 100;
        [Min(0)] public int AttackPower = 20;
        [Min(0)] public int Defense = 5;
        [Min(0.01f)] public float AttackSpeed = 1f;
        [Min(1)] public int AttackRange = 1;
        [Min(0.01f)] public float MoveSpeed = 2f; // Cells per second.

        public PrototypeCombatStats CopyValidated() => new PrototypeCombatStats
        {
            MaxHealth = Mathf.Max(1, MaxHealth), AttackPower = Mathf.Max(0, AttackPower),
            Defense = Mathf.Max(0, Defense), AttackSpeed = SafeSpeed(AttackSpeed),
            AttackRange = Mathf.Max(1, AttackRange), MoveSpeed = SafeSpeed(MoveSpeed)
        };

        private static float SafeSpeed(float value) =>
            float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp(value, 0.01f, 100f);
    }
}
