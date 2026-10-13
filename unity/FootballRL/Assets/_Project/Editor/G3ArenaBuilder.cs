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
    /// <summary>G3: KickConfig, punkt kopnięcia, podgląd zasięgu. Idempotentne.</summary>
    public static class G3ArenaBuilder
    {
        const string KickConfigPath = "Assets/_Project/Configs/KickConfig.asset";
        const string MovementConfigPath = "Assets/_Project/Configs/MovementConfig.asset";
        const string PrefabPath = "Assets/_Project/Prefabs/Arena.prefab";
        const string ScenePath = "Assets/_Project/Scenes/ManualPlayground.unity";
        const string PlayerObjectName = "Player_0";
        const string KickPointName = "KickPoint";
        const int DefaultPlayerId = 0;

        public static void RunBatch()
        {
            if (!BuildAll())
            {
                Debug.LogError("G3 build failed.");
                EditorApplication.Exit(1);
                return;
            }

            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("G3 build completed successfully.");
            EditorApplication.Exit(0);
        }

        [MenuItem("FootballRL/G3/Ensure Kick Setup")]
        public static void RunFromMenu()
        {
            if (BuildAll())
                EditorSceneManager.OpenScene(ScenePath);
        }

        public static bool RunBuildSilent() => BuildAll();

        static bool BuildAll()
        {
            if (!G2ArenaBuilder.RunBuildSilent())
            {
                Debug.LogError("G3: wymagany G2 (zawodnik).");
                return false;
            }

            var kickConfig = LoadOrCreateKickConfig();
            var movementConfig = AssetDatabase.LoadAssetAtPath<MovementConfig>(MovementConfigPath);

            if (!PatchArenaPrefab(kickConfig, movementConfig))
                return false;

            return PatchPlaygroundScene();
        }

        static KickConfig LoadOrCreateKickConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<KickConfig>(KickConfigPath);
            if (cfg != null)
                return cfg;

            cfg = ScriptableObject.CreateInstance<KickConfig>();
            AssetDatabase.CreateAsset(cfg, KickConfigPath);
            AssetDatabase.SaveAssets();
            return cfg;
        }

        static bool PatchArenaPrefab(KickConfig kickConfig, MovementConfig movementConfig)
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
                    Debug.LogError("G3: brak Player_0 — uruchom G2.");
                    return false;
                }

                var anchor = EnsureKickAnchor(playerMotor);
                EnsureKickVisualizer(playerMotor.gameObject, anchor, kickConfig);

                var motors = new List<PlayerMotor> { playerMotor };
                controller.SetReferences(arenaRoot, movementConfig, kickConfig, motors);
                EditorUtility.SetDirty(controller);

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        static PlayerKickAnchor EnsureKickAnchor(PlayerMotor motor)
        {
            var anchor = motor.GetComponent<PlayerKickAnchor>() ?? motor.gameObject.AddComponent<PlayerKickAnchor>();
            var kickPoint = motor.transform.Find(KickPointName);
            if (kickPoint == null)
            {
                var go = new GameObject(KickPointName);
                kickPoint = go.transform;
                kickPoint.SetParent(motor.transform, false);
                kickPoint.localPosition = new Vector3(0f, 0.15f, 0.45f);
                kickPoint.localRotation = Quaternion.identity;
            }

            anchor.SetKickPoint(kickPoint);
            return anchor;
        }

        static void EnsureKickVisualizer(GameObject player, PlayerKickAnchor anchor, KickConfig kickConfig)
        {
            var existing = player.GetComponentInChildren<KickRangeVisualizer>(true);
            if (existing == null)
            {
                var go = new GameObject("KickRangeVisualizer");
                go.transform.SetParent(player.transform, false);
                existing = go.AddComponent<KickRangeVisualizer>();
            }

            existing.Configure(anchor, kickConfig);
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
