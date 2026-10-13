#if UNITY_EDITOR
using System;
using FootballRL.Contracts;
using FootballRL.Gameplay;
using FootballRL.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FootballRL.Editor
{
    public static class G3Verification
    {
        const string ScenePath = "Assets/_Project/Scenes/ManualPlayground.unity";
        const int PlayerId = 0;
        const float Dt = 0.02f;
        static readonly Vector3 KickableBallPos = new Vector3(0f, 0.27f, -1.05f);

        public static void RunBatch()
        {
            if (!G3ArenaBuilder.RunBuildSilent())
            {
                Debug.LogError("G3 verify: build failed.");
                EditorApplication.Exit(1);
                return;
            }

            if (!RunAllTests())
            {
                Debug.LogError("G3 verify: tests failed.");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("G3 verification completed successfully.");
            EditorApplication.Exit(0);
        }

        public static bool RunAllTests()
        {
            if (!RunKeyboardKickMappingTests())
                return false;

            if (!TestFocusCancelClearsPendingKick())
                return false;

            return RunKickPhysicsTests();
        }

        static bool RunKeyboardKickMappingTests()
        {
            var state = new ManualKickInputState();
            if (!Mathf.Approximately(state.SelectedAngleDeg, 0f) || !Mathf.Approximately(state.SelectedKick, 1f))
            {
                Debug.LogError("G3 map: domyślny kąt 0° i Kick=1.");
                return false;
            }

            KeyboardKickMapping.ApplyArrowAdjustments(state, true, false, false, false);
            if (!Mathf.Approximately(state.SelectedAngleDeg, 5f))
            {
                Debug.LogError("G3 map: strzałka góra powinna dodać 5°.");
                return false;
            }

            KeyboardKickMapping.ApplyArrowAdjustments(state, false, false, true, false);
            if (state.SelectedKick >= 1f)
            {
                Debug.LogError("G3 map: strzałka lewo powinna zmniejszyć siłę.");
                return false;
            }

            if (!KeyboardKickMapping.ShouldFireKickOnSpace(true, false))
            {
                Debug.LogError("G3 map: zbocze spacji powinno strzelić.");
                return false;
            }

            if (KeyboardKickMapping.ShouldFireKickOnSpace(true, true))
            {
                Debug.LogError("G3 map: trzymana spacja nie powinna powtarzać strzału.");
                return false;
            }

            Debug.Log("G3 map OK.");
            return true;
        }

        /// <summary>Symuluje: Spacja (Kick&gt;0), potem utrata fokusu (Kick=0) przed krokiem fizyki.</summary>
        static bool TestFocusCancelClearsPendingKick()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
                return false;

            var controller = UnityEngine.Object.FindAnyObjectByType<ArenaController>();
            if (controller == null || controller.KickConfig == null)
            {
                Debug.LogError("G3 focus: brak ArenaController/KickConfig.");
                return false;
            }

            controller.RefreshPlayerRegistry();
            var motor = UnityEngine.Object.FindAnyObjectByType<PlayerIdentity>()?.GetComponent<PlayerMotor>();
            var ball = UnityEngine.Object.FindAnyObjectByType<BallPhysicsSetup>()?.GetComponent<Rigidbody>();
            if (motor == null || ball == null)
            {
                Debug.LogError("G3 focus: brak gracza/piłki.");
                return false;
            }

            var prevSim = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;

            try
            {
                var success = 0;
                void Handler(KickSucceededEventArgs _) => success++;
                controller.KickSucceeded += Handler;

                try
                {
                    ResetBodies(motor, ball, new Vector3(0f, 0.5f, -2f), Quaternion.identity, KickableBallPos);
                    var kickOnly = new PlayerCommand { Kick = 1f, Loft = -1f };
                    controller.SubmitCommand(PlayerId, kickOnly, 9001);
                    var zero = PlayerCommand.Zero;
                    controller.SubmitCommand(PlayerId, zero, 9002);

                    Physics.SyncTransforms();
                    controller.StepArenaPhysics(Dt);
                    Physics.Simulate(Dt);

                    if (success != 0)
                    {
                        Debug.LogError($"G3 focus (postój): oczekiwano 0 impulsów, jest {success}.");
                        return false;
                    }

                    success = 0;
                    ResetBodies(motor, ball, new Vector3(0f, 0.5f, -2f), Quaternion.identity, KickableBallPos);
                    var moveKick = new PlayerCommand { Forward = 1f, Kick = 1f, Loft = -1f };
                    controller.SubmitCommand(PlayerId, moveKick, 9003);
                    var moveZero = new PlayerCommand { Forward = 1f, Kick = 0f, Loft = -1f };
                    controller.SubmitCommand(PlayerId, moveZero, 9004);

                    Physics.SyncTransforms();
                    controller.StepArenaPhysics(Dt);
                    Physics.Simulate(Dt);

                    if (success != 0)
                    {
                        Debug.LogError($"G3 focus (ruch): oczekiwano 0 impulsów, jest {success}.");
                        return false;
                    }
                }
                finally
                {
                    controller.KickSucceeded -= Handler;
                }

                Debug.Log("G3 focus cancel OK.");
                return true;
            }
            finally
            {
                Physics.simulationMode = prevSim;
            }
        }

        static bool RunKickPhysicsTests()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
                return false;

            var controller = UnityEngine.Object.FindAnyObjectByType<ArenaController>();
            if (controller == null || controller.KickConfig == null)
            {
                Debug.LogError("G3 physics: brak ArenaController/KickConfig.");
                return false;
            }

            controller.RefreshPlayerRegistry();

            var prevSim = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;

            try
            {
                if (!TestT05_SingleImpulsePerCommandId(controller))
                    return false;
                if (!TestT06_CooldownNoPending(controller))
                    return false;
                if (!TestT07_RejectionCases(controller))
                    return false;
                if (!TestT08_StrengthScaling(controller))
                    return false;
                if (!TestT09_LoftAngles(controller))
                    return false;

                Debug.Log("G3 physics OK.");
                return true;
            }
            finally
            {
                Physics.simulationMode = prevSim;
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        static bool PrepareController(ref ArenaController controller, ref PlayerMotor motor, ref Rigidbody ballRb)
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            controller = UnityEngine.Object.FindAnyObjectByType<ArenaController>();
            controller?.RefreshPlayerRegistry();
            if (controller == null)
                return false;

            motor = FindMotor();
            ballRb = UnityEngine.Object.FindAnyObjectByType<BallPhysicsSetup>()?.GetComponent<Rigidbody>();
            return motor != null && ballRb != null;
        }

        static PlayerMotor FindMotor()
        {
            var id = UnityEngine.Object.FindAnyObjectByType<PlayerIdentity>();
            return id != null ? id.GetComponent<PlayerMotor>() : null;
        }

        static void ResetBodies(PlayerMotor motor, Rigidbody ball, Vector3 playerPos, Quaternion playerRot, Vector3 ballPos)
        {
            var prb = motor.GetComponent<Rigidbody>();
            motor.transform.SetPositionAndRotation(playerPos, playerRot);
            prb.position = playerPos;
            prb.rotation = playerRot;
            prb.linearVelocity = Vector3.zero;
            prb.angularVelocity = Vector3.zero;
            prb.WakeUp();

            ball.transform.position = ballPos;
            ball.position = ballPos;
            ball.linearVelocity = Vector3.zero;
            ball.angularVelocity = Vector3.zero;
            ball.WakeUp();
            Physics.SyncTransforms();
        }

        static void Simulate(ArenaController controller)
        {
            Physics.SyncTransforms();
            controller.StepArenaPhysics(Dt);
            Physics.Simulate(Dt);
        }

        static bool TestT05_SingleImpulsePerCommandId(ArenaController controller)
        {
            PlayerMotor motor = null;
            Rigidbody ball = null;
            if (!PrepareController(ref controller, ref motor, ref ball))
                return false;

            ResetBodies(motor, ball, new Vector3(0f, 0.5f, -2f), Quaternion.identity, KickableBallPos);

            var cmd = new PlayerCommand { Kick = 1f, Loft = -1f };
            controller.SubmitCommand(PlayerId, cmd, 500);

            var success = 0;
            void Handler(KickSucceededEventArgs _) => success++;
            controller.KickSucceeded += Handler;

            try
            {
                for (var i = 0; i < 80; i++)
                    Simulate(controller);

                if (success != 1)
                {
                    controller.TryGetLastKickAttempt(PlayerId, out var last);
                    var anchor = motor.GetComponent<PlayerKickAnchor>();
                    var origin = anchor != null ? anchor.GetKickWorldPosition() : motor.transform.position;
                    var dist = Vector3.Distance(origin, ball.worldCenterOfMass);
                    Debug.LogError(
                        $"G3 T05: oczekiwano 1 impuls, jest {success}. reason={last.Reason} dist={dist:F3} origin={origin} ball={ball.position}.");
                    return false;
                }

                Debug.Log("G3 T05 OK: jeden impuls przy powtarzanym commandId.");
                return true;
            }
            finally
            {
                controller.KickSucceeded -= Handler;
            }
        }

        static bool TestT06_CooldownNoPending(ArenaController controller)
        {
            PlayerMotor motor = null;
            Rigidbody ball = null;
            if (!PrepareController(ref controller, ref motor, ref ball))
                return false;

            var kick = new PlayerCommand { Kick = 1f, Loft = -1f };
            var success = 0;
            void Handler(KickSucceededEventArgs _) => success++;
            controller.KickSucceeded += Handler;
            try
            {
                ResetBodies(motor, ball, new Vector3(0f, 0.5f, -2f), Quaternion.identity, KickableBallPos);
                controller.SubmitCommand(PlayerId, kick, 600);
                Simulate(controller);
                if (success != 1)
                {
                    Debug.LogError($"G3 T06: pierwsze kopnięcie powinno się udać (success={success}).");
                    return false;
                }

                controller.SubmitCommand(PlayerId, kick, 601);
                Simulate(controller);
                if (!controller.TryGetLastKickAttempt(PlayerId, out var duringCd) || duringCd.Reason != KickRejectReason.Cooldown)
                {
                    Debug.LogError("G3 T06: podczas cooldownu powinien być powód cooldown.");
                    return false;
                }

                var cooldownSteps = Mathf.CeilToInt(controller.KickConfig.cooldownSeconds / Dt) + 2;
                for (var i = 0; i < cooldownSteps; i++)
                    controller.StepKickCooldownOnly(Dt);

                if (success != 1)
                {
                    Debug.LogError($"G3 T06: po cooldownzie bez nowej komendy nie powinno kopnąć (success={success}).");
                    return false;
                }

                ResetBodies(motor, ball, new Vector3(0f, 0.5f, -2f), Quaternion.identity, KickableBallPos);
                controller.SubmitCommand(PlayerId, kick, 602);
                Simulate(controller);
                if (success != 2)
                {
                    Debug.LogError($"G3 T06: po cooldownzie nowa decyzja powinna kopnąć (success={success}).");
                    return false;
                }

                Debug.Log("G3 T06 OK.");
                return true;
            }
            finally
            {
                controller.KickSucceeded -= Handler;
            }
        }

        static bool TestT07_RejectionCases(ArenaController controller)
        {
            PlayerMotor motor = null;
            Rigidbody ball = null;
            if (!PrepareController(ref controller, ref motor, ref ball))
                return false;

            var kick = new PlayerCommand { Kick = 1f, Loft = -1f };

            ResetBodies(motor, ball, new Vector3(0f, 0.5f, -2f), Quaternion.identity, new Vector3(0f, 0.27f, -2.55f));
            controller.SubmitCommand(PlayerId, kick, 701);
            Simulate(controller);
            if (!ExpectFail(controller, KickRejectReason.OutOfCone, "za graczem"))
                return false;

            ResetBodies(motor, ball, new Vector3(0f, 0.5f, -2f), Quaternion.identity, new Vector3(0f, 0.27f, 2f));
            controller.SubmitCommand(PlayerId, kick, 702);
            Simulate(controller);
            if (!ExpectFail(controller, KickRejectReason.OutOfRange, "zasięg"))
                return false;

            ResetBodies(motor, ball, new Vector3(0f, 0.5f, -2f), Quaternion.identity, new Vector3(0f, 1.2f, -0.8f));
            controller.SubmitCommand(PlayerId, kick, 703);
            Simulate(controller);
            if (!ExpectFail(controller, KickRejectReason.TooHigh, "wysokość"))
                return false;

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 0.5f, -1.4f);
            wall.transform.localScale = new Vector3(2f, 1.5f, 0.3f);
            Physics.SyncTransforms();

            ResetBodies(motor, ball, new Vector3(0f, 0.5f, -2f), Quaternion.identity, KickableBallPos);
            controller.SubmitCommand(PlayerId, kick, 704);
            Simulate(controller);
            UnityEngine.Object.DestroyImmediate(wall);

            if (!ExpectFail(controller, KickRejectReason.Occluded, "przesłonięcie"))
                return false;

            Debug.Log("G3 T07 OK.");
            return true;
        }

        static bool ExpectFail(ArenaController controller, KickRejectReason reason, string label)
        {
            if (!controller.TryGetLastKickAttempt(PlayerId, out var attempt) || attempt.Success
                || attempt.Reason != reason)
            {
                Debug.LogError(
                    $"G3 T07 ({label}): oczekiwano {reason}, jest {attempt.Reason} success={attempt.Success} attempted={attempt.Attempted}.");
                return false;
            }

            return true;
        }

        static bool TestT08_StrengthScaling(ArenaController controller)
        {
            PlayerMotor motor = null;
            Rigidbody ball = null;
            if (!PrepareController(ref controller, ref motor, ref ball))
                return false;

            float DeltaSpeed(float kick, long cmdId)
            {
                if (!PrepareController(ref controller, ref motor, ref ball))
                    return 0f;

                ResetBodies(motor, ball, new Vector3(0f, 0.5f, -2f), Quaternion.identity, KickableBallPos);
                var cmd = new PlayerCommand { Kick = kick, Loft = -1f };
                controller.SubmitCommand(PlayerId, cmd, cmdId);
                var before = ball.linearVelocity.magnitude;
                Simulate(controller);
                return ball.linearVelocity.magnitude - before;
            }

            var weak = DeltaSpeed(0.5f, 801);
            var strong = DeltaSpeed(1f, 802);
            if (strong <= weak + 0.05f)
            {
                Debug.LogError($"G3 T08: silniejszy strzał powinien dać większą Δv (weak={weak:F3}, strong={strong:F3}).");
                return false;
            }

            Debug.Log($"G3 T08 OK: Δv weak={weak:F3} m/s, strong={strong:F3} m/s.");
            return true;
        }

        static bool TestT09_LoftAngles(ArenaController controller)
        {
            PlayerMotor motor = null;
            Rigidbody ball = null;
            if (!PrepareController(ref controller, ref motor, ref ball))
                return false;

            Vector3 KickOnce(float loft, long cmdId)
            {
                ResetBodies(motor, ball, new Vector3(0f, 0.5f, -2f), Quaternion.identity, KickableBallPos);
                controller.SubmitCommand(PlayerId, new PlayerCommand { Kick = 1f, Loft = loft }, cmdId);
                Physics.SyncTransforms();
                controller.StepPlayerMotors(Dt);
                controller.StepPlayerKicks();
                return ball.linearVelocity;
            }

            KickSucceededEventArgs? flatEvent = null;
            void OnKick(KickSucceededEventArgs args) => flatEvent = args;
            void AttachHandler()
            {
                controller.KickSucceeded -= OnKick;
                controller.KickSucceeded += OnKick;
            }

            try
            {
                if (!PrepareController(ref controller, ref motor, ref ball))
                    return false;
                AttachHandler();

                var flat = KickOnce(-1f, 901);
                if (flatEvent == null || flatEvent.Value.AngleDeg > 1f)
                {
                    controller.TryGetLastKickAttempt(PlayerId, out var a);
                    Debug.LogError($"G3 T09: brak udanego kopnięcia 0° (angle={flatEvent?.AngleDeg}, reason={a.Reason}).");
                    return false;
                }

                var forward = motor.transform.forward;
                var flatDir = KickMath.BuildImpulseDirection(forward, flatEvent.Value.AngleDeg);
                if (Mathf.Abs(flatDir.y) > 0.05f)
                {
                    Debug.LogError($"G3 T09: kąt 0° — kierunek impulsu bez Y (dirY={flatDir.y:F3}).");
                    return false;
                }

                flatEvent = null;
                if (!PrepareController(ref controller, ref motor, ref ball))
                    return false;
                AttachHandler();

                var loft45 = KickMath.AngleDegToLoft(45f);
                KickOnce(loft45, 902);
                if (flatEvent == null || flatEvent.Value.AngleDeg < 40f || flatEvent.Value.AngleDeg > 50f)
                {
                    controller.TryGetLastKickAttempt(PlayerId, out var a);
                    Debug.LogError(
                        $"G3 T09: brak udanego lobu 45° (angle={flatEvent?.AngleDeg}, reason={a.Reason}).");
                    return false;
                }

                var lobDir = KickMath.BuildImpulseDirection(motor.transform.forward, flatEvent.Value.AngleDeg);
                if (lobDir.y <= 0.05f)
                {
                    Debug.LogError($"G3 T09: kąt 45° — dodatni Y kierunku impulsu (dirY={lobDir.y:F3}).");
                    return false;
                }

                Debug.Log(
                    $"G3 T09 OK: impuls 0° dirY={flatDir.y:F3}, 45° dirY={lobDir.y:F3}, lobAngle={flatEvent.Value.AngleDeg:F1}°.");
                return true;
            }
            finally
            {
                controller.KickSucceeded -= OnKick;
            }
        }
    }
}
#endif
