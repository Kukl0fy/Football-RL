#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using FootballRL.Gameplay;
using FootballRL.Input;
using FootballRL.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FootballRL.Editor
{
    /// <summary>G4: RoundConfig, spawn epizodu, reguły rundy. Idempotentne.</summary>
    public static class G4ArenaBuilder
    {
        const string RoundConfigPath = "Assets/_Project/Configs/RoundConfig.asset";
        const string MovementConfigPath = "Assets/_Project/Configs/MovementConfig.asset";
        const string PrefabPath = "Assets/_Project/Prefabs/Arena.prefab";
        const string ScenePath = "Assets/_Project/Scenes/ManualPlayground.unity";
        const string PlayerObjectName = "Player_0";
        const int DefaultPlayerId = 0;

        public static void RunBatch()
        {
            if (!BuildAll())
            {
                Debug.LogError("G4 build failed.");
                EditorApplication.Exit(1);
                return;
            }

            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("G4 build completed successfully.");
            EditorApplication.Exit(0);
        }

        [MenuItem("FootballRL/G4/Ensure Round And Goal Setup")]
        public static void RunFromMenu()
        {
            if (BuildAll())
                EditorSceneManager.OpenScene(ScenePath);
        }

        public static bool RunBuildSilent() => BuildAll();

        static bool BuildAll()
        {
            if (!G3ArenaBuilder.RunBuildSilent())
            {
                Debug.LogError("G4: wymagany G3 (kopnięcie).");
                return false;
            }

            var roundConfig = LoadOrCreateRoundConfig();
            var movementConfig = AssetDatabase.LoadAssetAtPath<MovementConfig>(MovementConfigPath);

            if (!PatchArenaPrefab(roundConfig, movementConfig))
                return false;

            return PatchPlaygroundScene();
        }

        static RoundConfig LoadOrCreateRoundConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<RoundConfig>(RoundConfigPath);
            if (cfg != null)
                return cfg;

            if (!Directory.Exists("Assets/_Project/Configs"))
                Directory.CreateDirectory("Assets/_Project/Configs");

            cfg = ScriptableObject.CreateInstance<RoundConfig>();
            AssetDatabase.CreateAsset(cfg, RoundConfigPath);
            AssetDatabase.SaveAssets();
            return cfg;
        }

        static bool PatchArenaPrefab(RoundConfig roundConfig, MovementConfig movementConfig)
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (prefabRoot == null)
                return false;

            try
            {
                var arenaRoot = prefabRoot.GetComponent<ArenaRoot>();
                var controller = prefabRoot.GetComponent<ArenaController>();
                if (arenaRoot == null || controller == null)
                    return false;

                var spawnState = prefabRoot.GetComponent<ArenaEpisodeSpawnState>() ??
                                 prefabRoot.AddComponent<ArenaEpisodeSpawnState>();

                PlayerMotor playerMotor = null;
                var playersRoot = arenaRoot.PlayersRoot;
                if (playersRoot != null)
                {
                    foreach (Transform child in playersRoot)
                    {
                        if (child.name == PlayerObjectName)
                            playerMotor = child.GetComponent<PlayerMotor>();
                    }
                }

                if (playerMotor == null)
                {
                    Debug.LogError("G4: brak Player_0 — uruchom G2.");
                    return false;
                }

                var motors = new List<PlayerMotor> { playerMotor };
                var kickConfig = controller.KickConfig;
                var scenario = controller.DefaultScenarioAsset;
                var target = controller.ExerciseTargetComponent;
                controller.SetReferences(arenaRoot, movementConfig, kickConfig, roundConfig, scenario, target, motors);

                var so = new SerializedObject(controller);
                so.FindProperty("episodeSpawnState").objectReferenceValue = spawnState;
                so.ApplyModifiedPropertiesWithoutUndo();

                var ballTf = arenaRoot.BallBody != null ? arenaRoot.BallBody.transform : null;
                spawnState.CaptureFromScene(ballTf, motors.ToArray());

                EditorUtility.SetDirty(controller);
                EditorUtility.SetDirty(spawnState);

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        static bool PatchPlaygroundScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var arenaGo = GameObject.Find("Arena");
            if (arenaGo == null)
                return false;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null && PrefabUtility.IsPartOfPrefabInstance(arenaGo))
                PrefabUtility.RevertPrefabInstance(arenaGo, InteractionMode.AutomatedAction);

            var controller = arenaGo.GetComponent<ArenaController>();
            var input = Object.FindAnyObjectByType<KeyboardCommandSource>();
            var hud = Object.FindAnyObjectByType<ArenaDebugHUD>();
            if (controller != null && input != null && hud != null)
                hud.Configure(controller, input, DefaultPlayerId);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return controller != null;
        }
    }
}
#endif
