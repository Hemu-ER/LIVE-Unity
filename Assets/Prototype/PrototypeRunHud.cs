using UnityEngine;

namespace LIVE.Prototype
{
    // Temporary responsive IMGUI presenter. Commands are device-independent model operations.
    public sealed class PrototypeRunHud : MonoBehaviour
    {
        public const float DesignWidth = 1100, DesignHeight = 850;
        private static readonly Rect BoardArea = new Rect(100, 120, 900, 410);
        private PrototypeRunController controller;
        private BattlefieldPrototype board;
        private long selectedId;
        private GUIStyle title, text, small, button, unitLabel;
        public void Initialize(PrototypeRunController runController, BattlefieldPrototype battlefield)
        { controller = runController; board = battlefield; }

        public static float Scale(int width, int height) => Mathf.Max(0.01f, Mathf.Min(width / DesignWidth, height / DesignHeight));
        public static Vector2 Offset(int width, int height)
        {
            float scale = Scale(width, height);
            return new Vector2((width - DesignWidth * scale) / 2, (height - DesignHeight * scale) / 2);
        }
        public static Rect CameraViewport(int width, int height)
        {
            float scale = Scale(width, height);
            Vector2 offset = Offset(width, height);
            return new Rect((offset.x + BoardArea.x * scale) / Mathf.Max(1, width),
                1 - (offset.y + BoardArea.yMax * scale) / Mathf.Max(1, height),
                BoardArea.width * scale / Mathf.Max(1, width), BoardArea.height * scale / Mathf.Max(1, height));
        }

        private void Styles()
        {
            if (text != null) return;
            title = new GUIStyle(GUI.skin.label) { fontSize = 23, fontStyle = FontStyle.Bold };
            text = new GUIStyle(GUI.skin.label) { fontSize = 17 };
            small = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            button = new GUIStyle(GUI.skin.button) { fontSize = 16, wordWrap = true };
            unitLabel = new GUIStyle(GUI.skin.box) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
        }

        private void OnGUI()
        {
            if (controller == null || controller.Run == null || board.View == null) return;
            Styles();
            var run = controller.Run;
            var player = run.Player;
            if (run.Phase != PrototypeRunPhase.Prep || player.Find(selectedId) == null) selectedId = 0;
            float scale = Scale(Screen.width, Screen.height);
            Vector2 offset = Offset(Screen.width, Screen.height);
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            bool oldEnabled = GUI.enabled;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offset.x, offset.y, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            try
            {
                GUI.Box(new Rect(18, 12, 1064, 98), GUIContent.none);
                GUI.Label(new Rect(34, 22, 450, 30), $"LIVE  /  ROUND {run.Round}  /  {run.Phase}", title);
                GUI.Label(new Rect(35, 60, 270, 30), $"Time {Mathf.CeilToInt(run.RemainingSeconds)}s    Credits {player.Credits}", text);
                string exp = player.Mastery.Level == 20 ? "MAX" : $"{player.Mastery.CurrentExp}/{player.Mastery.NextLevelExp} EXP";
                GUI.Label(new Rect(320, 60, 350, 30), $"Mastery Lv {player.Mastery.Level}   {exp}", text);
                GUI.Label(new Rect(690, 24, 240, 28), $"Seed {run.Seed}  |  {run.Enemies.OpponentType}", small);
                if (GUI.Button(new Rect(932, 29, 132, 54), "Reset Run", button))
                { selectedId = 0; controller.ResetRun(); return; }

                DrawBoard();
                GUI.Label(new Rect(26, 535, 760, 27), "BENCH  /  Select a unit, then click an empty destination", text);
                bool prep = run.Phase == PrototypeRunPhase.Prep;
                GUI.enabled = prep;
                for (int slot = 0; slot < player.BenchSize; slot++)
                {
                    var unit = player.AtBench(slot);
                    GUI.color = unit != null && selectedId == unit.InstanceId ? new Color(0.45f, 0.9f, 1f) : Color.white;
                    string label = unit == null ? $"[{slot + 1}] Empty" : controller.Label(unit.DefinitionId, unit.Stars);
                    if (GUI.Button(new Rect(26 + slot * 131, 568, 123, 57), label, button))
                        ClickSlot(unit, PrototypeUnitLocation.Bench, slot, 0);
                }
                GUI.color = Color.white;
                GUI.enabled = true;
                GUI.Label(new Rect(26, 638, 800, 27), "SHOP  /  " + (prep ? "Click to buy" : "Locked during combat and round transition"), text);
                GUI.enabled = prep;
                for (int slot = 0; slot < run.Shop.Count; slot++)
                {
                    string id = run.Shop.Slot(slot);
                    string label = id == null ? "Sold / Empty" :
                        controller.Data.Definition(id).DisplayName + "\n" + controller.Data.Definition(id).Cost + " Credits";
                    bool affordable = id != null && player.Credits >= controller.Data.Definition(id).Cost && player.EmptyBench() >= 0;
                    GUI.enabled = prep && affordable;
                    if (GUI.Button(new Rect(26 + slot * 211, 671, 201, 62), label, button)) controller.Buy(slot);
                }

                GUI.enabled = prep && player.Credits >= controller.Data.Rules.RerollCost;
                if (GUI.Button(new Rect(26, 747, 174, 42), $"Reroll ({controller.Data.Rules.RerollCost} Cr)", button)) controller.Reroll();
                GUI.enabled = prep && player.Credits >= controller.Data.Rules.InvestCost && player.Mastery.Level < 20;
                if (GUI.Button(new Rect(214, 747, 244, 42), $"Mastery +{controller.Data.Rules.InvestExp} EXP ({controller.Data.Rules.InvestCost} Cr)", button)) controller.Invest();
                var selected = player.Find(selectedId);
                GUI.enabled = prep && selected != null;
                string sell = selected == null ? "Sell selected" : $"Sell ({PrototypeEconomy.SalePrice(controller.Data.Definition(selected.DefinitionId).Cost, selected.Stars)} Cr)";
                if (GUI.Button(new Rect(472, 747, 185, 42), sell, button)) { controller.Sell(selectedId); selectedId = 0; }
                GUI.enabled = prep;
                if (GUI.Button(new Rect(671, 747, 187, 42), "Clear selection", button)) selectedId = 0;
                if (GUI.Button(new Rect(872, 747, 202, 42), "READY", button)) { selectedId = 0; controller.Ready(); }
                GUI.enabled = true;
                GUI.Label(new Rect(27, 802, 1046, 32), run.LastMessage, text);
            }
            finally { GUI.matrix = oldMatrix; GUI.color = oldColor; GUI.enabled = oldEnabled; }
        }

