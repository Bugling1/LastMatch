using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LastMatch.EditorTools
{
    /// <summary>Desktop build, used for screenshots and quick play tests.
    /// Unity.exe -batchmode -quit -projectPath ... -buildTarget Win64 -executeMethod LastMatch.EditorTools.BuildWindows.Exe</summary>
    public static class BuildWindows
    {
        [MenuItem("Last Match/Build Windows (test)")]
        public static void Exe()
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "build", "win"));
            Directory.CreateDirectory(outDir);
            string exe = Path.Combine(outDir, "LastMatch.exe");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 540; PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { "Assets/LastMatch/Scenes/Main.unity" }, locationPathName = exe, target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
            var s = report.summary;
            Debug.Log("Last Match EXE: " + s.result + " " + s.outputPath + " errors=" + s.totalErrors);
            if (s.result != BuildResult.Succeeded) throw new Exception("Windows build failed: " + s.result);
        }
    }
}
