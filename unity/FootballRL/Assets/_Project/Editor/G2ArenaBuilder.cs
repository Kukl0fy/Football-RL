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
    /// <summary>G2: zawodnik, MovementConfig, ArenaController, wejście i HUD. Idempotentne.</summary>
    public static class G2ArenaBuilder
    {
        const string MovementConfigPath = "Assets/_Project/Configs/MovementConfig.asset";
        const string PrefabPath = "Assets/_Project/Prefabs/Arena.prefab";
        const string ScenePath = "Assets/_Project/Scenes/ManualPlayground.unity";
        const string MatDir = "Assets/_Project/Configs/Materials";
        const string PlayerObjectName = "Player_0";

        const int DefaultPlayerId = 0;
        const int DefaultTeamId = 0;

        public static void RunBatch()
        {
            if (!BuildAll())
            {
                Debug.LogError("G2 build failed.");
                EditorApplication.Exit(1);
                return;
            }

            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("G2 build completed successfully.");
            EditorApplication.Exit(0);
        }

        [MenuItem("FootballRL/G2/Ensure Player And Arena Setup")]
        public static void RunFromMenu()
        {
            if (BuildAll())
                EditorSceneManager.OpenScene(ScenePath);
        }

        public static bool RunBuildSilent() => BuildAll();

        static bool BuildAll()
        {
            var movementConfig = LoadOrCreateMovementConfig();
            var playerMat = EnsurePlayerMaterial();

            if (!PatchArenaPrefab(movementConfig, playerMat))
                return false;

            return PatchPlaygroundScene(movementConfig);
        }

        static MovementConfig LoadOrCreateMovementConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<MovementConfig>(MovementConfigPath);
            if (cfg != null)
                return cfg;

            cfg = ScriptableObject.CreateInstance<MovementConfig>();
            AssetDatabase.CreateAsset(cfg, MovementConfigPath);
            AssetDatabase.SaveAssets();
            return cfg;
        }

        static Material EnsurePlayerMaterial()
        {
            const string path = MatDir + "/Player.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
                return mat;

            if (!Directory.Exists(MatDir))
                Directory.CreateDirectory(MatDir);

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader) { color = new Color(0.15f, 0.45f, 0.85f) };
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            return mat;
        }

        static bool PatchArenaPrefab(MovementConfig movementConfig, Material playerMat)
        {
            var prefabRoot = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (prefabRoot == null)
            {
                Debug.LogError("G2: brak prefabu areny — uruchom G1.");
                return false;
            }

            try
            {
                var arenaRoot = prefabRoot.GetComponent<ArenaRoot>();
                if (arenaRoot == null)
                {
                    Debug.LogError("G2: prefab areny bez ArenaRoot.");
                    return false;
                }

                var playersRoot = arenaRoot.PlayersRoot;
                if (playersRoot == null)
                {
                    Debug.LogError("G2: brak kontenera Players.");
                    return false;
                }

                var playerMotor = EnsureSinglePlayer(playersRoot, playerMat, movementConfig);
                var controller = EnsureArenaController(prefabRoot, arenaRoot, movementConfig, playerMotor);

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                return controller != null && playerMotor != null;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        static PlayerMotor EnsureSinglePlayer(Transform playersRoot, Material playerMat,
            MovementConfig movementConfig)
        {
            PlayerMotor kept = null;
            var toRemove = new List<GameObject>();
            for (var i = 0; i < playersRoot.childCount; i++)
            {
                var child = playersRoot.GetChild(i).gameObject;
                if (child.name == PlayerObjectName)
                {
                    if (kept == null)
                        kept = child.GetComponent<PlayerMotor>() ?? child.AddComponent<PlayerMotor>();
                    else
                        toRemove.Add(child);
                }
            }

            foreach (var go in toRemove)
                Object.DestroyImmediate(go);

            if (kept == null)
            {
                var player = GameObject.CreatePrimitive(PrimitiveType.Cube);
                player.name = PlayerObjectName;
                player.transform.SetParent(playersRoot, false);
                player.transform.localPosition = new Vector3(0f, 0.5f, -3f);
                player.transform.localRotation = Quaternion.identity;
                player.transform.localScale = new Vector3(0.8f, 1f, 0.8f);

                var rb = player.AddComponent<Rigidbody>();
                rb.mass = 75f;
                rb.useGravity = true;
                rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

                var renderer = player.GetComponent<MeshRenderer>();
                if (renderer != null && playerMat != null)
                    renderer.sharedMaterial = playerMat;

                kept = player.AddComponent<PlayerMotor>();
                kept.SetMovementConfig(movementConfig);
                var identity = player.AddComponent<PlayerIdentity>();
                identity.Configure(DefaultPlayerId, DefaultTeamId);
            }
            else
            {
                kept.transform.localPosition = new Vector3(0f, 0.5f, -3f);
                kept.transform.localRotation = Quaternion.identity;
                kept.transform.localScale = new Vector3(0.8f, 1f, 0.8f);

                if (kept.GetComponent<Rigidbody>() == null)
                {
                    var rb = kept.gameObject.AddComponent<Rigidbody>();
                    rb.mass = 75f;
                }

                var identity = kept.GetComponent<PlayerIdentity>() ?? kept.gameObject.AddComponent<PlayerIdentity>();
                identity.Configure(DefaultPlayerId, DefaultTeamId);
                kept.SetMovementConfig(movementConfig);
            }

            return kept;
        }

        static ArenaController EnsureArenaController(GameObject root, ArenaRoot arenaRoot,
            MovementConfig movementConfig, PlayerMotor playerMotor)
        {
            var controller = root.GetComponent<ArenaController>() ?? root.AddComponent<ArenaController>();
            var motors = new List<PlayerMotor>();
            if (playerMotor != null)
                motors.Add(playerMotor);

            // Nie zeruj referencji G3 (KickConfig) — G2 aktualizuje tylko ruch i listę zawodników.
            var kickConfig = controller.KickConfig;
            var roundConfig = controller.RoundConfig;
            var scenario = controller.DefaultScenarioAsset;
            var target = controller.ExerciseTargetComponent;
            controller.SetReferences(arenaRoot, movementConfig, kickConfig, roundConfig, scenario, target, motors);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static bool PatchPlaygroundScene(MovementConfig movementConfig)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("G2: nie można otworzyć sceny " + ScenePath);
                return false;
            }

            var arenaGo = GameObject.Find("Arena");
            if (arenaGo == null)
            {
                Debug.LogError("G2: brak instancji Arena na scenie — uruchom G1.");
                return false;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("G2: brak prefabu areny.");
                return false;
            }

            if (PrefabUtility.IsPartOfPrefabInstance(arenaGo))
            {
                PrefabUtility.RevertPrefabInstance(arenaGo, InteractionMode.AutomatedAction);
            }
            else
            {
                Object.DestroyImmediate(arenaGo);
                arenaGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                arenaGo.name = "Arena";
                arenaGo.transform.position = Vector3.zero;
                arenaGo.transform.rotation = Quaternion.identity;
                arenaGo.transform.localScale = Vector3.one;
            }

            var controller = arenaGo.GetComponent<ArenaController>();
            if (controller == null)
            {
                Debug.LogError("G2: Arena bez ArenaController po odświeżeniu prefabu.");
                return false;
            }

            EnsureSceneInputAndHud(arenaGo, controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return true;
        }

        static void EnsureSceneInputAndHud(GameObject arenaGo, ArenaController controller)
        {
            var input = Object.FindFirstObjectByType<KeyboardCommandSource>();
            if (input == null)
            {
                var go = new GameObject("KeyboardCommandSource");
                input = go.AddComponent<KeyboardCommandSource>();
            }

            input.Configure(controller, DefaultPlayerId);

            var hud = Object.FindFirstObjectByType<ArenaDebugHUD>();
            if (hud == null)
            {
                var go = new GameObject("ArenaDebugHUD");
                hud = go.AddComponent<ArenaDebugHUD>();
            }

            hud.Configure(controller, input, DefaultPlayerId);
        }
    }
}
#endif
