using UnityEngine;

namespace LIVE.Prototype
{
    public sealed partial class PrototypeUnit : MonoBehaviour
    {
        [SerializeField] private PrototypeCombatStats stats = new PrototypeCombatStats();
        [SerializeField, Min(0)] private int currentHealth;
        private PrototypeCombatController combat;
        private Transform healthFill;
        private Transform character;
        private SpriteRenderer[] renderers;
        private Vector3 fullHealthScale, fullHealthPosition, moveStart;
        private Vector2Int spawnCell, destination;
        private float moveProgress, attackCooldown, attackFlash;
        public PrototypeCombatStats Stats => stats;
        public string Faction { get; private set; }
        public Vector2Int Cell { get; private set; }
        public int Row => Cell.x;
        public int Column => Cell.y;
        public int SpawnOrder { get; private set; }
        public int MaxHealth => stats.MaxHealth;
        public int CurrentHealth => currentHealth;
        public bool IsAlive { get; private set; }
        public bool IsMoving { get; private set; }
        public PrototypeUnit Target { get; private set; }
        public string DisplayLabel { get; set; }

        public void Initialize(string faction, int row, int column, PrototypeCombatStats combatStats, Transform fill, Transform visual)
        {
            Faction = faction;
            stats = combatStats.CopyValidated();
            Cell = spawnCell = new Vector2Int(row, column);
            healthFill = fill;
            character = visual;
            if (fill != null)
            {
                fullHealthScale = fill.localScale;
                fullHealthPosition = fill.localPosition;
            }
            renderers = GetComponentsInChildren<SpriteRenderer>();
        }

        internal void Bind(PrototypeCombatController controller, int order)
        {
            combat = controller;
            SpawnOrder = order;
            GetComponent<PrototypeUnitView>()?.Bind(this, controller);
        }

        internal void ResetForBattle()
        {
            stats = stats.CopyValidated();
            ResetAbilities();
            Cell = spawnCell;
            IsAlive = true;
            currentHealth = MaxHealth;
            IsMoving = false;
            moveProgress = attackCooldown = attackFlash = 0;
            Target = null;
            transform.position = combat.WorldPosition(Cell);
            if (character != null) character.localPosition = Vector3.zero;
            foreach (var renderer in renderers) if (renderer != null) renderer.enabled = true;
            RefreshHealth();
        }

        internal void Retarget() => Target = combat.FindNearestEnemy(this);

        internal void Tick(float seconds)
        {
            if (!IsAlive) return;
            if (TickAbilities(seconds)) return;
            var effective = EffectiveStats;
            attackCooldown = Mathf.Max(0, attackCooldown - seconds);
            attackFlash = Mathf.Max(0, attackFlash - seconds);
            if (character != null && attackFlash <= 0) character.localPosition = Vector3.zero;
            if (IsMoving)
            {
                moveProgress = Mathf.Min(1, moveProgress + seconds * effective.MoveSpeed);
                transform.position = Vector3.Lerp(moveStart, combat.WorldPosition(destination), moveProgress);
                if (moveProgress >= 1)
                {
                    combat.Grid.CompleteStep(this, destination);
                    var from = Cell;
                    Cell = destination;
                    ActionState = PrototypeActionState.Idle;
                    combat.Publish(new PrototypeCombatEvent(PrototypeCombatEventType.UnitMoved, this, from: from, to: Cell));
                    IsMoving = false;
                }
                return; // No attack on the movement completion tick either.
            }
            Retarget();
            if (Target == null) return;
            if (PrototypeCombatGrid.Distance(Cell, Target.Cell) <= effective.AttackRange)
            {
                if (attackCooldown > 0) return;
                attackCooldown = 1f / effective.AttackSpeed;
                attackFlash = 0.12f;
                if (character != null)
                    character.localPosition = (combat.WorldPosition(Target.Cell) - transform.position).normalized * 0.12f;
                PerformBasicAttack();
            }
            else if (combat.Grid.TryFindStep(this, Target, out var next) && combat.Grid.TryReserveStep(this, next))
            {
                destination = next;
                moveStart = transform.position;
                moveProgress = 0;
                IsMoving = true;
                ActionState = PrototypeActionState.Moving;
            }
        }

        public void TakeDamage(int damage)
        {
            if (!IsAlive || damage <= 0) return;
            ReceiveDamage(damage, null, false);
        }

        public void SetHealth(int health)
        {
            if (!IsAlive) return; // Resurrection requires ResetBattle to restore occupancy safely.
            currentHealth = Mathf.Clamp(health, 0, MaxHealth);
            RefreshHealth();
            if (currentHealth > 0) return;
            IsAlive = false;
            CancelAbilities(true);
            IsMoving = false;
            Target = null;
            foreach (var renderer in renderers) if (renderer != null) renderer.enabled = false;
            if (combat != null) combat.NotifyDeath(this);
        }

        internal void StopCombat()
        {
            if (IsMoving)
            {
                combat.Grid.Release(this);
                if (IsAlive) combat.Grid.Place(this, Cell);
                transform.position = combat.WorldPosition(Cell);
            }
            IsMoving = false;
            Target = null;
            attackFlash = 0;
            CancelAbilities(false);
            if (character != null) character.localPosition = Vector3.zero;
        }

        [ContextMenu("Prototype/Take 25 damage")]
        private void TakeTestDamage() => TakeDamage(25);

        [ContextMenu("Prototype/Restore health")]
        private void RestoreHealth() => SetHealth(MaxHealth);

        private void OnValidate()
        {
            stats = (stats ?? new PrototypeCombatStats()).CopyValidated();
            if (Application.isPlaying && combat != null && IsAlive) SetHealth(currentHealth);
        }

        private void RefreshHealth()
        {
            if (healthFill == null) return;
            float ratio = (float)currentHealth / MaxHealth;
            healthFill.localScale = new Vector3(fullHealthScale.x * ratio, fullHealthScale.y, fullHealthScale.z);
            healthFill.localPosition = fullHealthPosition + Vector3.right * ((ratio - 1) * fullHealthScale.x * 0.5f);
        }
    }
}
