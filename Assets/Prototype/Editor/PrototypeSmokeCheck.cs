using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LIVE.Prototype.Editor
{
    // -batchmode -nographics -executeMethod LIVE.Prototype.Editor.PrototypeSmokeCheck.Run
    // Omit -quit; validation exits Unity after entering Play mode.
    public static class PrototypeSmokeCheck
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/BattlefieldPrototype.unity");
            SessionState.SetBool("LIVE.PrototypeSmokeCheck", true);
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void Subscribe()
        {
            EditorApplication.update += () =>
            {
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                    !SessionState.GetBool("LIVE.PrototypeSmokeCheck", false)) return;
                SessionState.SetBool("LIVE.PrototypeSmokeCheck", false);
                Validate();
            };
        }

        private static void Validate()
        {
            try
            {
                var board = UnityEngine.Object.FindAnyObjectByType<BattlefieldPrototype>();
                Require(board != null && board.Tiles.childCount == 18, "Expected a 3x6 board.");
                Require(board.GetComponentsInChildren<TextMesh>().Length == 0, "Battlefield should contain no text labels.");
                // Retain the original two-unit regression suite independently of the run's random starter.
                board.RunController.enabled = false;
                board.ClearUnits();
                board.ConfigureCombat(new List<PrototypeUnit>
                {
                    board.CreateUnit("A", 1, 1, new PrototypeCombatStats(), "Regression A"),
                    board.CreateUnit("B", 1, 4, new PrototypeCombatStats(), "Regression B")
                });
                Require(board.Units.Length == 2 && board.Units[0].Faction == "A" && board.Units[1].Faction == "B", "Expected A/B units.");
                var combat = board.Combat;
                combat.enabled = false; // Run the exact simulation step without wall-clock timing variability.
                combat.ResetBattle();
                VerifyOccupancy(combat);
                Require(combat.State == PrototypeCombatState.Ready, "Reset must enter Ready.");
                combat.Step(0.5f);
                Require(combat.State == PrototypeCombatState.Ready, "Preparation time must elapse first.");
                combat.Step(0.5f);
                Require(combat.State == PrototypeCombatState.Fighting, "Preparation must automatically start combat.");
                var a = board.Units[0];
                var b = board.Units[1];
                Require(PrototypeDamageCalculator.Calculate(20, 5) == 19, "Default damage must round to 19.");
                Require(PrototypeDamageCalculator.Calculate(0, int.MaxValue) == 1, "Damage floor must be 1.");
                Require(PrototypeDamageCalculator.Calculate(int.MaxValue, 0) == int.MaxValue, "Damage must not overflow.");
                a.TakeDamage(19);
                Require(a.CurrentHealth == 81, "Damage must decrease HP.");
                var fill = a.transform.Find("Health fill");
                Require(Mathf.Approximately(fill.localScale.x, 0.81f), "HP bar must follow health.");
                a.SetHealth(50);
                Require(Mathf.Approximately(fill.localPosition.x, -0.25f), "HP bar must stay left anchored.");
                a.SetHealth(150);
                Require(a.CurrentHealth == 100, "HP must clamp to maximum.");
                b.TakeDamage(1000);
                Require(!b.IsAlive && b.CurrentHealth == 0 && combat.Grid.IsFree(b.Cell), "Death must release occupancy.");
                Require(combat.State == PrototypeCombatState.Finished && combat.Winner == "A", "Elimination must decide winner immediately.");
                foreach (var renderer in b.GetComponentsInChildren<SpriteRenderer>()) Require(!renderer.enabled, "Dead units must disappear.");
                b.SetHealth(100);
                Require(!b.IsAlive && b.CurrentHealth == 0, "Direct healing must not resurrect dead units.");
                VerifyCompleteBattle(board);
                VerifyMultipleUnitsAndRouting();
                VerifyRangedAttacks();
                PrototypeRunSmokeCheck.Validate(board);
                PrototypeAbilitySmokeCheck.Validate();
                PrototypeAiSmokeCheck.Validate();
                PrototypeRosterSmokeCheck.Validate(board);
                Require(Camera.main != null && Camera.main.orthographic, "Expected orthographic camera.");
                Debug.Log("PROTOTYPE_SMOKE_CHECK_PASSED: board, stats, occupancy, reservations, movement, targeting, range, cooldown, damage, death, victory, full combat loop and reset.");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        private static void VerifyCompleteBattle(BattlefieldPrototype board)
        {
            var combat = board.Combat;
            string firstWinner = null;
            for (int run = 0; run < 2; run++)
            {
                combat.ResetBattle();
                var a = board.Units[0];
                var b = board.Units[1];
                Require(a.Cell == new Vector2Int(1, 1) && b.Cell == new Vector2Int(1, 4), "Reset must restore spawn cells.");
                Require(a.CurrentHealth == 100 && b.CurrentHealth == 100 && a.IsAlive && b.IsAlive, "Reset must revive and heal units.");
                combat.StartBattle();
                bool sawMovement = false, sawDamage = false, sawInterpolation = false;
                for (int step = 0; step < 1500 && combat.State == PrototypeCombatState.Fighting; step++)
                {
                    bool aMoving = a.IsMoving, bMoving = b.IsMoving;
                    int oldAHealth = a.CurrentHealth, oldBHealth = b.CurrentHealth;
                    combat.Step(0.02f);
                    VerifyOccupancy(combat);
                    sawMovement |= a.IsMoving || b.IsMoving;
                    sawDamage |= a.CurrentHealth < 100 || b.CurrentHealth < 100;
                    sawInterpolation |= a.IsMoving && (a.transform.position - combat.WorldPosition(a.Cell)).sqrMagnitude > 0.0001f;
                    if (aMoving) Require(b.CurrentHealth == oldBHealth, "Moving A must not attack.");
                    if (bMoving) Require(a.CurrentHealth == oldAHealth, "Moving B must not attack.");
                }
                Require(sawMovement && sawInterpolation && sawDamage, "Full loop must move smoothly and attack.");
                Require(combat.State == PrototypeCombatState.Finished && combat.Winner != null, "Unassisted combat must finish within 30 seconds.");
                if (run == 0) firstWinner = combat.Winner;
                else Require(combat.Winner == firstWinner, "Reset must reproduce deterministic combat.");
                Vector3 position = a.transform.position;
                int health = a.CurrentHealth;
                combat.Step(5);
                Require(a.transform.position == position && a.CurrentHealth == health && !a.IsMoving && !b.IsMoving, "Finished battle must stop.");
            }
            combat.ResetBattle();
            combat.StartBattle();
            combat.Step(0.02f);
            Require(board.Units[0].IsMoving, "Expected a movement reservation.");
            combat.ResetBattle();
            Require(combat.Grid.IsFree(new Vector2Int(1, 2)), "Reset during movement must clear reservations.");
            combat.StartBattle();
            combat.Step(0.02f);
            board.Units[0].TakeDamage(1000);
            Require(combat.Winner == "B" && combat.Grid.IsFree(new Vector2Int(1, 2)), "Death in transit must release reservation and allow B victory.");
        }

        private static void VerifyMultipleUnitsAndRouting()
        {
            var root = new GameObject("Smoke: multi-unit battle");
            try
            {
                var a = MakeUnit(root, "A", 1, 1);
                var blocker = MakeUnit(root, "A", 1, 2);
                var enemyLow = MakeUnit(root, "B", 2, 4);
                var enemyHigh = MakeUnit(root, "B", 0, 4);
                var combat = root.AddComponent<PrototypeCombatController>();
                combat.enabled = false;
                combat.Initialize(3, 6, new[] { a, blocker, enemyLow, enemyHigh }, BattlefieldPrototype.Position);
                Require(combat.FindNearestEnemy(a) == enemyHigh, "Equal distance must use row before creation order.");
                Require(combat.Grid.TryFindStep(a, enemyHigh, out var next) && next == new Vector2Int(0, 1), "Path must route around occupied cells deterministically.");
                Require(!combat.Grid.TryReserveStep(a, blocker.Cell), "Occupied destinations must be rejected.");
                Require(!combat.Grid.TryReserveStep(a, new Vector2Int(-1, 1)), "Out-of-bounds destinations must be rejected.");
                Require(combat.Grid.TryReserveStep(a, next) && !combat.Grid.IsFree(next), "Movement destination must be reserved.");
                combat.ResetBattle();
                combat.StartBattle();
                combat.Step(0.02f);
                Require(a.Target == enemyHigh, "Expected selected target.");
                enemyHigh.TakeDamage(1000);
                Require(a.Target == enemyLow && combat.State == PrototypeCombatState.Fighting, "Death must retarget immediately without premature victory.");
                for (int i = 0; i < 2000 && combat.State == PrototypeCombatState.Fighting; i++)
                {
                    combat.Step(0.02f);
                    VerifyOccupancy(combat);
                }
                Require(combat.State == PrototypeCombatState.Finished, "Multi-unit combat should complete.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void VerifyRangedAttacks()
        {
            var root = new GameObject("Smoke: ranged combat");
            try
            {
                var a = MakeUnit(root, "A", 1, 1, 3);
                var b = MakeUnit(root, "B", 1, 4, 3);
                var combat = root.AddComponent<PrototypeCombatController>();
                combat.enabled = false;
                combat.Initialize(3, 6, new[] { a, b }, BattlefieldPrototype.Position);
                combat.StartBattle();
                combat.Step(0.02f);
                Require(!a.IsMoving && !b.IsMoving && a.CurrentHealth == 81 && b.CurrentHealth == 81, "Range 3 should attack without moving.");
                for (int i = 0; i < 40; i++) combat.Step(0.02f);
                Require(a.CurrentHealth == 81 && b.CurrentHealth == 81, "Attack cooldown must prevent early attacks.");
                for (int i = 0; i < 12; i++) combat.Step(0.02f);
                Require(a.CurrentHealth == 62 && b.CurrentHealth == 62, "Attack must repeat after one second.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static PrototypeUnit MakeUnit(GameObject root, string faction, int row, int column, int range = 1)
        {
            var obj = new GameObject("Smoke unit " + faction);
            obj.transform.SetParent(root.transform);
            var unit = obj.AddComponent<PrototypeUnit>();
            unit.Initialize(faction, row, column, new PrototypeCombatStats { AttackRange = range }, null, null);
            return unit;
        }

        private static void VerifyOccupancy(PrototypeCombatController combat)
        {
            var occupied = new HashSet<Vector2Int>();
            foreach (var unit in combat.Units)
            {
                if (!unit.IsAlive) continue;
                Require(combat.Grid.Contains(unit.Cell), "Logical position must remain in bounds.");
                Require(occupied.Add(unit.Cell), "Alive units must never share cells.");
                Require(combat.Grid.Occupant(unit.Cell) == unit, "Grid must agree with unit position.");
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
