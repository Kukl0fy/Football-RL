#if UNITY_EDITOR
using FootballRL.Contracts;
using FootballRL.Gameplay;
using FootballRL.Input;
using FootballRL.Scenarios;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FootballRL.Editor
{
    public static class G5Verification
    {
        const string ScenePath = "Assets/_Project/Scenes/ManualPlayground.unity";
        const string ManualSandboxPath = "Assets/_Project/Configs/Scenarios/ManualSandbox.asset";
        const string MoveToTargetPath = "Assets/_Project/Configs/Scenarios/MoveToTarget.asset";
        const string ShootPath = "Assets/_Project/Configs/Scenarios/ShootAtEmptyGoal.asset";
        const float Dt = 0.02f;
        const float LayoutEpsilon = 0.05f;
        const float SeedDifferenceMin = 1f;

        public static void RunBatch()
        {
            if (!G5ArenaBuilder.RunBuildSilent())
            {
                Debug.LogError("G5 verify: build failed.");
                EditorApplication.Exit(1);
                return;
            }

            if (!RunAllTests())
            {
                Debug.LogError("G5 verify: tests failed.");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("G5 verification completed successfully.");
            EditorApplication.Exit(0);
        }

        public static bool RunAllTests()
        {
            if (!ScenarioAssetsExist())
                return false;

            if (!TestT16_SeedRepeatability())
                return false;
            if (!TestT17_SpawnValidation())
                return false;
            if (!TestSnapshotGoalAndReset())
                return false;
            if (!TestEndedAngularVelocity())
                return false;
            if (!TestCanKickNow())
                return false;
            if (!TestKickAngleRegulation())
                return false;
            if (!TestShootScenarioEasyLayout())
                return false;
            if (!TestSnapshotLocalSpace())
                return false;

            Debug.Log("G5 all tests OK.");
            return true;
        }

        static bool TestT16_SeedRepeatability()
        {
            var controller = OpenArenaController();
            if (controller == null)
                return false;

            var move = LoadMoveToTargetScenario();
            if (move == null)
                return false;

            controller.EditorDisableAutoReset();

            if (!TryResetAndMatchSampler(controller, move, 777, out var snap777a, out var err))
            {
                Debug.LogError($"G5 T16: reset seed 777 failed: {err}");
                return false;
            }

            if (!TryResetAndMatchSampler(controller, move, 777, out var snap777b, out err))
            {
                Debug.LogError($"G5 T16: drugi reset seed 777 failed: {err}");
                return false;
            }

            if (!PositionsEqual(snap777a, snap777b, LayoutEpsilon))
            {
                Debug.LogError("G5 T16: ten sam seed 777 — różne pozycje w snapshot.");
                return false;
            }

            if (!TryResetAndMatchSampler(controller, move, 888, out var snap888, out err))
            {
                Debug.LogError($"G5 T16: reset seed 888 failed: {err}");
                return false;
            }

            var playerDelta = Vector3.Distance(snap777a.Players[0].LocalPosition, snap888.Players[0].LocalPosition);
            var ballDelta = Vector3.Distance(snap777a.Ball.LocalPosition, snap888.Ball.LocalPosition);
            Debug.Log(
                $"G5 T16 seed777 player={snap777a.Players[0].LocalPosition} ball={snap777a.Ball.LocalPosition} " +
                $"seed888 player={snap888.Players[0].LocalPosition} ball={snap888.Ball.LocalPosition} " +
                $"(Δplayer={playerDelta:F2}m Δball={ballDelta:F2}m)");

            if (playerDelta < SeedDifferenceMin && ballDelta < SeedDifferenceMin)
            {
                Debug.LogError("G5 T16: seedy 777 i 888 powinny dać wyraźnie różne starty (≥1 m).");
                return false;
            }

            Debug.Log("G5 T16 OK.");
            return true;
        }

        static bool TryResetAndMatchSampler(
            ArenaController controller,
            ScenarioDefinition scenario,
            int seed,
            out ArenaSnapshot snapshot,
            out string error)
        {
            snapshot = default;
            error = null;

            if (scenario == null)
            {
                error = "Brak ScenarioDefinition (test asset).";
                return false;
            }

            if (controller.ArenaConfig == null)
            {
                error = "Brak ArenaConfig na arenie (arenaRoot?).";
                return false;
            }

            if (!TrySampleExpectedLayout(controller, scenario, seed, out var expected, out error))
            {
                error = $"sampler: {error}";
                return false;
            }

            if (!controller.TryResetEpisode(ResetEpisodeRequest.ForScenario(scenario, seed), out error))
            {
                error = $"reset: {error}";
                return false;
            }

            snapshot = controller.CaptureSnapshot();
            if (!SnapshotMatchesLayout(snapshot, expected, LayoutEpsilon))
            {
                error =
                    $"snapshot≠sampler player snap={snapshot.Players[0].LocalPosition} exp={expected.playerLocalPosition} " +
                    $"ball snap={snapshot.Ball.LocalPosition} exp={expected.ballLocalPosition}";
                return false;
            }

            return true;
        }

        static bool TrySampleExpectedLayout(
            ArenaController controller,
            ScenarioDefinition scenario,
            int seed,
            out SpawnLayout layout,
            out string error)
        {
            var spawn = controller.GetComponent<ArenaEpisodeSpawnState>();
            return SpawnSampler.TrySample(
                scenario,
                controller.ArenaConfig,
                spawn,
                new System.Random(seed),
                default,
                false,
                out layout,
                out error);
        }

        static bool SnapshotMatchesLayout(ArenaSnapshot snap, SpawnLayout layout, float eps)
        {
            if (snap.Players == null || snap.Players.Length == 0)
                return false;

            if (Vector3.Distance(snap.Players[0].LocalPosition, layout.playerLocalPosition) > eps)
                return false;

            if (Vector3.Distance(snap.Ball.LocalPosition, layout.ballLocalPosition) > eps)
                return false;

            if (layout.hasTarget)
            {
                if (!snap.Target.Present)
                    return false;
                if (Vector3.Distance(snap.Target.LocalPosition, layout.targetLocalPosition) > eps)
                    return false;
            }

            return true;
        }

        static bool PositionsEqual(ArenaSnapshot a, ArenaSnapshot b, float eps)
        {
            if (Vector3.Distance(a.Players[0].LocalPosition, b.Players[0].LocalPosition) > eps)
                return false;
            if (Vector3.Distance(a.Ball.LocalPosition, b.Ball.LocalPosition) > eps)
                return false;
            if (a.Target.Present != b.Target.Present)
                return false;
            if (a.Target.Present &&
                Vector3.Distance(a.Target.LocalPosition, b.Target.LocalPosition) > eps)
                return false;

            return true;
        }

        static bool TestT17_SpawnValidation()
        {
            var controller = OpenArenaController();
            if (controller == null)
                return false;

            var move = LoadMoveToTargetScenario();
            if (move == null)
                return false;

            var spawn = controller.GetComponent<ArenaEpisodeSpawnState>();
            spawn.GetStored(out var prefabBallState, out var prefabPlayer);
            var prefabPlayerPos = prefabPlayer.localPosition;
            var prefabBallPos = prefabBallState.localPosition;

            if (!controller.TryResetEpisode(ResetEpisodeRequest.ForScenario(move, 4242), out var err))
            {
                Debug.LogError($"G5 T17: reset seed 4242 failed: {err}");
                return false;
            }

            var afterRandom = controller.CaptureSnapshot();
            var movedFromPrefab =
                Vector3.Distance(afterRandom.Players[0].LocalPosition, prefabPlayerPos) > LayoutEpsilon ||
                Vector3.Distance(afterRandom.Ball.LocalPosition, prefabBallPos) > LayoutEpsilon;
            if (!movedFromPrefab)
            {
                Debug.LogError("G5 T17: losowy reset powinien zmienić pozycje względem prefabu.");
                return false;
            }

            for (var i = 0; i < 20; i++)
            {
                if (!controller.TryResetEpisode(ResetEpisodeRequest.ForScenario(move, 2000 + i), out err))
                {
                    Debug.LogError($"G5 T17: losowy reset {i} nie powiódł się: {err}");
                    return false;
                }
            }

            var ball = Object.FindAnyObjectByType<BallPhysicsSetup>()?.GetComponent<Rigidbody>();
            var beforeFail = ball != null ? ball.position : Vector3.zero;

            var bad = new ExactEpisodeState
            {
                hasPlayer = true,
                playerLocalPosition = Vector3.zero,
                playerLocalRotation = Quaternion.identity,
                hasBall = true,
                ballLocalPosition = Vector3.zero,
                ballLocalRotation = Quaternion.identity
            };
            if (controller.TryResetEpisode(ResetEpisodeRequest.WithExactState(bad, move), out var error))
            {
                Debug.LogError("G5 T17: nakładający się exact spawn powinien się nie udać.");
                return false;
            }

            if (string.IsNullOrEmpty(error))
            {
                Debug.LogError("G5 T17: oczekiwano czytelnego błędu spawnu.");
                return false;
            }

            if (ball != null && Vector3.Distance(ball.position, beforeFail) > LayoutEpsilon)
            {
                Debug.LogError("G5 T17: nieudany reset nie powinien przestawiać świata.");
                return false;
            }

            Debug.Log("G5 T17 OK.");
            return true;
        }

        static bool TestSnapshotGoalAndReset()
        {
            var controller = OpenArenaController();
            if (controller == null)
                return false;

            var manual = LoadManualSandboxScenario();
            if (manual == null)
                return false;

            controller.EditorDisableAutoReset();
            if (!controller.TryResetEpisode(ResetEpisodeRequest.ForScenario(manual, 1), out var resetErr))
            {
                Debug.LogError($"G5 snapshot: reset failed: {resetErr}");
                return false;
            }

            var episodeBefore = controller.EpisodeId;

            controller.EditorEvaluateBallSegment(new Vector3(0f, 0.22f, 9.5f), new Vector3(0f, 0.22f, 10.5f), Dt);
            var snap = controller.CaptureSnapshot();
            if (snap.Arena.Phase != ArenaPhase.Ended || snap.RoundEnd.Reason != RoundEndReason.Goal)
            {
                Debug.LogError("G5 snapshot: oczekiwano Ended/Goal przed resetem.");
                return false;
            }

            if (!snap.RoundEnd.HasScoringTeam || snap.RoundEnd.ScoringTeamId != 0)
            {
                Debug.LogError("G5 snapshot: brak poprawnej drużyny strzelca.");
                return false;
            }

            controller.ResetEpisode();
            var after = controller.CaptureSnapshot();
            if (after.Arena.Phase != ArenaPhase.Running || after.Arena.EpisodeId != episodeBefore + 1)
            {
                Debug.LogError("G5 snapshot: po resecie Running i nowy EpisodeId.");
                return false;
            }

            Debug.Log("G5 snapshot OK.");
            return true;
        }

        static bool TestEndedAngularVelocity()
        {
            var controller = OpenArenaController();
            if (controller == null)
                return false;

            var manual = LoadManualSandboxScenario();
            if (manual == null)
                return false;

            controller.EditorDisableAutoReset();
            if (!controller.TryResetEpisode(ResetEpisodeRequest.ForScenario(manual, 11), out var resetErr))
            {
                Debug.LogError($"G5 spin: reset failed: {resetErr}");
                return false;
            }

            var ball = Object.FindAnyObjectByType<BallPhysicsSetup>()?.GetComponent<Rigidbody>();
            if (ball == null)
                return false;

            var spin = new Vector3(0.5f, 4f, -1.2f);
            ball.angularVelocity = spin;
            controller.RequestManualResetWhileRunning();

            var ended = controller.CaptureSnapshot();
            if (ended.Arena.Phase != ArenaPhase.Ended)
            {
                Debug.LogError("G5 spin: oczekiwano fazy Ended.");
                return false;
            }

            if (ended.Ball.LocalAngularVelocity.sqrMagnitude < 0.5f)
            {
                Debug.LogError(
                    $"G5 spin: snapshot Ended powinien zachować spin piłki, jest {ended.Ball.LocalAngularVelocity}.");
                return false;
            }

            Debug.Log($"G5 spin Ended OK: ball ω={ended.Ball.LocalAngularVelocity} (world ref magnitude OK).");

            controller.ResetEpisode();
            var running = controller.CaptureSnapshot();
            if (running.Arena.Phase != ArenaPhase.Running)
                return false;

            if (running.Ball.LocalAngularVelocity.sqrMagnitude > 0.01f)
            {
                Debug.LogError("G5 spin: po resecie angular velocity piłki powinno być ~0.");
                return false;
            }

            Debug.Log("G5 spin reset OK.");
            return true;
        }

        static bool TestCanKickNow()
        {
            var controller = OpenArenaController();
            if (controller == null)
                return false;

            var manual = LoadManualSandboxScenario();
            if (manual == null)
                return false;

            if (!controller.TryResetEpisode(ResetEpisodeRequest.ForScenario(manual, 5), out var resetErr))
            {
                Debug.LogError($"G5 canKickNow: reset failed: {resetErr}");
                return false;
            }

            var ball = Object.FindAnyObjectByType<BallPhysicsSetup>()?.GetComponent<Rigidbody>();
            var motor = Object.FindAnyObjectByType<PlayerIdentity>()?.GetComponent<PlayerMotor>();
            if (ball == null || motor == null)
                return false;

            PlaceLocal(motor.transform, motor.Body, new Vector3(0f, 0.5f, -2f), Quaternion.identity);
            PlaceLocal(ball.transform, ball, new Vector3(0f, 0.27f, -1.05f), Quaternion.identity);

            var good = controller.CaptureSnapshot();
            if (good.Players.Length == 0 || !good.Players[0].CanKickNow)
            {
                Debug.LogError("G5 canKickNow: piłka przed graczem powinna pozwalać na kopnięcie.");
                return false;
            }

            var behind = motor.transform.localPosition - motor.transform.forward * 2f;
            behind.y = 0.27f;
            PlaceLocal(ball.transform, ball, behind, Quaternion.identity);

            var bad = controller.CaptureSnapshot();
            if (bad.Players[0].CanKickNow)
            {
                Debug.LogError("G5 canKickNow: piłka za plecami nie powinna dawać CanKickNow.");
                return false;
            }

            Debug.Log("G5 canKickNow OK.");
            return true;
        }

        static void PlaceLocal(Transform t, Rigidbody body, Vector3 localPos, Quaternion localRot)
        {
            t.localPosition = localPos;
            t.localRotation = localRot;
            if (body != null)
            {
                body.position = t.position;
                body.rotation = t.rotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            Physics.SyncTransforms();
        }

        static bool TestKickAngleRegulation()
        {
            var controller = OpenArenaController();
            if (controller == null)
                return false;

            var manual = LoadManualSandboxScenario();
            var shoot = LoadShootScenario();
            if (manual == null || shoot == null)
                return false;

            if (!controller.TryResetEpisode(ResetEpisodeRequest.ForScenario(manual, 1), out var angleResetErr))
            {
                Debug.LogError("G5 angle: reset ManualSandbox failed.");
                return false;
            }

            if (!controller.IsKickAngleRegulated)
            {
                Debug.LogError("G5 angle: ManualSandbox powinien regulować kąt.");
                return false;
            }

            var manualState = new ManualKickInputState();
            var manualBefore = manualState.SelectedAngleDeg;
            ApplyKickUiLikeKeyboard(manualState, controller, upPressed: true);
            if (manualState.SelectedAngleDeg <= manualBefore + 0.1f)
            {
                Debug.LogError("G5 angle: strzałka w górę w ManualSandbox powinna zwiększyć kąt.");
                return false;
            }

            if (!controller.TryResetEpisode(ResetEpisodeRequest.ForScenario(shoot, 3), out var shootAngleResetErr))
            {
                Debug.LogError("G5 angle: reset Shoot failed.");
                return false;
            }

            if (controller.IsKickAngleRegulated)
            {
                Debug.LogError("G5 angle: ShootAtEmptyGoal nie powinien regulować kąta strzałkami.");
                return false;
            }

            var shootState = new ManualKickInputState();
            ApplyKickUiLikeKeyboard(shootState, controller, upPressed: true);
            if (!Mathf.Approximately(shootState.SelectedLoft, shoot.lockedLoft))
            {
                Debug.LogError(
                    $"G5 angle: w Shoot loft powinien zostać {shoot.lockedLoft}, jest {shootState.SelectedLoft}.");
                return false;
            }

            var shootStateWrongPath = new ManualKickInputState();
            shootStateWrongPath.SetLoft(shoot.lockedLoft);
            KeyboardKickMapping.ApplyArrowAdjustments(shootStateWrongPath, true, false, false, false);
            if (Mathf.Approximately(shootStateWrongPath.SelectedLoft, shoot.lockedLoft))
            {
                Debug.LogError("G5 angle: ApplyArrowAdjustments samo w sobie powinno zmieniać kąt (kontrola testu).");
                return false;
            }

            Debug.Log("G5 kick angle regulation OK.");
            return true;
        }

        static void ApplyKickUiLikeKeyboard(ManualKickInputState state, ArenaController arena, bool upPressed)
        {
            if (arena.IsKickAngleRegulated)
            {
                KeyboardKickMapping.ApplyArrowAdjustments(
                    state,
                    upPressed,
                    false,
                    false,
                    false);
            }
            else
            {
                state.SetLoft(arena.LockedKickLoft);
            }
        }

        static bool TestShootScenarioEasyLayout()
        {
            var controller = OpenArenaController();
            if (controller == null)
                return false;

            var shoot = LoadShootScenario();
            if (shoot == null)
                return false;

            if (!controller.TryResetEpisode(ResetEpisodeRequest.ForScenario(shoot, 3), out var resetErr))
            {
                Debug.LogError($"G5 shoot layout: reset failed: {resetErr}");
                return false;
            }

            var snap = controller.CaptureSnapshot();
            if (Vector3.Distance(snap.Players[0].LocalPosition, shoot.easyPlayerLocalPosition) > LayoutEpsilon)
            {
                Debug.LogError(
                    $"G5 shoot: gracz {snap.Players[0].LocalPosition} ≠ easy {shoot.easyPlayerLocalPosition}.");
                return false;
            }

            if (Vector3.Distance(snap.Ball.LocalPosition, shoot.easyBallLocalPosition) > LayoutEpsilon)
            {
                Debug.LogError($"G5 shoot: piłka {snap.Ball.LocalPosition} ≠ easy {shoot.easyBallLocalPosition}.");
                return false;
            }

            Debug.Log("G5 shoot easy layout OK.");
            return true;
        }

        static bool TestSnapshotLocalSpace()
        {
            var controller = OpenArenaController();
            if (controller == null)
                return false;

            var manual = LoadManualSandboxScenario();
            if (manual == null)
                return false;

            if (!controller.TryResetEpisode(ResetEpisodeRequest.ForScenario(manual, 2), out var resetErr))
            {
                Debug.LogError($"G5 local: reset failed: {resetErr}");
                return false;
            }

            var root = controller.ArenaRootTransform;
            var baseline = controller.CaptureSnapshot().Ball.LocalPosition;

            var originalPos = root.position;
            var originalRot = root.rotation;
            try
            {
                root.SetPositionAndRotation(new Vector3(5f, 0f, -3f), Quaternion.Euler(0f, 33f, 0f));
                Physics.SyncTransforms();
                var rotated = controller.CaptureSnapshot().Ball.LocalPosition;
                if (Vector3.Distance(baseline, rotated) > 0.15f)
                {
                    Debug.LogError($"G5 local: lokalna pozycja piłki powinna być stabilna ({baseline} vs {rotated}).");
                    return false;
                }
            }
            finally
            {
                root.SetPositionAndRotation(originalPos, originalRot);
                Physics.SyncTransforms();
            }

            Debug.Log("G5 local space OK.");
            return true;
        }

        static bool ScenarioAssetsExist()
        {
            if (AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(ManualSandboxPath) == null ||
                AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(MoveToTargetPath) == null ||
                AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(ShootPath) == null)
            {
                Debug.LogError("G5: brak assetów scenariuszy.");
                return false;
            }

            return true;
        }

        static ScenarioDefinition LoadScenarioAsset(string path, string label)
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScenarioDefinition>(path);
            if (asset == null)
                Debug.LogError($"G5: brak assetu {label}.");
            return asset;
        }

        static ScenarioDefinition LoadMoveToTargetScenario() =>
            LoadScenarioAsset(MoveToTargetPath, "MoveToTarget");

        static ScenarioDefinition LoadManualSandboxScenario() =>
            LoadScenarioAsset(ManualSandboxPath, "ManualSandbox");

        static ScenarioDefinition LoadShootScenario() =>
            LoadScenarioAsset(ShootPath, "ShootAtEmptyGoal");

        static ArenaController OpenArenaController()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var controller = Object.FindAnyObjectByType<ArenaController>();
            if (controller == null)
            {
                Debug.LogError("G5: brak ArenaController w ManualPlayground.");
                return null;
            }

            controller.EditorPrepareForBatchVerification();
            controller.EditorDisableAutoReset();
            return controller;
        }
    }
}
#endif
