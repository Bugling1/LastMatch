using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LastMatch.EditorTools
{
    /// <summary>Builds a sideloadable APK. From a terminal:
    /// Unity.exe -batchmode -quit -projectPath ... -buildTarget Android -executeMethod LastMatch.EditorTools.BuildAndroid.Apk</summary>
    public static class BuildAndroid
    {
        [MenuItem("Last Match/Build Android APK")]
        public static void Apk()
        {
            string outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "build"));
            Directory.CreateDirectory(outDir);
            string apk = Path.Combine(outDir, "LastMatch.apk");
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.bundleVersion = "0.1." + DateTime.UtcNow.ToString("yyyyMMdd");
            PlayerSettings.Android.bundleVersionCode = (int)(DateTime.UtcNow - new DateTime(2026, 1, 1)).TotalHours;
            var scenes = new[] { "Assets/LastMatch/Scenes/Main.unity" };
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, locationPathName = apk, target = BuildTarget.Android, options = BuildOptions.None });
            var s = report.summary;
            Debug.Log("Last Match APK: " + s.result + " " + s.outputPath + " " + (s.totalSize / 1048576) + " MB, errors=" + s.totalErrors);
            if (s.result != BuildResult.Succeeded) throw new Exception("Android build failed: " + s.result);
        }
    }
}
