using UnityEngine;
using System.Collections.Generic;

namespace LIVE.Prototype
{
    public sealed class BattlefieldPrototype : MonoBehaviour
    {
        public const int Rows = 3;
        public const int Columns = 6;
        private const float Spacing = 1.6f;
        private static readonly Color TeamA = new Color(0.22f, 0.72f, 1f);
        private static readonly Color TeamB = new Color(1f, 0.43f, 0.29f);
        private readonly List<GameObject> unitObjects = new List<GameObject>();
        public PrototypeRunController RunController { get; private set; }
        public Camera View => view;
        public PrototypeCombatController Combat { get; private set; }
        private Sprite square;
        private Texture2D texture;
        private Camera view;

        public Transform Tiles { get; private set; }
        public PrototypeUnit[] Units { get; private set; } = new PrototypeUnit[0];

        private void Awake()
        {
            texture = new Texture2D(1, 1) { name = "Prototype white pixel" };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            square = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
            Tiles = new GameObject("Tiles (3 rows x 6 columns)").transform;
            Tiles.SetParent(transform, false);

            for (int row = 0; row < Rows; row++)
            for (int column = 0; column < Columns; column++)
            {
                Color team = column < 3 ? TeamA : TeamB;
                MakeSprite($"Tile {row + 1},{column + 1} / {(column < 3 ? "A" : "B")}", Tiles,
                    Position(row, column), new Vector2(1.56f, 1.56f), Color.Lerp(new Color(0.07f, 0.1f, 0.17f), team, 0.28f), 0);
            }

            MakeSprite("Faction divider", transform, Vector3.zero, new Vector2(0.018f, 4.76f), new Color(0.6f, 0.7f, 0.8f), 1);

            Combat = gameObject.AddComponent<PrototypeCombatController>();
            Combat.enabled = false; // The run controller owns the simulation clock.

            view = Camera.main;
            if (view == null)
            {
                view = new GameObject("Prototype Camera", typeof(Camera)).GetComponent<Camera>();
                view.transform.SetParent(transform, false);
                view.tag = "MainCamera";
            }
            view.orthographic = true;
            view.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = new Color(0.035f, 0.05f, 0.085f);
            FitCamera();
            RunController = gameObject.AddComponent<PrototypeRunController>();
            RunController.Initialize(this);
            gameObject.AddComponent<PrototypeRunHud>().Initialize(RunController, this);
        }

        private void LateUpdate() => FitCamera();

        private void FitCamera()
        {
            if (view != null)
            {
                view.rect = PrototypeRunHud.CameraViewport(Screen.width, Screen.height);
                view.orthographicSize = Mathf.Max(2.7f, 5.2f / Mathf.Max(0.1f, view.aspect));
            }
        }

        public static Vector3 Position(int row, int column) =>
            new Vector3((column - 2.5f) * Spacing, (1 - row) * Spacing, 0);

        public PrototypeUnit CreateUnit(string faction, int row, int column, PrototypeCombatStats stats, string label)
        {
            Color color = faction == "A" ? TeamA : TeamB;
            var root = new GameObject(label + " / " + faction);
            unitObjects.Add(root);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Position(row, column);
            var character = new GameObject("Character").transform;
            character.SetParent(root.transform, false);
            // A small geometric character silhouette: head, torso and two feet.
            MakeSprite("Head", character, new Vector3(0, 0.27f, 0), new Vector2(0.32f, 0.32f), Color.Lerp(color, Color.white, 0.25f), 2);
            MakeSprite("Body", character, new Vector3(0, -0.08f, 0), new Vector2(0.48f, 0.34f), color, 2);
            MakeSprite("Left foot", character, new Vector3(-0.14f, -0.33f, 0), new Vector2(0.18f, 0.16f), color, 2);
            MakeSprite("Right foot", character, new Vector3(0.14f, -0.33f, 0), new Vector2(0.18f, 0.16f), color, 2);
            MakeSprite("Health background", root.transform, new Vector3(0, 0.62f, 0), new Vector2(1.04f, 0.09f), new Color(0.02f, 0.03f, 0.04f), 3);
            var fill = MakeSprite("Health fill", root.transform, new Vector3(0, 0.62f, -0.01f), new Vector2(1f, 0.05f), new Color(0.35f, 0.95f, 0.5f), 4);
            var unit = root.AddComponent<PrototypeUnit>();
            unit.Initialize(faction, row, column, stats, fill.transform, character);
            unit.DisplayLabel = label;
            return unit;
        }

        private SpriteRenderer MakeSprite(string objectName, Transform parent, Vector3 position, Vector2 size, Color color, int order)
        {
            var obj = new GameObject(objectName);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = new Vector3(size.x, size.y, 1);
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = color;
            renderer.sortingOrder = order;
            return renderer;
        }

        public void ClearUnits()
        {
            Combat.FinishAsDraw();
            foreach (var obj in unitObjects)
            {
                if (obj == null) continue;
                obj.SetActive(false);
                Destroy(obj);
            }
            unitObjects.Clear();
            Units = new PrototypeUnit[0];
        }

        public void ConfigureCombat(List<PrototypeUnit> units)
        {
            Units = units.ToArray();
            Combat.Initialize(Rows, Columns, Units, (row, column) => transform.TransformPoint(Position(row, column)));
        }

        private void OnDestroy()
        {
            if (square != null) Destroy(square);
            if (texture != null) Destroy(texture);
        }
    }
}
