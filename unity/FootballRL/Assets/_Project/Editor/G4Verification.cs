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
        const float Dt = 0.02f;
        const float BallY = 0.22f;

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
            if (!RunBallBoundsUnitTests())
                return false;

            if (!RunGoalDetectorUnitTests())
                return false;

            return RunArenaRoundTests();
        }

        static ArenaConfig CreateDefaultArenaConfig() => ScriptableObject.CreateInstance<ArenaConfig>();

        static bool RunBallBoundsUnitTests()
        {
            var cfg = CreateDefaultArenaConfig();
            var r = cfg.ballRadius;

            if (ArenaBounds.IsBallOutOfPlay(new Vector3(1.4f, BallY, 10.5f), r, cfg))
            {
                Debug.LogError("G4 bounds: x=1,4 z=10,5 w otworze nie powinno być BallOut.");
                return false;
            }

            if (!ArenaBounds.IsBallOutOfPlay(new Vector3(0f, 2.3f, 10.5f), r, cfg))
            {
                Debug.LogError("G4 bounds: y=2,3 za poprzeczką w bramce powinno być BallOut.");
                return false;
            }

            if (ArenaBounds.IsBallOutOfPlay(new Vector3(0f, 1f, 10.5f), r, cfg))
            {
                Debug.LogError("G4 bounds: piłka w środku siatki nie powinna być BallOut.");
                return false;
            }

            if (!ArenaBounds.IsBallOutOfPlay(new Vector3(6.5f, BallY, 0f), r, cfg))
            {
                Debug.LogError("G4 bounds: wyjście bokiem boiska nadal BallOut.");
                return false;
            }

            Debug.Log("G4 bounds unit OK.");
            return true;
        }

        static bool RunGoalDetectorUnitTests()
        {
            var goals = new[]
            {
                new GoalDefinition { defendingTeamId = 1, goalLineZ = 10f, enterDirectionZ = 1f },
                new GoalDefinition { defendingTeamId = 0, goalLineZ = -10f, enterDirectionZ = -1f }
            };

            var r = 0.22f;
            var halfW = 1.5f;
            var h = 2f;

            if (!GoalDetector.TryDetectGoal(
                    new Vector3(0f, BallY, 9.5f),
                    new Vector3(0f, BallY, 10.5f),
                    r, halfW, h, goals, out var team) || team != 0)
            {
                Debug.LogError("G4 unit: T10 oczekiwano gola drużyny 0 w +Z.");
                return false;
            }

            if (GoalDetector.TryDetectGoal(
                    new Vector3(0f, 2.3f, 9.5f),
                    new Vector3(0f, 2.3f, 10.5f),
                    r, halfW, h, goals, out _))
            {
                Debug.LogError("G4 unit: T11 nad poprzeczką nie powinien być golem.");
                return false;
            }

            if (GoalDetector.TryDetectGoal(
                    new Vector3(2f, BallY, 9.5f),
                    new Vector3(2f, BallY, 10.5f),
                    r, halfW, h, goals, out _))
            {
                Debug.LogError("G4 unit: T11 obok słupka nie powinien być golem.");
                return false;
            }

            if (!GoalDetector.TryDetectGoal(
                    new Vector3(0f, BallY, 9f),
                    new Vector3(0f, BallY, 11f),
                    r, halfW, h, goals, out _))
            {
                Debug.LogError("G4 unit: T12 szybki przelot powinien być golem.");
                return false;
            }

            if (GoalDetector.TryDetectGoal(
                    new Vector3(0f, BallY, 10.5f),
                    new Vector3(0f, BallY, 10.6f),
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

            controller.RefreshPlayerRegistry();
            controller.EditorDisableAutoReset();

            var prevSim = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;

            try
            {
                if (!TestT10_GoalTeam(controller))
                    return false;
                if (!TestT13_TimeoutAndManualReset(controller))
                    return false;
                if (!TestT14_ResetClearsState(controller))
                    return false;
                if (!TestT15_EndStateBeforeReset(controller))
                    return false;
                if (!TestBallOutVsGoalInterior(controller))
                    return false;
                if (!TestOverBarSegmentBallOut(controller))
                    return false;
                if (!TestEndedPauseFreezesBodies(controller))
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

        static bool TestT10_GoalTeam(ArenaController controller)
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
                controller.EditorEvaluateBallSegment(new Vector3(0f, BallY, 9.5f), new Vector3(0f, BallY, 10.5f), Dt);
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

        static bool TestT13_TimeoutAndManualReset(ArenaController controller)
        {
            controller.ResetEpisode();
            controller.EditorForceElapsedSeconds(59.99f);
            controller.EditorEvaluateBallSegment(new Vector3(0f, BallY, 0f), new Vector3(0f, BallY, 0.01f), Dt);
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

        static bool TestT15_EndStateBeforeReset(ArenaController controller)
        {
            controller.ResetEpisode();
            controller.EditorEvaluateBallSegment(new Vector3(0f, BallY, 9.5f), new Vector3(0f, BallY, 10.5f), Dt);
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

            if (endState.BallPosition.z < 10f)
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

        static bool TestBallOutVsGoalInterior(ArenaController controller)
        {
            controller.ResetEpisode();
            controller.EditorEvaluateBallSegment(new Vector3(6.5f, BallY, 0f), new Vector3(6.6f, BallY, 0f), Dt);
            if (controller.LastEndReason != RoundEndReason.BallOut)
            {
                Debug.LogError($"G4 BallOut: oczekiwano BallOut poza boiskiem, jest {controller.LastEndReason}.");
                return false;
            }

            controller.ResetEpisode();
            controller.EditorEvaluateBallSegment(new Vector3(0f, BallY, 10.3f), new Vector3(0f, BallY, 10.35f), Dt);
            if (controller.Phase == ArenaPhase.Ended && controller.LastEndReason == RoundEndReason.BallOut)
            {
                Debug.LogError("G4 BallOut: piłka w bramce +Z nie powinna kończyć BallOut.");
                return false;
            }

            Debug.Log("G4 BallOut OK.");
            return true;
        }

        static bool TestOverBarSegmentBallOut(ArenaController controller)
        {
            controller.ResetEpisode();
            controller.EditorEvaluateBallSegment(new Vector3(0f, 2.3f, 9.5f), new Vector3(0f, 2.3f, 10.5f), Dt);
            if (controller.LastEndReason != RoundEndReason.BallOut)
            {
                Debug.LogError($"G4 over bar: oczekiwano BallOut, jest {controller.LastEndReason}.");
                return false;
            }

            Debug.Log("G4 over bar BallOut OK.");
            return true;
        }

        static bool TestEndedPauseFreezesBodies(ArenaController controller)
        {
            controller.ResetEpisode();
            var ball = Object.FindAnyObjectByType<BallPhysicsSetup>()?.GetComponent<Rigidbody>();
            if (ball == null)
                return false;

            ball.linearVelocity = new Vector3(0f, 0f, 8f);
            controller.EditorEvaluateBallSegment(new Vector3(0f, BallY, 9.5f), new Vector3(0f, BallY, 10.5f), Dt);
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
    }
}
#endif
