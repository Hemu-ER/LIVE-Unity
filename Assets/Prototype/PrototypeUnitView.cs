using UnityEngine;

namespace LIVE.Prototype
{
    // Presentation observes completed events. Rules never await flashes or movement easing.
    public sealed class PrototypeUnitView : MonoBehaviour
    {
        private PrototypeUnit unit;
        private PrototypeCombatController combat;
        private Transform character, shieldBar, gaugeBar;
        private SpriteRenderer[] body;
        private Color[] colors;
        private float flash;
        private Color flashColor;
        private Vector3 dashOffset;
        public void Initialize(Transform visual, Transform shield, Transform gauge)
        {
            character = visual; shieldBar = shield; gaugeBar = gauge;
            body = visual.GetComponentsInChildren<SpriteRenderer>();
            colors = new Color[body.Length];
            for (int i = 0; i < body.Length; i++) colors[i] = body[i].color;
        }
        public void Bind(PrototypeUnit value, PrototypeCombatController controller)
        {
            if (combat != null) combat.EventRaised -= Observe;
            unit = value; combat = controller; combat.EventRaised += Observe;
        }
        private void Observe(PrototypeCombatEvent message)
        {
            if (message.Type == PrototypeCombatEventType.UnitSpawned && message.Source == unit)
            { flash = 0; dashOffset = Vector3.zero; }
            if (message.Type == PrototypeCombatEventType.SkillCast && message.Source == unit)
            { flash = 0.22f; flashColor = Color.white; }
            if (message.Type == PrototypeCombatEventType.HealApplied && message.Target == unit && message.Amount > 0)
            { flash = 0.2f; flashColor = Color.green; }
            if (message.Type == PrototypeCombatEventType.CriticalHit && message.Target == unit)
            { flash = 0.13f; flashColor = new Color(1, 0.65f, 0.5f); }
            if (message.Type == PrototypeCombatEventType.UnitMoved && message.Source == unit && message.SkillId != null)
                dashOffset += combat.WorldPosition(message.From) - combat.WorldPosition(message.To);
        }
        private void LateUpdate()
        {
            if (unit == null || character == null) return;
            flash = Mathf.Max(0, flash - Time.deltaTime);
            character.localScale = Vector3.one * (1 + 0.15f * Mathf.Clamp01(flash / 0.22f));
            for (int i = 0; i < body.Length; i++) body[i].color = Color.Lerp(colors[i], flashColor, Mathf.Clamp01(flash / 0.22f));
            if (dashOffset.sqrMagnitude > 0.00001f)
            {
                dashOffset = Vector3.MoveTowards(dashOffset, Vector3.zero, Time.deltaTime * 24);
                character.localPosition = transform.InverseTransformVector(dashOffset);
            }
            SetBar(shieldBar, unit.IsAlive ? Mathf.Min(1, (float)unit.Shield / unit.MaxHealth) : 0);
            SetBar(gaugeBar, unit.IsAlive ? unit.SkillGauge / unit.MaxSkillGauge : 0);
        }
        private static void SetBar(Transform bar, float ratio)
        {
            if (bar == null) return;
            bar.localScale = new Vector3(ratio, bar.localScale.y, 1);
            bar.localPosition = new Vector3((ratio - 1) * 0.5f, bar.localPosition.y, bar.localPosition.z);
        }
        private void OnDestroy() { if (combat != null) combat.EventRaised -= Observe; }
    }
}
