#if UNITY_EDITOR
using FootballRL.Contracts;
using FootballRL.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FootballRL.Editor
{
    public static class G4Verification
    {
        const string ScenePath = "Assets/_Project/Scenes/ManualPlayground.unity";
        const string ArenaConfigAssetPath = "Assets/_Project/Configs/ArenaConfig.asset";
        const float Dt = 0.02f;

        public static void RunBatch()
        {
            if (!G4ArenaBuilder.RunBuildSilent())
            {
                Debug.LogError("G4 verify: build failed.");
                EditorApplication.Exit(1);
                return;
            }

            if (!RunAllTests())
            {
                Debug.LogError("G4 verify: tests failed.");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("G4 verification completed successfully.");
            EditorApplication.Exit(0);
        }

        public static bool RunAllTests()
        {
            var cfg = LoadArenaConfigForTests();

            if (!RunBallBoundsUnitTests(cfg))
                return false;

            if (!RunGoalDetectorUnitTests(cfg))
                return false;

            return RunArenaRoundTests();
        }

        static ArenaConfig CreateDefaultArenaConfig() => ScriptableObject.CreateInstance<ArenaConfig>();

        static ArenaConfig LoadArenaConfigForTests()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ArenaConfig>(ArenaConfigAssetPath);
            return asset != null ? asset : CreateDefaultArenaConfig();
        }

        static float BallY(ArenaConfig cfg) => cfg.ballRadius;

        static void MovePrimaryPlayerAside(ArenaController controller)
        {
            var identity = UnityEngine.Object.FindAnyObjectByType<PlayerIdentity>();
            var motor = identity != null ? identity.GetComponent<PlayerMotor>() : null;
            if (motor == null)
                return;

            var root = controller.ArenaRootTransform;
            var away = root.TransformPoint(new Vector3(-6f, 0.5f, -4f));
            var rb = motor.GetComponent<Rigidbody>();
            rb.position = away;
            rb.rotation = root.rotation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.WakeUp();
            Physics.SyncTransforms();
        }

        /// <summary>Jeden krok batch: normalna ścieżka areny (Before → Simulate → After).</summary>
        static void StepArenaSimulationFrame(ArenaController controller, float deltaTime)
        {
            Physics.SyncTransforms();
            controller.StepArenaPhysicsWithRules(deltaTime);
        }

        static GoalDefinition[] BuildStandardGoals(ArenaConfig cfg)
        {
            var half = cfg.HalfPlayLengthZ;
            return new[]
            {
                new GoalDefinition { defendingTeamId = 1, goalLineZ = half, enterDirectionZ = 1f },
                new GoalDefinition { defendingTeamId = 0, goalLineZ = -half, enterDirectionZ = -1f }
            };
        }

        static (Vector3 prev, Vector3 curr) PositiveGoalCrossSegment(ArenaConfig cfg)
        {
            var half = cfg.HalfPlayLengthZ;
            var y = BallY(cfg);
            return (new Vector3(0f, y, half - 1f), new Vector3(0f, y, half + 0.5f));
        }

        static bool RunBallBoundsUnitTests(ArenaConfig cfg)
        {
            var r = cfg.ballRadius;
            var halfZ = cfg.HalfPlayLengthZ;
            var halfX = cfg.HalfWidthX;
            var halfGoalX = cfg.HalfGoalWidth;
            var y = BallY(cfg);

            if (ArenaBounds.IsBallOutOfPlay(new Vector3(halfGoalX - 0.2f, y, halfZ + 0.5f), r, cfg))
            {
                Debug.LogError("G4 bounds: x w otworze bramkowym nie powinno być BallOut.");
                return false;
            }

            if (!ArenaBounds.IsBallOutOfPlay(new Vector3(0f, cfg.goalHeight + 0.3f, halfZ + 0.5f), r, cfg))
            {
                Debug.LogError("G4 bounds: y za poprzeczką w bramce powinno być BallOut.");
                return false;
            }

            if (ArenaBounds.IsBallOutOfPlay(new Vector3(0f, cfg.goalHeight * 0.5f, halfZ + 0.5f), r, cfg))
            {
                Debug.LogError("G4 bounds: piłka w środku siatki nie powinna być BallOut.");
                return false;
            }

            if (!ArenaBounds.IsBallOutOfPlay(new Vector3(halfX + 0.5f, y, 0f), r, cfg))
            {
                Debug.LogError("G4 bounds: wyjście bokiem boiska nadal BallOut.");
                return false;
            }

            Debug.Log("G4 bounds unit OK.");
            return true;
        }

        static bool RunGoalDetectorUnitTests(ArenaConfig cfg)
        {
            var goals = BuildStandardGoals(cfg);
            var r = cfg.ballRadius;
            var halfW = cfg.HalfGoalWidth;
            var h = cfg.goalHeight;
            var halfZ = cfg.HalfPlayLengthZ;
            var y = BallY(cfg);

            var cross = PositiveGoalCrossSegment(cfg);
            if (!GoalDetector.TryDetectGoal(cross.prev, cross.curr, r, halfW, h, goals, out var team) || team != 0)
            {
                Debug.LogError("G4 unit: T10 oczekiwano gola drużyny 0 w +Z.");
                return false;
            }

            if (GoalDetector.TryDetectGoal(
                    new Vector3(0f, cfg.goalHeight + 0.3f, halfZ - 1f),
                    new Vector3(0f, cfg.goalHeight + 0.3f, halfZ + 0.5f),
                    r, halfW, h, goals, out _))
            {
                Debug.LogError("G4 unit: T11 nad poprzeczką nie powinien być golem.");
                return false;
            }

            if (GoalDetector.TryDetectGoal(
                    new Vector3(halfW + 0.5f, y, halfZ - 1f),
                    new Vector3(halfW + 0.5f, y, halfZ + 0.5f),
                    r, halfW, h, goals, out _))
            {
                Debug.LogError("G4 unit: T11 obok słupka nie powinien być golem.");
                return false;
            }

            if (!GoalDetector.TryDetectGoal(
                    new Vector3(0f, y, halfZ - 2f),
                    new Vector3(0f, y, halfZ + 2f),
                    r, halfW, h, goals, out _))
            {
                Debug.LogError("G4 unit: T12 szybki przelot powinien być golem.");
                return false;
            }

            if (!GoalDetector.TryDetectGoal(
                    new Vector3(0f, y, halfZ - 1f),
                    new Vector3(0f, y, halfZ + 0.5f),
                    r, halfW, h, goals, out _))
            {
                Debug.LogError("G4 unit: segment 24→25.5 (first step) powinien być golem.");
                return false;
            }

            if (GoalDetector.TryDetectGoal(
                    new Vector3(0f, y, halfZ + 0.5f),
                    new Vector3(0f, y, halfZ + 0.6f),
                    r, halfW, h, goals, out _))
            {
                Debug.LogError("G4 unit: T12 brak drugiego gola gdy piłka już za linią.");
                return false;
            }

            Debug.Log("G4 goal detector unit OK.");
            return true;
        }

        static bool RunArenaRoundTests()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
                return false;

            var controller = Object.FindAnyObjectByType<ArenaController>();
            if (controller == null || controller.RoundConfig == null)
            {
                Debug.LogError("G4 arena: brak ArenaController/RoundConfig.");
                return false;
            }

            var cfg = controller.ArenaConfig ?? LoadArenaConfigForTests();
            controller.MatchGoalLinesToPitchLength();

            controller.RefreshPlayerRegistry();
            controller.EditorDisableAutoReset();

            var prevSim = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;

            try
            {
                if (!TestT10_GoalTeam(controller, cfg))
                    return false;
                if (!TestT13_TimeoutAndManualReset(controller, cfg))
                    return false;
                if (!TestT14_ResetClearsState(controller))
                    return false;
                if (!TestT15_EndStateBeforeReset(controller, cfg))
                    return false;
                if (!TestBallOutVsGoalInterior(controller, cfg))
                    return false;
                if (!TestOverBarSegmentBallOut(controller, cfg))
                    return false;
                if (!TestEndedPauseFreezesBodies(controller, cfg))
                    return false;

                controller.MatchGoalLinesToPitchLength();
                controller.EditorPrepareForBatchVerification();

                if (!TestFirstStepGoalViaArenaStep(controller, cfg))
                    return false;
                if (!TestFirstStepBallOutViaArenaStep(controller, cfg))
                    return false;
                if (!TestFirstStepNoFalseGoalOnPitch(controller, cfg))
                    return false;

                Debug.Log("G4 arena round OK.");
                return true;
            }
            finally
            {
                Physics.simulationMode = prevSim;
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        static bool TestT10_GoalTeam(ArenaController controller, ArenaConfig cfg)
        {
            controller.ResetEpisode();
            var ended = 0;
            RoundEndReason reason = RoundEndReason.None;
            void Handler(RoundEndedEventArgs args)
            {
                ended++;
                reason = args.Reason;
            }

            controller.RoundEnded += Handler;
            try
            {
                var cross = PositiveGoalCrossSegment(cfg);
                controller.EditorEvaluateBallSegment(cross.prev, cross.curr, Dt);
                if (ended != 1 || reason != RoundEndReason.Goal)
                {
                    Debug.LogError($"G4 T10: oczekiwano 1 Goal, jest ended={ended} reason={reason}.");
                    return false;
                }

                if (!controller.TryGetEndState(out var state) || !state.HasScoringTeam || state.ScoringTeamId != 0)
                {
                    Debug.LogError("G4 T10: scoringTeam powinna być 0 (+Z broni drużyna 1).");
                    return false;
                }

                Debug.Log("G4 T10 OK.");
                return true;
            }
            finally
            {
                controller.RoundEnded -= Handler;
            }
        }

        static bool TestT13_TimeoutAndManualReset(ArenaController controller, ArenaConfig cfg)
        {
            controller.ResetEpisode();
            controller.EditorForceElapsedSeconds(59.99f);
            var y = BallY(cfg);
            controller.EditorEvaluateBallSegment(new Vector3(0f, y, 0f), new Vector3(0f, y, 0.01f), Dt);
            if (controller.LastEndReason != RoundEndReason.TimeLimit)
            {
                Debug.LogError($"G4 T13: oczekiwano TimeLimit, jest {controller.LastEndReason}.");
                return false;
            }

            controller.ResetEpisode();
            controller.RequestManualResetWhileRunning();
            if (controller.LastEndReason != RoundEndReason.ManualReset)
            {
                Debug.LogError($"G4 T13: oczekiwano ManualReset, jest {controller.LastEndReason}.");
                return false;
            }

            Debug.Log("G4 T13 OK.");
            return true;
        }

        static bool TestT14_ResetClearsState(ArenaController controller)
        {
            controller.ResetEpisode();
            var episodeBefore = controller.EpisodeId;

            var motor = Object.FindAnyObjectByType<PlayerIdentity>()?.GetComponent<PlayerMotor>();
            var ball = Object.FindAnyObjectByType<BallPhysicsSetup>()?.GetComponent<Rigidbody>();
            if (motor == null || ball == null)
            {
                Debug.LogError("G4 T14: brak gracza/piłki.");
                return false;
            }

            controller.SubmitCommand(0, new PlayerCommand { Forward = 1f, Kick = 1f }, 9000);
            controller.StepArenaPhysics(Dt);
            Physics.Simulate(Dt);
            controller.EditorForceElapsedSeconds(5f);
            controller.GetKickCooldownRemaining(0);
            var kickCfg = controller.KickConfig;
            if (kickCfg != null)
            {
                controller.StepArenaPhysics(Dt);
                Physics.Simulate(Dt);
            }

            controller.ResetEpisode();
            if (controller.EpisodeId != episodeBefore + 1)
            {
                Debug.LogError("G4 T14: EpisodeId powinien wzrosnąć o 1.");
                return false;
            }

            if (controller.Phase != ArenaPhase.Running || controller.ElapsedSeconds > 0.001f)
            {
                Debug.LogError("G4 T14: po resecie Running i zegar ~0.");
                return false;
            }

            if (motor.Body.linearVelocity.sqrMagnitude > 0.001f || ball.linearVelocity.sqrMagnitude > 0.001f)
            {
                Debug.LogError("G4 T14: prędkości powinny być zerowe.");
                return false;
            }

            if (ball.GetComponent<Collider>() != null && !ball.GetComponent<Collider>().enabled)
            {
                Debug.LogError("G4 T14: collider piłki powinien być włączony po resecie z Running.");
                return false;
            }

            if (controller.GetKickCooldownRemaining(0) > 0.001f)
            {
                Debug.LogError("G4 T14: cooldown powinien być wyzerowany.");
                return false;
            }

            if (!controller.TryGetActiveCommand(0, out var cmd) ||
                !Mathf.Approximately(cmd.Forward, 0f) || !Mathf.Approximately(cmd.Kick, 0f))
            {
                Debug.LogError("G4 T14: komendy powinny być zerowe.");
                return false;
            }

            Debug.Log("G4 T14 OK.");
            return true;
        }

        static bool TestT15_EndStateBeforeReset(ArenaController controller, ArenaConfig cfg)
        {
            controller.ResetEpisode();
            var cross = PositiveGoalCrossSegment(cfg);
            controller.EditorEvaluateBallSegment(cross.prev, cross.curr, Dt);
            if (controller.Phase != ArenaPhase.Ended)
            {
                Debug.LogError("G4 T15: oczekiwano Ended po golu.");
                return false;
            }

            if (!controller.TryGetEndState(out var endState))
            {
                Debug.LogError("G4 T15: brak stanu końcowego.");
                return false;
            }

            var half = cfg.HalfPlayLengthZ;
            var localBall = controller.ArenaRootTransform.InverseTransformPoint(endState.BallPosition);
            if (localBall.z < half)
            {
                Debug.LogError("G4 T15: piłka końcowa powinna być za linią bramkową.");
                return false;
            }

            var ballBeforeReset = endState.BallPosition;
            controller.ResetEpisode();
            var ball = Object.FindAnyObjectByType<BallPhysicsSetup>()?.GetComponent<Rigidbody>();
            if (ball == null)
                return false;

            if (Vector3.Distance(ball.position, ballBeforeReset) < 0.5f)
            {
                Debug.LogError("G4 T15: po resecie piłka nie powinna zostać w pozycji końcowej gola.");
                return false;
            }

            Debug.Log("G4 T15 OK.");
            return true;
        }

        static bool TestBallOutVsGoalInterior(ArenaController controller, ArenaConfig cfg)
        {
            var halfX = cfg.HalfWidthX;
            var halfZ = cfg.HalfPlayLengthZ;
            var y = BallY(cfg);

            controller.ResetEpisode();
            controller.EditorEvaluateBallSegment(
                new Vector3(halfX + 0.5f, y, 0f),
                new Vector3(halfX + 0.6f, y, 0f),
                Dt);
            if (controller.LastEndReason != RoundEndReason.BallOut)
            {
                Debug.LogError($"G4 BallOut: oczekiwano BallOut poza boiskiem, jest {controller.LastEndReason}.");
                return false;
            }

            controller.ResetEpisode();
            controller.EditorEvaluateBallSegment(
                new Vector3(0f, y, halfZ + 0.3f),
                new Vector3(0f, y, halfZ + 0.35f),
                Dt);
            if (controller.Phase == ArenaPhase.Ended && controller.LastEndReason == RoundEndReason.BallOut)
            {
                Debug.LogError("G4 BallOut: piłka w bramce +Z nie powinna kończyć BallOut.");
                return false;
            }

            Debug.Log("G4 BallOut OK.");
            return true;
        }

        static bool TestOverBarSegmentBallOut(ArenaController controller, ArenaConfig cfg)
        {
            var halfZ = cfg.HalfPlayLengthZ;
            var yOver = cfg.goalHeight + 0.3f;

            controller.ResetEpisode();
            controller.EditorEvaluateBallSegment(
                new Vector3(0f, yOver, halfZ - 1f),
                new Vector3(0f, yOver, halfZ + 0.5f),
                Dt);
            if (controller.LastEndReason != RoundEndReason.BallOut)
            {
                Debug.LogError($"G4 over bar: oczekiwano BallOut, jest {controller.LastEndReason}.");
                return false;
            }

            Debug.Log("G4 over bar BallOut OK.");
            return true;
        }

        static bool TestEndedPauseFreezesBodies(ArenaController controller, ArenaConfig cfg)
        {
            controller.ResetEpisode();
            var ball = Object.FindAnyObjectByType<BallPhysicsSetup>()?.GetComponent<Rigidbody>();
            if (ball == null)
                return false;

            ball.linearVelocity = new Vector3(0f, 0f, 8f);
            var cross = PositiveGoalCrossSegment(cfg);
            controller.EditorEvaluateBallSegment(cross.prev, cross.curr, Dt);
            if (controller.Phase != ArenaPhase.Ended || controller.LastEndReason != RoundEndReason.Goal)
            {
                Debug.LogError("G4 freeze: oczekiwano Ended/Goal przed pauzą.");
                return false;
            }

            if (!controller.TryGetEndState(out var endState))
            {
                Debug.LogError("G4 freeze: brak stanu końcowego.");
                return false;
            }

            var frozenPos = endState.BallPosition;
            for (var i = 0; i < 25; i++)
                Physics.Simulate(Dt);

            if (Vector3.Distance(ball.position, frozenPos) > 0.001f)
            {
                Debug.LogError($"G4 freeze: piłka ruszyła w pauzie ({ball.position} vs {frozenPos}).");
                return false;
            }

            if (!controller.TryGetEndState(out var endAgain) ||
                Vector3.Distance(endAgain.BallPosition, frozenPos) > 0.001f)
            {
                Debug.LogError("G4 freeze: TryGetEndState zmienił pozycję po krokach fizyki.");
                return false;
            }

            Debug.Log("G4 ended pause freeze OK.");
            return true;
        }

        static bool TestFirstStepGoalViaArenaStep(ArenaController controller, ArenaConfig cfg)
        {
            try
            {
                controller.ResetEpisode();
                controller.EditorPrepareForBatchVerification();
                MovePrimaryPlayerAside(controller);
                var root = controller.ArenaRootTransform;
                controller.MatchGoalLinesToPitchLength();
                var half = cfg.HalfPlayLengthZ;
                var y = BallY(cfg);
                var startLocal = new Vector3(0f, y, half - 1f);
                var endLocal = new Vector3(0f, y, half + 0.5f);
                var deltaLocal = endLocal - startLocal;
                var startWorld = root.TransformPoint(startLocal);
                controller.EditorSetBallPosition(startWorld);
                controller.EditorSetBallLinearVelocity(root.TransformDirection(deltaLocal / Dt));

                StepArenaSimulationFrame(controller, Dt);

                if (controller.Phase != ArenaPhase.Ended || controller.LastEndReason != RoundEndReason.Goal)
                {
                    Debug.LogError(
                        $"G4 first step goal: oczekiwano Ended/Goal, jest {controller.Phase} {controller.LastEndReason}.");
                    return false;
                }

                if (controller.ElapsedSeconds <= 0f)
                {
                    Debug.LogError("G4 first step goal: ElapsedSeconds powinien być > 0 po pierwszym kroku.");
                    return false;
                }

                if (!controller.TryGetEndState(out var state) || !state.HasScoringTeam || state.ScoringTeamId != 0)
                {
                    Debug.LogError("G4 first step goal: oczekiwano scoringTeam=0.");
                    return false;
                }

                Debug.Log("G4 first step goal via arena simulation step OK.");
                return true;
            }
            finally { }
        }

        static bool TestFirstStepBallOutViaArenaStep(ArenaController controller, ArenaConfig cfg)
        {
            try
            {
                controller.ResetEpisode();
                controller.EditorPrepareForBatchVerification();
                MovePrimaryPlayerAside(controller);
                controller.MatchGoalLinesToPitchLength();
                var halfX = cfg.HalfWidthX;
                var y = BallY(cfg);
                var root = controller.ArenaRootTransform;
                var startLocal = new Vector3(halfX - 0.15f, y, 0f);
                var startWorld = root.TransformPoint(startLocal);
                controller.EditorSetBallPosition(startWorld);
                controller.EditorSetBallLinearVelocity(root.TransformDirection(new Vector3(50f, 0f, 0f)));

                StepArenaSimulationFrame(controller, Dt);

                if (controller.Phase != ArenaPhase.Ended || controller.LastEndReason != RoundEndReason.BallOut)
                {
                    Debug.LogError(
                        $"G4 first step BallOut: oczekiwano Ended/BallOut, jest {controller.Phase} {controller.LastEndReason}.");
                    return false;
                }

                if (controller.ElapsedSeconds <= 0f)
                {
                    Debug.LogError("G4 first step BallOut: ElapsedSeconds powinien być > 0 po pierwszym kroku.");
                    return false;
                }

                Debug.Log("G4 first step BallOut via arena simulation step OK.");
                return true;
            }
            finally { }
        }

        static bool TestFirstStepNoFalseGoalOnPitch(ArenaController controller, ArenaConfig cfg)
        {
            try
            {
                controller.ResetEpisode();
                controller.EditorPrepareForBatchVerification();
                MovePrimaryPlayerAside(controller);
                controller.MatchGoalLinesToPitchLength();
                var y = BallY(cfg);
                var root = controller.ArenaRootTransform;
                var startWorld = root.TransformPoint(new Vector3(0f, y, 0f));
                controller.EditorSetBallPosition(startWorld);
                controller.EditorSetBallLinearVelocity(root.TransformDirection(new Vector3(2f, 0f, 0f)));

                StepArenaSimulationFrame(controller, Dt);

                if (controller.Phase == ArenaPhase.Ended && controller.LastEndReason == RoundEndReason.Goal)
                {
                    Debug.LogError("G4 first step: fałszywy gol gdy piłka zostaje na boisku.");
                    return false;
                }

                if (controller.Phase != ArenaPhase.Running)
                {
                    Debug.LogError(
                        $"G4 first step pitch: oczekiwano Running, jest {controller.Phase} {controller.LastEndReason}.");
                    return false;
                }

                Debug.Log("G4 first step brak fałszywego gola OK.");
                return true;
            }
            finally { }
        }
    }
}
#endif
