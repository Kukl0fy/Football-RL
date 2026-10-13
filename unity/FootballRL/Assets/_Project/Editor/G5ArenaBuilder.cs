#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using FootballRL.Gameplay;
using FootballRL.Input;
using FootballRL.Presentation;
using FootballRL.Scenarios;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FootballRL.Editor
{
    public static class G5ArenaBuilder
    {
        const string ScenariosDir = "Assets/_Project/Configs/Scenarios";
        const string ManualSandboxPath = ScenariosDir + "/ManualSandbox.asset";
        const string MoveToTargetPath = ScenariosDir + "/MoveToTarget.asset";
        const string ShootAtEmptyGoalPath = ScenariosDir + "/ShootAtEmptyGoal.asset";
        const string ShootRandomPath = ScenariosDir + "/ShootAtEmptyGoal_RandomSpawn.asset";
        const string PrefabPath = "Assets/_Project/Prefabs/Arena.prefab";
        const string ScenePath = "Assets/_Project/Scenes/ManualPlayground.unity";
        const string TargetName = "ExerciseTarget";
        const int DefaultPlayerId = 0;

        public static void RunBatch()
        {
            if (!BuildAll())
            {
                Debug.LogError("G5 build failed.");
                EditorApplication.Exit(1);
                return;
            }

            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("G5 build completed successfully.");
            EditorApplication.Exit(0);
        }

        [MenuItem("FootballRL/G5/Ensure Scenario Setup")]
        public static void RunFromMenu() => BuildAll();

        [MenuItem("FootballRL/G5/Apply ManualSandbox To Playground")]
        public static void ApplyManualSandbox() => ApplyScenarioToScene(LoadScenario(ManualSandboxPath));

        [MenuItem("FootballRL/G5/Apply MoveToTarget To Playground")]
        public static void ApplyMoveToTarget() => ApplyScenarioToScene(LoadScenario(MoveToTargetPath));

        [MenuItem("FootballRL/G5/Apply ShootAtEmptyGoal To Playground")]
        public static void ApplyShootScenario() => ApplyScenarioToScene(LoadScenario(ShootAtEmptyGoalPath));

        public static bool RunBuildSilent() => BuildAll();

        static bool BuildAll()
        {
            if (!G4ArenaBuilder.RunBuildSilent())
            {
                Debug.LogError("G5: wymagany G4.");
                return false;
            }

            if (!Directory.Exists(ScenariosDir))
                Directory.CreateDirectory(ScenariosDir);

            var manual = EnsureManualSandbox();
            EnsureMoveToTarget();
            EnsureShootAtEmptyGoal();
            EnsureShootRandomSpawn();

            if (!PatchArenaPrefab(manual))
                return false;

            return PatchPlaygroundScene(manual);
        }

        static ScenarioDefinition EnsureManualSandbox()
        {
            var s = LoadOrCreate(ManualSandboxPath);
            s.scenarioId = "ManualSandbox";
            s.kind = ScenarioKind.ManualSandbox;
            s.kickingEnabled = true;
            s.regulateKickAngle = true;
            s.useRandomSpawn = false;
            s.roundTimeLimitSeconds = -1f;
            EditorUtility.SetDirty(s);
            return s;
        }

        static ScenarioDefinition EnsureMoveToTarget()
        {
            var s = LoadOrCreate(MoveToTargetPath);
            s.scenarioId = "MoveToTarget";
            s.kind = ScenarioKind.MoveToTarget;
            s.kickingEnabled = true;
            s.regulateKickAngle = true;
            s.useRandomSpawn = true;
            s.roundTimeLimitSeconds = -1f;
            EditorUtility.SetDirty(s);
            return s;
        }

        static ScenarioDefinition EnsureShootAtEmptyGoal()
        {
            var s = LoadOrCreate(ShootAtEmptyGoalPath);
            s.scenarioId = "ShootAtEmptyGoal";
            s.kind = ScenarioKind.ShootAtEmptyGoal;
            s.kickingEnabled = true;
            s.regulateKickAngle = false;
            s.lockedLoft = -1f;
            s.useRandomSpawn = false;
            s.easyPlayerLocalPosition = new Vector3(0f, 0.5f, -4f);
            s.easyBallLocalPosition = new Vector3(0f, 0.22f, -3.15f);
            s.easyPlayerYawDeg = 0f;
            EditorUtility.SetDirty(s);
            return s;
        }

        static void EnsureShootRandomSpawn()
        {
            var s = LoadOrCreate(ShootRandomPath);
            s.scenarioId = "ShootAtEmptyGoal_RandomSpawn";
            s.kind = ScenarioKind.ShootAtEmptyGoal;
            s.kickingEnabled = true;
            s.regulateKickAngle = false;
            s.lockedLoft = -1f;
            s.useRandomSpawn = true;
            EditorUtility.SetDirty(s);
        }

        static ScenarioDefinition LoadOrCreate(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(path);
            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance<ScenarioDefinition>();
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        static ScenarioDefinition LoadScenario(string path) =>
            AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(path);

        static bool PatchArenaPrefab(ScenarioDefinition manualSandbox)
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

                var target = EnsureExerciseTarget(prefabRoot.transform);
                target.SetActiveForScenario(false);

                PlayerMotor playerMotor = null;
                var playersRoot = arenaRoot.PlayersRoot;
                if (playersRoot != null)
                {
                    foreach (Transform child in playersRoot)
                    {
                        if (child.name == "Player_0")
                            playerMotor = child.GetComponent<PlayerMotor>();
                    }
                }

                if (playerMotor == null)
                    return false;

                var motors = new List<PlayerMotor> { playerMotor };
                var kickConfig = controller.KickConfig;
                var roundConfig = controller.RoundConfig;
                controller.SetReferences(
                    arenaRoot,
                    controller.MovementConfig,
                    kickConfig,
                    roundConfig,
                    manualSandbox,
                    target,
                    motors);

                var so = new SerializedObject(controller);
                so.FindProperty("defaultScenario").objectReferenceValue = manualSandbox;
                so.FindProperty("exerciseTarget").objectReferenceValue = target;
                so.ApplyModifiedPropertiesWithoutUndo();

                var spawnState = prefabRoot.GetComponent<ArenaEpisodeSpawnState>();
                spawnState?.CaptureFromScene(arenaRoot.BallBody?.transform, motors.ToArray());

                EditorUtility.SetDirty(controller);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        static ExerciseTarget EnsureExerciseTarget(Transform arenaTransform)
        {
            var existing = arenaTransform.Find(TargetName);
            if (existing != null)
                return existing.GetComponent<ExerciseTarget>() ??
                       existing.gameObject.AddComponent<ExerciseTarget>();

            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = TargetName;
            go.transform.SetParent(arenaTransform, false);
            go.transform.localScale = new Vector3(1.2f, 0.05f, 1.2f);
            go.transform.localPosition = new Vector3(0f, 0.05f, 3f);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            return go.AddComponent<ExerciseTarget>();
        }

        static bool PatchPlaygroundScene(ScenarioDefinition manualSandbox)
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

            ApplyScenarioToScene(manualSandbox, scene, arenaGo);
            return controller != null;
        }

        static void ApplyScenarioToScene(ScenarioDefinition scenario)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var arenaGo = GameObject.Find("Arena");
            if (arenaGo == null || scenario == null)
                return;

            ApplyScenarioToScene(scenario, scene, arenaGo);
        }

        static void ApplyScenarioToScene(ScenarioDefinition scenario, Scene scene, GameObject arenaGo)
        {
            var controller = arenaGo.GetComponent<ArenaController>();
            if (controller == null)
                return;

            var so = new SerializedObject(controller);
            so.FindProperty("defaultScenario").objectReferenceValue = scenario;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
#endif
