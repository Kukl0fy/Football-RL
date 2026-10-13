#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FootballRL.Contracts;
using FootballRL.Gameplay;
using FootballRL.Scenarios;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FootballRL.Editor
{
    public static class G6Verification
    {
        const string PrefabPath = "Assets/_Project/Prefabs/Arena.prefab";
        const string ManualSandboxPath = "Assets/_Project/Configs/Scenarios/ManualSandbox.asset";
        const string KickConfigGuid = "60d36d1b36bc02f4aa276029e6021cee";
        const float Dt = 0.02f;
        const float Epsilon = 0.08f;
        const int PlayerId = 0;
        const float BenchmarkWallSeconds = 3f;

        public static void RunBatch()
        {
            if (!G6ArenaBuilder.RunBuildSilent())
            {
                UnityEngine.Debug.LogError("G6 verify: build failed.");
                EditorApplication.Exit(1);
                return;
            }

            if (!RunAllTests())
            {
                UnityEngine.Debug.LogError("G6 verify: tests failed.");
                EditorApplication.Exit(1);
                return;
            }

            UnityEngine.Debug.Log("G6 verification completed successfully.");
            EditorApplication.Exit(0);
        }

        public static bool RunAllTests()
        {
            if (!TestT22_NoMlAgentsPackage())
                return false;

            if (!G2Verification.RunAllTests())
                return false;
            if (!G3Verification.RunAllTests())
                return false;
            if (!G4Verification.RunAllTests())
                return false;
            if (!G5Verification.RunAllTests())
                return false;

            if (!TestT18_DualArenaIndependence())
                return false;
            if (!TestT19_RotatedArenaLocalAndGoal())
                return false;
            if (!TestT20_FastBallTowardNeighbor())
                return false;

            RunPerformanceBenchmarks();

            if (!VerifyKickConfigGuidOnPrefab())
                return false;

            TryBuildPlayer();
            return true;
        }

        static bool TestT22_NoMlAgentsPackage()
        {
            var manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
            var text = File.ReadAllText(manifestPath);
            if (text.IndexOf("ml-agents", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("com.unity.ml-agents", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                UnityEngine.Debug.LogError("G6 T22: manifest zawiera ML-Agents.");
                return false;
            }

            UnityEngine.Debug.Log("G6 T22 OK: brak ML-Agents w Packages/manifest.json.");
            return true;
        }

        static bool VerifyKickConfigGuidOnPrefab()
        {
            var yaml = File.ReadAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "..", PrefabPath)));
            if (!yaml.Contains(KickConfigGuid))
            {
                UnityEngine.Debug.LogError($"G6: KickConfig guid {KickConfigGuid} nie znaleziony w prefabie Arena.");
                return false;
            }

            UnityEngine.Debug.Log($"G6 KickConfig guid OK ({KickConfigGuid}).");
            return true;
        }

        static ScenarioDefinition LoadManualSandbox() =>
            AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(ManualSandboxPath);

        static GameObject InstantiateArenaInIsolatedPhysicsScene(
            GameObject prefab,
            string objectName,
            Vector3 worldPosition,
            Quaternion worldRotation)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.name = objectName;
            go.transform.SetPositionAndRotation(worldPosition, worldRotation);
            Physics.SyncTransforms();
            var physicsScene = scene.GetPhysicsScene();
            if (physicsScene.IsValid())
                physicsScene.Simulate(0f);
            return go;
        }

        static (ArenaController a, ArenaController b) CreateDualArenaScene(float separationX)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var goA = InstantiateArenaInIsolatedPhysicsScene(prefab, "Arena_A", Vector3.zero, Quaternion.identity);
            var goB = InstantiateArenaInIsolatedPhysicsScene(
                prefab,
                "Arena_B",
                new Vector3(separationX, 0f, 0f),
                Quaternion.identity);

            var a = goA.GetComponent<ArenaController>();
            var b = goB.GetComponent<ArenaController>();
            a.EditorPrepareForBatchVerification();
            b.EditorPrepareForBatchVerification();
            a.EditorDisableAutoReset();
            b.EditorDisableAutoReset();
            return (a, b);
        }

        static void PrepareSimulationMode(out SimulationMode previous)
        {
            previous = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
        }

        static void RestoreSimulationMode(SimulationMode previous) => Physics.simulationMode = previous;

        static bool TestT18_DualArenaIndependence()
        {
            var manual = LoadManualSandbox();
            if (manual == null)
            {
                UnityEngine.Debug.LogError("G6 T18: brak ManualSandbox.");
                return false;
            }

            PrepareSimulationMode(out var prevSim);
            try
            {
                var (a, b) = CreateDualArenaScene(ArenaFactory.DefaultSeparationMeters);
                if (!a.TryResetEpisode(ResetEpisodeRequest.ForScenario(manual, 11), out var errA))
                {
                    UnityEngine.Debug.LogError($"G6 T18: reset A failed: {errA}");
                    return false;
                }

                if (!b.TryResetEpisode(ResetEpisodeRequest.ForScenario(manual, 22), out var errB))
                {
                    UnityEngine.Debug.LogError($"G6 T18: reset B failed: {errB}");
                    return false;
                }

                var list = new List<ArenaController> { a, b };
                var zero = PlayerCommand.Zero;
                a.SubmitCommand(PlayerId, zero, 1);
                b.SubmitCommand(PlayerId, zero, 1);
                for (var i = 0; i < 40; i++)
                    ArenaController.StepManyWithSharedSimulation(list, Dt);

                var snapB0 = b.CaptureSnapshot();
                var episodeB0 = b.EpisodeId;
                var phaseB0 = b.Phase;

                a.SubmitCommand(PlayerId, new PlayerCommand { Forward = 1f }, 100);
                b.SubmitCommand(PlayerId, zero, 101);
                for (var i = 0; i < 15; i++)
                    ArenaController.StepManyWithSharedSimulation(list, Dt);

                var snapBAfterSteps = b.CaptureSnapshot();
                if (Vector3.Distance(snapB0.Ball.LocalPosition, snapBAfterSteps.Ball.LocalPosition) > Epsilon ||
                    Vector3.Distance(snapB0.Players[0].LocalPosition, snapBAfterSteps.Players[0].LocalPosition) >
                    Epsilon)
                {
                    UnityEngine.Debug.LogError("G6 T18: ruch na A nie powinien przesuwać B (osobna arena).");
                    return false;
                }

                if (!a.TryResetEpisode(ResetEpisodeRequest.ForScenario(manual, 33), out var errReset))
                {
                    UnityEngine.Debug.LogError($"G6 T18: reset tylko A failed: {errReset}");
                    return false;
                }

                var snapB1 = b.CaptureSnapshot();
                if (b.EpisodeId != episodeB0)
                {
                    UnityEngine.Debug.LogError($"G6 T18: EpisodeId B zmienił się ({episodeB0} → {b.EpisodeId}).");
                    return false;
                }

                if (b.Phase != phaseB0)
                {
                    UnityEngine.Debug.LogError($"G6 T18: faza B zmieniła się ({phaseB0} → {b.Phase}).");
                    return false;
                }

                if (Vector3.Distance(snapBAfterSteps.Ball.LocalPosition, snapB1.Ball.LocalPosition) > Epsilon ||
                    Vector3.Distance(snapBAfterSteps.Players[0].LocalPosition, snapB1.Players[0].LocalPosition) >
                    Epsilon)
                {
                    UnityEngine.Debug.LogError("G6 T18: reset A nie powinien przesuwać B.");
                    return false;
                }

                UnityEngine.Debug.Log("G6 T18 OK: dwie areny niezależne.");
                return true;
            }
            finally
            {
                RestoreSimulationMode(prevSim);
            }
        }

        static bool TestT19_RotatedArenaLocalAndGoal()
        {
            var manual = LoadManualSandbox();
            if (manual == null)
                return false;

            PrepareSimulationMode(out var prevSim);
            try
            {
                var (controller, _) = CreateDualArenaScene(ArenaFactory.DefaultSeparationMeters);
                controller.TryResetEpisode(ResetEpisodeRequest.ForScenario(manual, 5), out _);

                var root = controller.ArenaRootTransform;
                var baselineBall = controller.CaptureSnapshot().Ball.LocalPosition;

                var originalPos = root.position;
                var originalRot = root.rotation;
                try
                {
                    root.SetPositionAndRotation(new Vector3(4f, 0f, -2f), Quaternion.Euler(0f, 40f, 0f));
                    Physics.SyncTransforms();
                    var afterMove = controller.CaptureSnapshot().Ball.LocalPosition;
                    if (Vector3.Distance(baselineBall, afterMove) > 0.15f)
                    {
                        UnityEngine.Debug.LogError($"G6 T19: lokalna piłka powinna być stabilna ({baselineBall} vs {afterMove}).");
                        return false;
                    }
                }
                finally
                {
                    root.SetPositionAndRotation(originalPos, originalRot);
                    Physics.SyncTransforms();
                }

                controller.EditorDisableAutoReset();
                controller.TryResetEpisode(ResetEpisodeRequest.ForScenario(manual, 6), out _);
                var ended = 0;
                void OnRoundEnded(RoundEndedEventArgs _) => ended++;
                controller.RoundEnded += OnRoundEnded;
                try
                {
                    controller.EditorEvaluateBallSegment(new Vector3(0f, 0.22f, 9.5f), new Vector3(0f, 0.22f, 10.5f), Dt);
                }
                finally
                {
                    controller.RoundEnded -= OnRoundEnded;
                }

                if (ended != 1 || controller.LastEndReason != RoundEndReason.Goal)
                {
                    UnityEngine.Debug.LogError($"G6 T19: gol po obrocie areny — ended={ended} reason={controller.LastEndReason}.");
                    return false;
                }

                UnityEngine.Debug.Log("G6 T19 OK: lokalność i bramka po przesunięciu/obrocie.");
                return true;
            }
            finally
            {
                RestoreSimulationMode(prevSim);
            }
        }

        static bool TestT20_FastBallTowardNeighbor()
        {
            var manual = LoadManualSandbox();
            if (manual == null)
                return false;

            PrepareSimulationMode(out var prevSim);
            try
            {
                var (a, b) = CreateDualArenaScene(ArenaFactory.DefaultSeparationMeters);
                a.TryResetEpisode(ResetEpisodeRequest.ForScenario(manual, 7), out _);
                b.TryResetEpisode(ResetEpisodeRequest.ForScenario(manual, 8), out _);

                var snapB0 = b.CaptureSnapshot();
                var playerB0 = snapB0.Players.Length > 0 ? snapB0.Players[0].LocalPosition : Vector3.zero;
                var ballB0 = snapB0.Ball.LocalPosition;

                a.EditorSetBallPosition(a.ArenaRootTransform.TransformPoint(new Vector3(0f, 0.22f, 0f)));
                a.EditorSetBallLinearVelocity(Vector3.right * 120f);

                var list = new List<ArenaController> { a, b };
                for (var i = 0; i < 200 && a.Phase == ArenaPhase.Running; i++)
                    ArenaController.StepManyWithSharedSimulation(list, Dt);

                if (a.Phase != ArenaPhase.Ended || a.LastEndReason != RoundEndReason.BallOut)
                {
                    UnityEngine.Debug.LogError($"G6 T20: arena A oczekiwano BallOut/Ended, jest {a.Phase} {a.LastEndReason}.");
                    return false;
                }

                var snapB1 = b.CaptureSnapshot();
                if (snapB1.Players.Length == 0)
                    return false;

                if (Vector3.Distance(playerB0, snapB1.Players[0].LocalPosition) > Epsilon ||
                    Vector3.Distance(ballB0, snapB1.Ball.LocalPosition) > Epsilon)
                {
                    UnityEngine.Debug.LogError("G6 T20: arena B nie powinna reagować na piłkę A.");
                    return false;
                }

                UnityEngine.Debug.Log("G6 T20 OK: BallOut na A, B nietknięta.");
                return true;
            }
            finally
            {
                RestoreSimulationMode(prevSim);
            }
        }

        static void RunPerformanceBenchmarks()
        {
            PrepareSimulationMode(out var prevSim);
            try
            {
                BenchmarkArenaCount(1);
                BenchmarkArenaCount(4);
                BenchmarkArenaCount(16);
            }
            finally
            {
                RestoreSimulationMode(prevSim);
            }
        }

        static void BenchmarkArenaCount(int count)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var manual = LoadManualSandbox();
            var controllers = new List<ArenaController>(count);
            var cols = Mathf.CeilToInt(Mathf.Sqrt(count));
            for (var i = 0; i < count; i++)
            {
                var x = (i % cols) * ArenaFactory.DefaultSeparationMeters;
                var z = (i / cols) * ArenaFactory.DefaultSeparationMeters;
                var go = InstantiateArenaInIsolatedPhysicsScene(
                    prefab,
                    $"Arena_{i}",
                    new Vector3(x, 0f, z),
                    Quaternion.identity);
                var c = go.GetComponent<ArenaController>();
                c.EditorPrepareForBatchVerification();
                c.EditorDisableAutoReset();
                if (manual != null)
                    c.TryResetEpisode(ResetEpisodeRequest.ForScenario(manual, 100 + i), out _);
                controllers.Add(c);
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            long steps = 0;
            while (sw.Elapsed.TotalSeconds < BenchmarkWallSeconds)
            {
                ArenaController.StepManyWithSharedSimulation(controllers, Dt);
                steps++;
            }

            var stepsPerSec = steps / Math.Max(0.001, sw.Elapsed.TotalSeconds);
            UnityEngine.Debug.Log(
                $"G6 perf: {count} arena(−y), Δt={Dt}s, {BenchmarkWallSeconds}s wall → {steps} kroków " +
                $"({stepsPerSec:F0} kroków/s, {stepsPerSec * Dt:F1} symulowanych s/s). " +
                $"Host: {SystemInfo.processorType}, {SystemInfo.systemMemorySize} MB RAM.");
        }

        static void TryBuildPlayer()
        {
            var buildDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "builds", "G6"));
            Directory.CreateDirectory(buildDir);
            var exePath = Path.Combine(buildDir, "FootballRL.exe");
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                UnityEngine.Debug.LogWarning("G6 build: brak scen w EditorBuildSettings.");
                return;
            }

            var report = BuildPipeline.BuildPlayer(
                scenes,
                exePath,
                BuildTarget.StandaloneWindows64,
                BuildOptions.None);

            if (report.summary.result == BuildResult.Succeeded)
                UnityEngine.Debug.Log($"G6 build OK: {exePath} ({report.summary.totalSize} bytes).");
            else
                UnityEngine.Debug.LogWarning($"G6 build: {report.summary.result} — {report.summary.totalErrors} errors.");
        }
    }
}
#endif
