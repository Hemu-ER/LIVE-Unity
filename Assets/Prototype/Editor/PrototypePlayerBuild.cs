using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace LIVE.Prototype.Editor
{
    public static class PrototypePlayerBuild
    {
        public static void BuildVisualCheck()
        {
            string path = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,
                "../../PrototypePlayer/LIVEPrototype.exe"));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/BattlefieldPrototype.unity" },
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Visual-check player build failed: " + report.summary.result);
            UnityEngine.Debug.Log("PROTOTYPE_PLAYER_BUILD_PASSED: " + path);
        }
    }
}
