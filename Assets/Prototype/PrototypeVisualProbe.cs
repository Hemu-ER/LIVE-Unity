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
            yield return Capture("prep-1280x720", 1280, 720);
            yield return Capture("prep-800x600", 800, 600);
            yield return Capture("prep-720x1280", 720, 1280);
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
            Debug.Log("VISUAL_PROBE_PASSED");
            Application.Quit(0);
        }

        private IEnumerator Capture(string name, int width, int height)
        {
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, name + ".png"));
            yield return new WaitForSecondsRealtime(0.5f);
        }
    }
}
#endif