        private Vector2 LogicalScreen(Vector3 world)
        {
            Vector3 screen = board.View.WorldToScreenPoint(world);
            return (new Vector2(screen.x, Screen.height - screen.y) - Offset(Screen.width, Screen.height)) / Scale(Screen.width, Screen.height);
        }

        private void DrawBoard()
        {
            var run = controller.Run;
            if (run.Phase == PrototypeRunPhase.Prep)
            {
                // Transparent cell hit regions; no rule changes are performed in this presenter.
                for (int row = 0; row < 3; row++)
                for (int column = 0; column < 3; column++)
                {
                    Vector3 position = board.transform.TransformPoint(BattlefieldPrototype.Position(row, column));
                    Vector2 center = LogicalScreen(position);
                    float cellWidth = Mathf.Abs(LogicalScreen(position + Vector3.right * 1.48f).x - center.x);
                    float cellHeight = Mathf.Abs(LogicalScreen(position + Vector3.up * 1.48f).y - center.y);
                    var rect = new Rect(center.x - cellWidth / 2, center.y - cellHeight / 2, cellWidth, cellHeight);
                    var unit = run.Player.AtBoard(row, column);
                    if (unit != null && unit.InstanceId == selectedId)
                    {
                        Color previous = GUI.color;
                        GUI.color = new Color(0.3f, 0.85f, 1, 0.5f);
                        GUI.Box(rect, GUIContent.none);
                        GUI.color = previous;
                    }
                    if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                        ClickSlot(unit, PrototypeUnitLocation.Board, row, column);
                }
            }
            foreach (var unit in board.Units)
            {
                if (unit == null || !unit.IsAlive) continue;
                Vector2 location = LogicalScreen(unit.transform.position + Vector3.up * 0.91f);
                GUI.Label(new Rect(location.x - 48, location.y - 10, 96, 23), unit.DisplayLabel, unitLabel);
            }
        }

        private void ClickSlot(PrototypeOwnedUnit occupant, PrototypeUnitLocation location, int first, int second)
        {
            if (occupant != null)
            { selectedId = selectedId == occupant.InstanceId ? 0 : occupant.InstanceId; return; }
            if (selectedId != 0 && controller.Move(selectedId, location, first, second)) selectedId = 0;
        }
    }
}
