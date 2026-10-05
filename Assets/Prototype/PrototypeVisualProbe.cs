#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace LIVE.Prototype
{
    // Opt-in development-player screenshot probe; never runs in ordinary Play or release builds.
    public sealed class PrototypeVisualProbe : MonoBehaviour
    {
        private string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-live-visual-check");
            if (index < 0 || index + 1 >= args.Length) return;
            var probe = new GameObject("Development Visual Probe").AddComponent<PrototypeVisualProbe>();
            probe.output = args[index + 1];
        }

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            Directory.CreateDirectory(output);
            yield return null;
            var board = FindAnyObjectByType<BattlefieldPrototype>();
            var controller = board.RunController;
            controller.enabled = false;
            controller.ResetRun();
            while (!UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            controller.Buy(0);controller.Buy(1);
            var hud=board.GetComponent<PrototypeRunHud>();hud.Refresh();hud.BenchButtons[0].onClick.Invoke();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-live-items-check") >= 0)
            {
                var owned=controller.Run.Player.OwnedUnits[0];
                foreach(var id in new[]{"crafted_sheath_sheath","crafted_gold_bracelet_quiver","wire"})
                {
                    controller.AcquireItem(id);
                    foreach(var item in controller.Run.Items.Items)if(item.UnitId==0){controller.EquipItem(item.InstanceId,owned.InstanceId);break;}
                }
                foreach(var definition in controller.Run.ItemCatalog.Items)
                { if(controller.Run.Items.FreeSlots==0)break;controller.AcquireItem(definition.Id); }
                hud.BoardButtons[4].onClick.Invoke();hud.ItemButtons[8].onClick.Invoke();hud.Refresh();
            }
            yield return Capture("prep-1920x1080", 1920, 1080);
            yield return Capture("prep-1600x900", 1600, 900);
            yield return Capture("prep-1280x720", 1280, 720);
            for (int slot = 0; slot < controller.Run.Shop.Count; slot++)
            {
                if (!controller.Buy(slot)) continue;
                var unit = controller.Run.Player.AtBench(0);
                if (unit != null) controller.Move(unit.InstanceId, PrototypeUnitLocation.Board, 0, 0);
                break;
            }
            controller.Ready();
            for (int tick = 0; tick < 80; tick++) controller.Step(0.02f);
            yield return Capture("combat-1280x720", 1280, 720);
            while (controller.Run.Phase == PrototypeRunPhase.Combat) controller.Step(0.02f);
            yield return Capture("result-1280x720", 1280, 720);
            // A separate opt-in view fixture makes shield/gauge bars visible in a repeatable capture.
            board.GetComponent<PrototypeRunHud>().Canvas.enabled = false;
            board.GetComponent<PrototypeRunHud>().enabled = false;
            board.ClearUnits();
            var combatants = new System.Collections.Generic.List<PrototypeUnit>();
            int[] definitions = { 0, 1, 5, 3, 6, 7 };
            for (int i = 0; i < definitions.Length; i++)
            {
                var definition = controller.Data.Units[definitions[i]];
                combatants.Add(board.CreateUnit(i < 3 ? "A" : "B", i % 3, i < 3 ? 1 : 4,
                    controller.Data.CombatStats(definition.Id, 1), definition.DisplayName, definition.Abilities));
            }
            board.ConfigureCombat(combatants);
            board.Combat.StartBattle();
            for (int tick = 0; tick < 12; tick++) board.Combat.Step(0.02f);
            yield return Capture("abilities-1280x720", 1280, 720);
            Debug.Log("VISUAL_PROBE_PASSED");
            Application.Quit(0);
        }

        private IEnumerator Capture(string name, int width, int height)
        {
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(0.5f);
            var board=FindAnyObjectByType<BattlefieldPrototype>();var hud=board.GetComponent<PrototypeRunHud>();
            if(hud.enabled)
            {
                if(!hud.PhaseText.Contains(board.RunController.Run.Phase.ToString()))
                {Debug.LogError("UI_AUTO_REFRESH_FAILED: "+name);Application.Quit(1);yield break;}
                hud.Refresh();Canvas.ForceUpdateCanvases();
                Debug.Log("UI_CAPTURE_STATE: "+name+" / "+hud.PhaseText+" / inspector="+hud.InspectorVisible);
            }
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"));
            yield return new WaitForSecondsRealtime(0.5f);
        }
    }
}
#endif
