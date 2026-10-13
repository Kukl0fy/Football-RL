#if UNITY_EDITOR
using FootballRL.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FootballRL.Editor
{
    /// <summary>Krótki test fizyki w edytorze (batchmode). Nie wymaga Play Mode.</summary>
    public static class G1Verification
    {
        const string ScenePath = "Assets/_Project/Scenes/ManualPlayground.unity";

        public static void RunBatch()
        {
            if (!G1ArenaBuilder.RunBuildSilent())
            {
                Debug.LogError("G1 verify: build failed.");
                EditorApplication.Exit(1);
                return;
            }

            if (!RunPhysicsSmoke())
            {
                Debug.LogError("G1 verify: physics smoke failed.");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("G1 verification completed successfully.");
            EditorApplication.Exit(0);
        }

        public static bool RunPhysicsSmoke()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("G1 smoke: invalid scene.");
                return false;
            }

            var ballSetup = Object.FindFirstObjectByType<BallPhysicsSetup>();
            if (ballSetup == null)
            {
                Debug.LogError("G1 smoke: brak BallPhysicsSetup.");
                return false;
            }

            var rb = ballSetup.GetComponent<Rigidbody>();
            if (rb == null)
            {
                Debug.LogError("G1 smoke: brak Rigidbody piłki.");
                return false;
            }

            rb.WakeUp();
            var startY = rb.position.y;
            const int steps = 120;
            const float dt = 0.02f;
            var prevSim = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;

            try
            {
                for (var i = 0; i < steps; i++)
                    Physics.Simulate(dt);

                var y = rb.position.y;
                if (y < -0.5f)
                {
                    Debug.LogError($"G1 smoke: piłka spadła pod podłogę (y={y:F3}).");
                    return false;
                }

                if (y >= startY - 0.001f && steps > 30)
                {
                    Debug.LogWarning($"G1 smoke: piłka nie spadła wyraźnie (start={startY:F3}, end={y:F3}).");
                }

                // Odbicie: nadaj impuls w stronę ściany +Z i sprawdź ruch.
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = new Vector3(0f, 0.5f, 0f);
                rb.WakeUp();
                rb.AddForce(new Vector3(0f, 0f, 8f), ForceMode.VelocityChange);

                for (var i = 0; i < 80; i++)
                    Physics.Simulate(dt);

                if (rb.position.z < 2f)
                {
                    Debug.LogError($"G1 smoke: piłka nie poruszyła się w kierunku ściany (z={rb.position.z:F3}).");
                    return false;
                }

                Debug.Log($"G1 smoke OK: piłka y={y:F3} po spadku, z={rb.position.z:F3} po impulsie.");
                return true;
            }
            finally
            {
                Physics.simulationMode = prevSim;
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
        }
    }
}
#endif
