#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FootballRL.Editor
{
    /// <summary>
    /// G0: minimalna scena startowa i ustawienia projektu. Idempotentne.
    /// Menu: FootballRL/G0/Bootstrap ManualPlayground
    /// Batchmode: -executeMethod FootballRL.Editor.G0Bootstrap.RunBatch
    /// </summary>
    public static class G0Bootstrap
    {
        const string ScenePath = "Assets/_Project/Scenes/ManualPlayground.unity";

        public static void RunBatch()
        {
            if (!ApplyProjectSettings() || !EnsureScene())
            {
                Debug.LogError("G0 bootstrap failed.");
                EditorApplication.Exit(1);
                return;
            }

            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("G0 bootstrap completed successfully.");
            EditorApplication.Exit(0);
        }

        [MenuItem("FootballRL/G0/Bootstrap ManualPlayground")]
        public static void RunFromMenu()
        {
            ApplyProjectSettings();
            EnsureScene();
            EditorSceneManager.OpenScene(ScenePath);
        }

        static bool ApplyProjectSettings()
        {
            Time.fixedDeltaTime = 0.02f;
            ConfigureNewInputSystemOnly();

            var scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
            EditorBuildSettings.scenes = scenes;

            AssetDatabase.SaveAssets();
            return true;
        }

        /// <summary>Unity 6000.x: activeInputHandler przez SerializedObject (brak PlayerSettings.activeInputHandler).</summary>
        static void ConfigureNewInputSystemOnly()
        {
            var playerSettings = Resources.FindObjectsOfTypeAll<PlayerSettings>().FirstOrDefault();
            if (playerSettings == null)
            {
                Debug.LogWarning("G0: nie znaleziono PlayerSettings — pominięto activeInputHandler.");
                return;
            }

            var serialized = new SerializedObject(playerSettings);
            var activeInputHandler = serialized.FindProperty("activeInputHandler");
            if (activeInputHandler == null)
            {
                Debug.LogWarning("G0: brak właściwości activeInputHandler.");
                return;
            }

            activeInputHandler.intValue = 1;
            serialized.ApplyModifiedProperties();
        }

        static bool EnsureScene()
        {
            var sceneDir = Path.GetDirectoryName(ScenePath);
            if (!string.IsNullOrEmpty(sceneDir) && !Directory.Exists(sceneDir))
            {
                Directory.CreateDirectory(sceneDir);
                AssetDatabase.Refresh();
            }

            if (File.Exists(ScenePath))
            {
                var existing = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                if (existing.IsValid() && GameObject.Find("Ground") != null)
                {
                    Debug.Log("G0 scene already present: " + ScenePath);
                    EditorSceneManager.SaveScene(existing);
                    return true;
                }
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var ground = GameObject.Find("Ground");
            if (ground == null)
            {
                ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
                ground.transform.localScale = new Vector3(2f, 1f, 2f);
            }

            var mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.transform.position = new Vector3(0f, 5f, -8f);
                mainCamera.transform.rotation = Quaternion.Euler(25f, 0f, 0f);
            }

            var light = Object.FindFirstObjectByType<Light>();
            if (light != null && light.type == LightType.Directional)
            {
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            return EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }
}
#endif
