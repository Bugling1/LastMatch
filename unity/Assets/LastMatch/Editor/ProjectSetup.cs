using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace LastMatch.EditorTools
{
    /// <summary>Builds the one scene the game needs and applies mobile player settings. Run from the menu or with
    /// Unity.exe -batchmode -quit -projectPath ... -executeMethod LastMatch.EditorTools.ProjectSetup.Setup</summary>
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/LastMatch/Scenes/Main.unity";

        [MenuItem("Last Match/Set up project")]
        public static void Setup()
        {
            Directory.CreateDirectory("Assets/LastMatch/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            camGo.tag = "MainCamera";
            cam.orthographic = true; cam.orthographicSize = 8f; cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.106f, 0.078f, 0.227f); cam.nearClipPlane = -10f; cam.farClipPlane = 10f;
            camGo.transform.position = new Vector3(0, 0, -5f);

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();

            var game = new GameObject("Game");
            game.AddComponent<LastMatch.View.GameController>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.productName = "Last Match";
            PlayerSettings.companyName = "Last Match";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.lastmatch.game");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.lastmatch.game");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            AssetDatabase.SaveAssets();
            Debug.Log("Last Match: scene and player settings ready at " + ScenePath);
        }
    }
}
