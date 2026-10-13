#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FootballRL.Editor
{
    public static class G6ArenaBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/ManualPlayground.unity";
        const string PrefabPath = "Assets/_Project/Prefabs/Arena.prefab";

        public static void RunBatch()
        {
            if (!RunBuildSilent())
            {
                Debug.LogError("G6 build failed.");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("G6 build completed successfully.");
            EditorApplication.Exit(0);
        }

        [MenuItem("FootballRL/G6/Ensure G6 Prerequisites")]
        public static void RunFromMenu() => RunBuildSilent();

        public static bool RunBuildSilent()
        {
            if (!G5ArenaBuilder.RunBuildSilent())
            {
                Debug.LogError("G6: wymagany G5.");
                return false;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var arenas = Object.FindObjectsByType<Gameplay.ArenaController>();
            if (arenas.Length != 1)
            {
                Debug.LogError(
                    $"G6: ManualPlayground powinien mieć dokładnie jedną arenę, jest {arenas.Length}.");
                return false;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("G6: brak prefabu Arena.");
                return false;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            return true;
        }
    }
}
#endif
