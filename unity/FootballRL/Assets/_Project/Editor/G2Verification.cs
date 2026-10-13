#if UNITY_EDITOR
using FootballRL.Contracts;
using FootballRL.Gameplay;
using FootballRL.Input;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FootballRL.Editor
{
    public static class G2Verification
    {
        const string ScenePath = "Assets/_Project/Scenes/ManualPlayground.unity";
        const int DefaultPlayerId = 0;
        const float Dt = 0.02f;
        const float SpeedTolerance = 0.35f;

        public static void RunBatch()
        {
            if (!G2ArenaBuilder.RunBuildSilent())
            {
                Debug.LogError("G2 verify: build failed.");
                EditorApplication.Exit(1);
                return;
            }

            if (!RunAllTests())
            {
                Debug.LogError("G2 verify: tests failed.");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("G2 verification completed successfully.");
            EditorApplication.Exit(0);
        }

        public static bool RunAllTests()
        {
            if (!RunKeyboardMappingTests())
                return false;

            return RunMovementPhysicsTests();
        }

        static bool RunKeyboardMappingTests()
        {
            var wOnly = KeyboardCommandMapping.FromKeyStates(true, false, false, false, false, false);
            if (wOnly.Forward <= 0f)
            {
                Debug.LogError("G2 map: W powinno dać Forward > 0.");
                return false;
            }

            var aOnly = KeyboardCommandMapping.FromKeyStates(false, false, true, false, false, false);
            if (aOnly.Strafe >= 0f)
            {
                Debug.LogError("G2 map: A powinno dać Strafe < 0 (lewo).");
                return false;
            }

            var eOnly = KeyboardCommandMapping.FromKeyStates(false, false, false, false, false, true);
            if (eOnly.Turn <= 0f)
            {
                Debug.LogError("G2 map: E powinno dać Turn > 0 (prawo).");
                return false;
            }

            var none = KeyboardCommandMapping.FromKeyStates(false, false, false, false, false, false);
            if (!Mathf.Approximately(none.Forward, 0f) || !Mathf.Approximately(none.Strafe, 0f)
                || !Mathf.Approximately(none.Turn, 0f))
            {
                Debug.LogError("G2 map: brak klawiszy powinien dać zerowy ruch.");
                return false;
            }

            Debug.Log("G2 map OK.");
            return true;
        }

        static bool RunMovementPhysicsTests()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("G2 physics: invalid scene.");
                return false;
            }

            var controller = Object.FindFirstObjectByType<ArenaController>();
            if (controller == null)
            {
                Debug.LogError("G2 physics: brak ArenaController.");
                return false;
            }

            controller.RefreshPlayerRegistry();
            if (controller.MovementConfig == null)
            {
                Debug.LogError("G2 physics: brak MovementConfig na ArenaController.");
                return false;
            }

            var motor = FindPlayerMotor(controller);
            if (motor == null)
            {
                Debug.LogError("G2 physics: brak PlayerMotor.");
                return false;
            }

            motor.SetMovementConfig(controller.MovementConfig);

            Rigidbody ballRb = null;

            var prevSim = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;

            try
            {
                if (!TestStraightSpeed(ref controller, ref motor, ref ballRb))
                    return false;
                if (!TestDiagonalSpeed(ref controller, ref motor, ref ballRb))
                    return false;
                if (!TestForwardAfterTurn(ref controller, ref motor, ref ballRb))
                    return false;
                if (!TestZeroDoesNotAccelerate(ref controller, ref motor, ref ballRb))
                    return false;
                if (!TestDecelerationAfterRelease(ref controller, ref motor, ref ballRb))
                    return false;
                if (!TestBallPush(ref controller, ref motor, ref ballRb))
                    return false;

                Debug.Log("G2 physics OK.");
                return true;
            }
            finally
            {
                Physics.simulationMode = prevSim;
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }

        static PlayerMotor FindPlayerMotor(ArenaController controller)
        {
            var identity = Object.FindFirstObjectByType<PlayerIdentity>();
            if (identity == null)
                return null;
            return identity.GetComponent<PlayerMotor>();
        }

        static ArenaController ReloadArenaController()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var controller = Object.FindAnyObjectByType<ArenaController>();
            controller?.RefreshPlayerRegistry();
            return controller;
        }

        static void ResetPlayer(PlayerMotor motor, Vector3 position, Quaternion rotation)
        {
            var rb = motor.GetComponent<Rigidbody>();
            rb.position = position;
            rb.rotation = rotation;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.WakeUp();
            Physics.SyncTransforms();
        }

        static void MoveBallAside(Rigidbody ballRb)
        {
            if (ballRb == null)
                return;

            ballRb.position = new Vector3(4f, 0.27f, 4f);
            ballRb.linearVelocity = Vector3.zero;
            ballRb.angularVelocity = Vector3.zero;
            ballRb.WakeUp();
            Physics.SyncTransforms();
        }

        static void SimulateStep(ArenaController controller)
        {
            controller.StepPlayerMotors(Dt);
            Physics.Simulate(Dt);
        }

        static float HorizontalSpeed(Rigidbody rb)
        {
            var v = rb.linearVelocity;
            return new Vector3(v.x, 0f, v.z).magnitude;
        }

        static bool PrepareTest(ref ArenaController controller, ref PlayerMotor motor, ref Rigidbody ballRb)
        {
            controller = ReloadArenaController();
            if (controller == null)
            {
                Debug.LogError("G2 physics: brak ArenaController po przeładowaniu sceny.");
                return false;
            }

            motor = FindPlayerMotor(controller);
            if (motor == null)
            {
                Debug.LogError("G2 physics: brak PlayerMotor po przeładowaniu sceny.");
                return false;
            }

            ballRb = Object.FindAnyObjectByType<BallPhysicsSetup>()?.GetComponent<Rigidbody>();
            if (ballRb == null)
            {
                Debug.LogError("G2 physics: brak piłki po przeładowaniu sceny.");
                return false;
            }

            motor.SetMovementConfig(controller.MovementConfig);
            MoveBallAside(ballRb);
            return true;
        }

        static bool TestStraightSpeed(ref ArenaController controller, ref PlayerMotor motor, ref Rigidbody ballRb)
        {
            if (!PrepareTest(ref controller, ref motor, ref ballRb))
                return false;

            ResetPlayer(motor, new Vector3(-2f, 0.5f, -4f), Quaternion.identity);
            var forward = new PlayerCommand { Forward = 1f, Strafe = 0f, Turn = 0f, Kick = 0f, Loft = -1f };
            controller.SubmitCommand(DefaultPlayerId, forward, 1);

            for (var i = 0; i < 120; i++)
                SimulateStep(controller);

            var speed = HorizontalSpeed(motor.GetComponent<Rigidbody>());
            var expected = controller.MovementConfig.maxSpeed;
            if (Mathf.Abs(speed - expected) > SpeedTolerance)
            {
                Debug.LogError($"G2 speed straight: oczekiwano ~{expected:F2} m/s, jest {speed:F2} m/s.");
                return false;
            }

            Debug.Log($"G2 speed straight OK: {speed:F2} m/s (tolerancja ±{SpeedTolerance}).");
            return true;
        }

        static bool TestDiagonalSpeed(ref ArenaController controller, ref PlayerMotor motor, ref Rigidbody ballRb)
        {
            if (!PrepareTest(ref controller, ref motor, ref ballRb))
                return false;

            ResetPlayer(motor, new Vector3(-2f, 0.5f, -4f), Quaternion.identity);
            var diag = new PlayerCommand { Forward = 1f, Strafe = 1f, Turn = 0f, Kick = 0f, Loft = -1f };
            controller.SubmitCommand(DefaultPlayerId, diag, 2);

            for (var i = 0; i < 120; i++)
                SimulateStep(controller);

            var speed = HorizontalSpeed(motor.GetComponent<Rigidbody>());
            var expected = controller.MovementConfig.maxSpeed;
            if (Mathf.Abs(speed - expected) > SpeedTolerance)
            {
                Debug.LogError($"G2 speed diagonal: oczekiwano ~{expected:F2} m/s, jest {speed:F2} m/s.");
                return false;
            }

            Debug.Log($"G2 speed diagonal OK: {speed:F2} m/s.");
            return true;
        }

        static bool TestForwardAfterTurn(ref ArenaController controller, ref PlayerMotor motor, ref Rigidbody ballRb)
        {
            if (!PrepareTest(ref controller, ref motor, ref ballRb))
                return false;

            ResetPlayer(motor, new Vector3(0f, 0.5f, -5f), Quaternion.identity);
            var turn = new PlayerCommand { Forward = 0f, Strafe = 0f, Turn = 1f, Kick = 0f, Loft = -1f };
            controller.SubmitCommand(DefaultPlayerId, turn, 3);

            for (var i = 0; i < 25; i++)
                SimulateStep(controller);

            var forwardCmd = new PlayerCommand { Forward = 1f, Strafe = 0f, Turn = 0f, Kick = 0f, Loft = -1f };
            controller.SubmitCommand(DefaultPlayerId, forwardCmd, 4);
            for (var i = 0; i < 80; i++)
                SimulateStep(controller);

            var rb = motor.GetComponent<Rigidbody>();
            var vel = rb.linearVelocity;
            var planar = new Vector3(vel.x, 0f, vel.z);
            if (planar.sqrMagnitude < 0.5f)
            {
                Debug.LogError("G2 turn: za mała prędkość po obrocie.");
                return false;
            }

            var facing = motor.transform.forward;
            facing.y = 0f;
            planar.Normalize();
            facing.Normalize();
            var dot = Vector3.Dot(planar, facing);
            if (dot < 0.85f)
            {
                Debug.LogError($"G2 turn: ruch nie idzie w przód gracza (dot={dot:F2}).");
                return false;
            }

            var dotWorldZ = Vector3.Dot(planar, Vector3.forward);
            if (Mathf.Abs(dotWorldZ) > 0.85f)
            {
                Debug.LogError("G2 turn: ruch nadal w światowym ±Z zamiast w lokalny przód.");
                return false;
            }

            Debug.Log($"G2 turn OK: dot(local forward)={dot:F2}, dot(world Z)={dotWorldZ:F2}.");
            return true;
        }

        static bool TestZeroDoesNotAccelerate(ref ArenaController controller, ref PlayerMotor motor, ref Rigidbody ballRb)
        {
            if (!PrepareTest(ref controller, ref motor, ref ballRb))
                return false;

            ResetPlayer(motor, new Vector3(0f, 0.5f, -2f), Quaternion.identity);
            controller.SubmitCommand(DefaultPlayerId, PlayerCommand.Zero, 10);
            for (var i = 0; i < 10; i++)
                SimulateStep(controller);

            var speed = HorizontalSpeed(motor.GetComponent<Rigidbody>());
            if (speed > 0.15f)
            {
                Debug.LogError($"G2 zero: spoczynek przy komendzie zerowej — prędkość {speed:F3} m/s.");
                return false;
            }

            Debug.Log("G2 zero idle OK.");
            return true;
        }

        static bool TestDecelerationAfterRelease(ref ArenaController controller, ref PlayerMotor motor, ref Rigidbody ballRb)
        {
            if (!PrepareTest(ref controller, ref motor, ref ballRb))
                return false;

            ResetPlayer(motor, new Vector3(-1f, 0.5f, -3f), Quaternion.identity);
            var move = new PlayerCommand { Forward = 1f, Strafe = 0f, Turn = 0f, Kick = 0f, Loft = -1f };
            controller.SubmitCommand(DefaultPlayerId, move, 20);
            for (var i = 0; i < 80; i++)
                SimulateStep(controller);

            var rb = motor.GetComponent<Rigidbody>();
            var speedBeforeZero = HorizontalSpeed(rb);
            if (speedBeforeZero < 1f)
            {
                Debug.LogError("G2 decel: gracz nie rozpędził się przed testem hamowania.");
                return false;
            }

            controller.SubmitCommand(DefaultPlayerId, PlayerCommand.Zero, 21);
            SimulateStep(controller);
            var speedAfterOne = HorizontalSpeed(rb);
            if (speedAfterOne <= 0.01f)
            {
                Debug.LogError("G2 decel: prędkość wyzerowana natychmiast po komendzie zerowej.");
                return false;
            }

            if (speedAfterOne >= speedBeforeZero)
            {
                Debug.LogError("G2 decel: brak spadku prędkości po komendzie zerowej.");
                return false;
            }

            Debug.Log($"G2 decel OK: {speedBeforeZero:F2} → {speedAfterOne:F2} m/s po 1 kroku.");
            return true;
        }

        static bool TestBallPush(ref ArenaController controller, ref PlayerMotor motor, ref Rigidbody ballRb)
        {
            if (!PrepareTest(ref controller, ref motor, ref ballRb))
                return false;

            ResetPlayer(motor, new Vector3(0f, 0.5f, -1.2f), Quaternion.identity);
            ballRb.position = new Vector3(0f, 0.27f, 0.5f);
            ballRb.linearVelocity = Vector3.zero;
            ballRb.angularVelocity = Vector3.zero;
            ballRb.WakeUp();

            var ballStartZ = ballRb.position.z;
            var move = new PlayerCommand { Forward = 1f, Strafe = 0f, Turn = 0f, Kick = 0f, Loft = -1f };
            controller.SubmitCommand(DefaultPlayerId, move, 30);

            for (var i = 0; i < 100; i++)
                SimulateStep(controller);

            var dz = ballRb.position.z - ballStartZ;
            if (dz < 0.05f)
            {
                Debug.LogError($"G2 push: piłka prawie się nie ruszyła (Δz={dz:F3}).");
                return false;
            }

            Debug.Log($"G2 push OK: piłka Δz={dz:F3} m.");
            return true;
        }
    }
}
#endif
