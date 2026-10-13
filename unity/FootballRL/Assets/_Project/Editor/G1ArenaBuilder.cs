#if UNITY_EDITOR
using System.IO;
using FootballRL.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FootballRL.Editor
{
    /// <summary>
    /// G1: prefab areny, konfiguracje, aktualizacja ManualPlayground. Idempotentne.
    /// Menu: FootballRL/G1/Build Arena And Update Playground
    /// Batchmode: -executeMethod FootballRL.Editor.G1ArenaBuilder.RunBatch
    /// </summary>
    public static class G1ArenaBuilder
    {
        const string ArenaConfigPath = "Assets/_Project/Configs/ArenaConfig.asset";
        const string BallPhysicsConfigPath = "Assets/_Project/Configs/BallPhysicsConfig.asset";
        const string PrefabPath = "Assets/_Project/Prefabs/Arena.prefab";
        const string ScenePath = "Assets/_Project/Scenes/ManualPlayground.unity";
        const string PhysMatDir = "Assets/_Project/Configs/PhysicsMaterials";
        const string MatDir = "Assets/_Project/Configs/Materials";

        public static void RunBatch()
        {
            if (!BuildAll())
            {
                Debug.LogError("G1 arena build failed.");
                EditorApplication.Exit(1);
                return;
            }

            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("G1 arena build completed successfully.");
            EditorApplication.Exit(0);
        }

        [MenuItem("FootballRL/G1/Build Arena And Update Playground")]
        public static void RunFromMenu()
        {
            if (BuildAll())
                EditorSceneManager.OpenScene(ScenePath);
        }

        /// <summary>Bez otwierania sceny na końcu — do testów batch.</summary>
        public static bool RunBuildSilent() => BuildAll();

        static bool BuildAll()
        {
            EnsureDirectories();
            var arenaConfig = LoadOrCreateArenaConfig();
            var ballConfig = LoadOrCreateBallPhysicsConfig();
            var physMats = EnsurePhysicsMaterials(ballConfig);
            var mats = EnsureVisualMaterials(arenaConfig);
            var prefab = BuildArenaPrefab(arenaConfig, ballConfig, physMats, mats);
            if (prefab == null)
                return false;
            return UpdatePlaygroundScene(prefab, arenaConfig);
        }

        static void EnsureDirectories()
        {
            foreach (var dir in new[]
                     {
                         "Assets/_Project/Configs",
                         PhysMatDir,
                         MatDir,
                         "Assets/_Project/Prefabs",
                         "Assets/_Project/Scripts/Gameplay"
                     })
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                    AssetDatabase.Refresh();
                }
            }
        }

        static ArenaConfig LoadOrCreateArenaConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<ArenaConfig>(ArenaConfigPath);
            if (cfg != null)
                return cfg;

            cfg = ScriptableObject.CreateInstance<ArenaConfig>();
            AssetDatabase.CreateAsset(cfg, ArenaConfigPath);
            AssetDatabase.SaveAssets();
            return cfg;
        }

        static BallPhysicsConfig LoadOrCreateBallPhysicsConfig()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<BallPhysicsConfig>(BallPhysicsConfigPath);
            if (cfg != null)
                return cfg;

            cfg = ScriptableObject.CreateInstance<BallPhysicsConfig>();
            AssetDatabase.CreateAsset(cfg, BallPhysicsConfigPath);
            AssetDatabase.SaveAssets();
            return cfg;
        }

        struct PhysicsMaterialSet
        {
            public PhysicsMaterial Ball;
            public PhysicsMaterial Pitch;
            public PhysicsMaterial Wall;
            public PhysicsMaterial GoalFrame;
        }

        static PhysicsMaterialSet EnsurePhysicsMaterials(BallPhysicsConfig cfg)
        {
            return new PhysicsMaterialSet
            {
                Ball = GetOrCreatePhysicsMaterial($"{PhysMatDir}/Ball.physicMaterial", cfg.ballDynamicFriction,
                    cfg.ballStaticFriction, cfg.ballBounciness, cfg.ballFrictionCombine, cfg.ballBounceCombine),
                Pitch = GetOrCreatePhysicsMaterial($"{PhysMatDir}/Pitch.physicMaterial", cfg.pitchDynamicFriction,
                    cfg.pitchStaticFriction, cfg.pitchBounciness),
                Wall = GetOrCreatePhysicsMaterial($"{PhysMatDir}/Wall.physicMaterial", cfg.wallDynamicFriction,
                    cfg.wallStaticFriction, cfg.wallBounciness),
                GoalFrame = GetOrCreatePhysicsMaterial($"{PhysMatDir}/GoalFrame.physicMaterial",
                    cfg.goalFrameDynamicFriction, cfg.goalFrameStaticFriction, cfg.goalFrameBounciness)
            };
        }

        static PhysicsMaterial GetOrCreatePhysicsMaterial(string path, float dynFriction, float statFriction,
            float bounciness, PhysicsMaterialCombine frictionCombine = PhysicsMaterialCombine.Average,
            PhysicsMaterialCombine bounceCombine = PhysicsMaterialCombine.Average)
        {
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (mat == null)
            {
                mat = new PhysicsMaterial(Path.GetFileNameWithoutExtension(path))
                {
                    dynamicFriction = dynFriction,
                    staticFriction = statFriction,
                    bounciness = bounciness,
                    frictionCombine = frictionCombine,
                    bounceCombine = bounceCombine
                };
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.dynamicFriction = dynFriction;
                mat.staticFriction = statFriction;
                mat.bounciness = bounciness;
                mat.frictionCombine = frictionCombine;
                mat.bounceCombine = bounceCombine;
                EditorUtility.SetDirty(mat);
            }

            return mat;
        }

        struct VisualMaterialSet
        {
            public Material Pitch;
            public Material Wall;
            public Material GoalFrame;
            public Material Ball;
        }

        static VisualMaterialSet EnsureVisualMaterials(ArenaConfig cfg)
        {
            return new VisualMaterialSet
            {
                Pitch = GetOrCreateColorMaterial($"{MatDir}/Pitch.mat", cfg.pitchColor),
                Wall = GetOrCreateColorMaterial($"{MatDir}/Wall.mat", cfg.wallColor),
                GoalFrame = GetOrCreateColorMaterial($"{MatDir}/GoalFrame.mat", cfg.goalFrameColor),
                Ball = GetOrCreateColorMaterial($"{MatDir}/Ball.mat", cfg.ballColor)
            };
        }

        static Material GetOrCreateColorMaterial(string path, Color color)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                    shader = Shader.Find("Standard");
                mat = new Material(shader) { color = color };
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.color = color;
                EditorUtility.SetDirty(mat);
            }

            return mat;
        }

        static GameObject BuildArenaPrefab(ArenaConfig cfg, BallPhysicsConfig ballCfg, PhysicsMaterialSet phys,
            VisualMaterialSet visuals)
        {
            var root = new GameObject("Arena");
            var arenaRoot = root.AddComponent<ArenaRoot>();

            var pitch = new GameObject("Pitch");
            pitch.transform.SetParent(root.transform, false);
            BuildPitch(pitch, cfg, phys.Pitch, visuals.Pitch);

            var walls = new GameObject("Walls");
            walls.transform.SetParent(root.transform, false);
            BuildWalls(walls.transform, cfg, phys.Wall, visuals.Wall);

            var goals = new GameObject("Goals");
            goals.transform.SetParent(root.transform, false);
            BuildGoals(goals.transform, cfg, phys, visuals);

            var emergency = new GameObject("EmergencyFloorBoundary");
            emergency.transform.SetParent(root.transform, false);
            BuildEmergencyFloor(emergency, cfg);

            var players = new GameObject("Players");
            players.transform.SetParent(root.transform, false);

            var obstacles = new GameObject("Obstacles");
            obstacles.transform.SetParent(root.transform, false);

            var ball = BuildBall(root.transform, cfg, ballCfg, phys.Ball, visuals.Ball);

            arenaRoot.SetReferences(cfg, players.transform, obstacles.transform, ball.body, ball.transform);

            var prefab = SaveOrReplacePrefab(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            return prefab;
        }

        static void BuildPitch(GameObject pitch, ArenaConfig cfg, PhysicsMaterial physMat, Material visual)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(pitch.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            floor.transform.localScale = new Vector3(cfg.pitchWidthX, 0.1f, cfg.pitchPlayLengthZ);
            ApplyStaticCollider(floor, physMat, visual);
        }

        static void BuildWalls(Transform parent, ArenaConfig cfg, PhysicsMaterial physMat, Material visual)
        {
            float hx = cfg.HalfWidthX;
            float hz = cfg.HalfPlayLengthZ;
            float t = cfg.wallThickness;
            float h = cfg.wallHeight;
            float halfH = h * 0.5f;
            float gw = cfg.HalfGoalWidth;

            // Długie boki (±X, 20 m wzdłuż Z) — pełne ściany, bez bramek.
            CreateWallSegment(parent, "Wall_PosX", new Vector3(hx + t * 0.5f, halfH, 0f),
                new Vector3(t, h, cfg.pitchPlayLengthZ), physMat, visual);
            CreateWallSegment(parent, "Wall_NegX", new Vector3(-hx - t * 0.5f, halfH, 0f),
                new Vector3(t, h, cfg.pitchPlayLengthZ), physMat, visual);

            BuildEndWallWithOpening(parent, cfg, physMat, visual, positiveZ: true, hx, hz, t, h, halfH, gw);
            BuildEndWallWithOpening(parent, cfg, physMat, visual, positiveZ: false, hx, hz, t, h, halfH, gw);
        }

        static void BuildEndWallWithOpening(Transform parent, ArenaConfig cfg, PhysicsMaterial physMat,
            Material visual, bool positiveZ, float hx, float hz, float t, float h, float halfH, float gw)
        {
            float sign = positiveZ ? 1f : -1f;
            string suffix = positiveZ ? "PosZ" : "NegZ";
            float wallCenterZ = sign * hz + sign * (t * 0.5f);

            float sideSegX = hx - gw;
            if (sideSegX > 0.01f)
            {
                CreateWallSegment(parent, $"Wall_{suffix}_SidePosX",
                    new Vector3(gw + sideSegX * 0.5f, halfH, wallCenterZ),
                    new Vector3(sideSegX, h, t), physMat, visual);
                CreateWallSegment(parent, $"Wall_{suffix}_SideNegX",
                    new Vector3(-gw - sideSegX * 0.5f, halfH, wallCenterZ),
                    new Vector3(sideSegX, h, t), physMat, visual);
            }

            float lintelHeight = h - cfg.goalHeight;
            if (lintelHeight > 0.01f)
            {
                CreateWallSegment(parent, $"Wall_{suffix}_Lintel",
                    new Vector3(0f, cfg.goalHeight + lintelHeight * 0.5f, wallCenterZ),
                    new Vector3(cfg.goalWidth, lintelHeight, t), physMat, visual);
            }
        }

        static void BuildGoals(Transform parent, ArenaConfig cfg, PhysicsMaterialSet phys, VisualMaterialSet visuals)
        {
            BuildSingleGoal(parent, cfg, phys.GoalFrame, phys.Pitch, visuals.GoalFrame, visuals.Pitch,
                positiveZ: true);
            BuildSingleGoal(parent, cfg, phys.GoalFrame, phys.Pitch, visuals.GoalFrame, visuals.Pitch,
                positiveZ: false);
        }

        static void BuildSingleGoal(Transform parent, ArenaConfig cfg, PhysicsMaterial framePhys,
            PhysicsMaterial pitchPhys, Material frameVisual, Material pitchVisual, bool positiveZ)
        {
            string suffix = positiveZ ? "PosZ" : "NegZ";
            var goalRoot = new GameObject($"Goal_{suffix}");
            goalRoot.transform.SetParent(parent, false);

            float hz = cfg.HalfPlayLengthZ;
            float gd = cfg.goalDepth;
            float t = cfg.wallThickness;
            float sign = positiveZ ? 1f : -1f;
            float goalLineZ = sign * hz;
            float postR = 0.06f;
            float gw = cfg.HalfGoalWidth;
            float gh = cfg.goalHeight;
            float frameZ = goalLineZ + sign * (t * 0.5f);

            // Rama (słupki + poprzeczka) w płaszczyźnie linii bramkowej — wlot z boiska.
            CreateGoalPost(goalRoot.transform, "Post_PosX", new Vector3(gw, gh * 0.5f, frameZ),
                new Vector3(postR * 2f, gh, postR * 2f), framePhys, frameVisual);
            CreateGoalPost(goalRoot.transform, "Post_NegX", new Vector3(-gw, gh * 0.5f, frameZ),
                new Vector3(postR * 2f, gh, postR * 2f), framePhys, frameVisual);
            CreateGoalPost(goalRoot.transform, "Crossbar", new Vector3(0f, gh, frameZ),
                new Vector3(cfg.goalWidth + postR * 2f, postR * 2f, postR * 2f), framePhys, frameVisual);

            float boxCenterZ = goalLineZ + sign * (gd * 0.5f);
            float backZ = goalLineZ + sign * (gd + t * 0.5f);

            // Boki pudła (głębokość 1 m za linią bramkową).
            CreateGoalPost(goalRoot.transform, "BoxSide_PosX", new Vector3(gw, gh * 0.5f, boxCenterZ),
                new Vector3(t, gh, gd), framePhys, frameVisual);
            CreateGoalPost(goalRoot.transform, "BoxSide_NegX", new Vector3(-gw, gh * 0.5f, boxCenterZ),
                new Vector3(t, gh, gd), framePhys, frameVisual);

            // Pełna tylna ściana — bez wylotu.
            CreateGoalPost(goalRoot.transform, "BackWall", new Vector3(0f, gh * 0.5f, backZ),
                new Vector3(cfg.goalWidth, gh, t), framePhys, frameVisual);

            // Podłoga tylko pod bramką, od linii bramkowej — materiał boiska, bez nachodzenia na Floor boiska.
            var goalFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            goalFloor.name = "GoalFloor";
            goalFloor.transform.SetParent(goalRoot.transform, false);
            goalFloor.transform.localPosition = new Vector3(0f, -0.05f, boxCenterZ);
            goalFloor.transform.localScale = new Vector3(cfg.goalWidth, 0.1f, gd);
            ApplyStaticCollider(goalFloor, pitchPhys, pitchVisual);
        }

        static void BuildEmergencyFloor(GameObject go, ArenaConfig cfg)
        {
            float totalZ = cfg.pitchPlayLengthZ + 2f * cfg.goalDepth;
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, cfg.emergencyFloorY, 0f);
            box.size = new Vector3(cfg.pitchWidthX + 4f, cfg.emergencyFloorDepth, totalZ + 4f);
        }

        static (Rigidbody body, Transform transform) BuildBall(Transform parent, ArenaConfig cfg,
            BallPhysicsConfig ballCfg, PhysicsMaterial ballPhys, Material ballVisual)
        {
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Ball";
            ball.transform.SetParent(parent, false);
            float y = cfg.ballRadius + 0.05f;
            ball.transform.localPosition = new Vector3(0f, y, 0f);
            ball.transform.localScale = Vector3.one * (cfg.ballRadius * 2f);

            var col = ball.GetComponent<SphereCollider>();
            col.sharedMaterial = ballPhys;

            var rb = ball.AddComponent<Rigidbody>();
            var setup = ball.AddComponent<BallPhysicsSetup>();
            setup.Configure(ballCfg, ballPhys);

            var renderer = ball.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = ballVisual;

            return (rb, ball.transform);
        }

        static void CreateWallSegment(Transform parent, string name, Vector3 localPos, Vector3 scale,
            PhysicsMaterial phys, Material visual)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent, false);
            wall.transform.localPosition = localPos;
            wall.transform.localScale = scale;
            ApplyStaticCollider(wall, phys, visual);
        }

        static void CreateGoalPost(Transform parent, string name, Vector3 localPos, Vector3 scale,
            PhysicsMaterial phys, Material visual)
        {
            CreateWallSegment(parent, name, localPos, scale, phys, visual);
        }

        static void ApplyStaticCollider(GameObject go, PhysicsMaterial phys, Material visual)
        {
            var col = go.GetComponent<Collider>();
            if (col != null)
                col.sharedMaterial = phys;
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
                renderer.sharedMaterial = visual;
        }

        static GameObject SaveOrReplacePrefab(GameObject tempRoot, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                PrefabUtility.SaveAsPrefabAsset(tempRoot, path);
            else
                PrefabUtility.SaveAsPrefabAsset(tempRoot, path);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        static bool UpdatePlaygroundScene(GameObject arenaPrefab, ArenaConfig cfg)
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("G1: nie można otworzyć sceny " + ScenePath);
                return false;
            }

            var ground = GameObject.Find("Ground");
            if (ground != null)
                Object.DestroyImmediate(ground);

            var existingArena = GameObject.Find("Arena");
            if (existingArena != null)
                Object.DestroyImmediate(existingArena);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(arenaPrefab, scene);
            instance.name = "Arena";
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            SetupMainCamera(cfg);
            EnsureDirectionalLight();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return true;
        }

        static void SetupMainCamera(ArenaConfig cfg)
        {
            var cam = Camera.main;
            if (cam == null)
                return;

            float totalZ = cfg.pitchPlayLengthZ + 2f * cfg.goalDepth;
            float extent = Mathf.Max(cfg.pitchWidthX, totalZ);
            float dist = extent * 0.85f;
            cam.transform.position = new Vector3(0f, dist * 0.95f, -dist * 0.75f);
            cam.transform.rotation = Quaternion.Euler(42f, 0f, 0f);
            cam.fieldOfView = 55f;
        }

        static void EnsureDirectionalLight()
        {
            var light = Object.FindFirstObjectByType<Light>();
            if (light != null && light.type == LightType.Directional)
                light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }
    }
}
#endif
