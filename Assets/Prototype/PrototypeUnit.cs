using UnityEngine;

namespace LIVE.Prototype
{
    public sealed class PrototypeUnit : MonoBehaviour
    {
        [SerializeField, Min(1)] private int maxHealth = 100;
        [SerializeField, Min(0)] private int currentHealth = 100;
        private Transform healthFill;
        private Vector3 fullHealthScale;
        private Vector3 fullHealthPosition;
        public string Faction { get; private set; }
        public int MaxHealth => maxHealth;
        public int CurrentHealth => currentHealth;

        public void Initialize(string faction, Transform fill)
        {
            Faction = faction;
            healthFill = fill;
            fullHealthScale = fill.localScale;
            fullHealthPosition = fill.localPosition;
            RefreshHealth();
        }

        public void SetHealth(int health)
        {
            currentHealth = Mathf.Clamp(health, 0, maxHealth);
            RefreshHealth();
        }

        [ContextMenu("Prototype/Take 25 damage")]
        private void TakeTestDamage() => SetHealth(currentHealth - 25);

        [ContextMenu("Prototype/Restore health")]
        private void RestoreHealth() => SetHealth(maxHealth);

        private void OnValidate()
        {
            maxHealth = Mathf.Max(1, maxHealth);
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
            RefreshHealth();
        }

        private void RefreshHealth()
        {
            if (healthFill == null) return;
            float ratio = (float)currentHealth / maxHealth;
            healthFill.localScale = new Vector3(fullHealthScale.x * ratio, fullHealthScale.y, fullHealthScale.z);
            healthFill.localPosition = fullHealthPosition + Vector3.right * ((ratio - 1) * fullHealthScale.x * 0.5f);
        }
    }
}
