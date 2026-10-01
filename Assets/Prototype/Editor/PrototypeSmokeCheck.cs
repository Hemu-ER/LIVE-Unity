using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LIVE.Prototype.Editor
{
    // Batch entry point: -batchmode -executeMethod LIVE.Prototype.Editor.PrototypeSmokeCheck.Run
    // Do not supply -quit: the check exits after entering Play mode.
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
                Require(board != null && board.Tiles.childCount == 18, "Expected 18 battlefield tiles.");
                Require(board.Units.Length == 2, "Expected two units.");
                Require(board.Units[0].Faction == "A" && board.Units[1].Faction == "B", "Expected opposing factions.");
                Require(board.Units[0].transform.position.x < 0 && board.Units[1].transform.position.x > 0, "Units must occupy their own half.");
                foreach (var unit in board.Units)
                {
                    Require(unit.CurrentHealth == 100, "Expected full initial health.");
                    unit.SetHealth(50);
                    var fill = unit.transform.Find("Health fill");
                    Require(Mathf.Approximately(fill.localScale.x, 0.5f), "Health fill must track health.");
                    Require(Mathf.Approximately(fill.localPosition.x, -0.25f), "Health fill must remain left anchored.");
                    unit.SetHealth(-5);
                    Require(unit.CurrentHealth == 0 && fill.localScale.x == 0, "Health must clamp at zero.");
                    unit.SetHealth(150);
                    Require(unit.CurrentHealth == 100, "Health must clamp at maximum.");
                }
                Require(Camera.main != null && Camera.main.orthographic, "Expected orthographic camera.");
                Debug.Log("PROTOTYPE_SMOKE_CHECK_PASSED: 18 tiles, factions, unit positions, health rendering and clamping.");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
