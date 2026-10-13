using UnityEngine;

namespace FootballRL.Scenarios
{
    public enum ScenarioKind
    {
        ManualSandbox = 0,
        MoveToTarget = 1,
        ShootAtEmptyGoal = 2
    }

    [CreateAssetMenu(fileName = "Scenario", menuName = "FootballRL/Scenario Definition")]
    public class ScenarioDefinition : ScriptableObject
    {
        [Header("Identyfikacja")]
        public string scenarioId = "ManualSandbox";
        public ScenarioKind kind = ScenarioKind.ManualSandbox;

        [Header("Mechaniki")]
        public bool kickingEnabled = true;
        public bool regulateKickAngle = true;
        [Tooltip("Loft przy zablokowanym kącie (0° = −1).")]
        public float lockedLoft = -1f;

        [Header("Czas rundy")]
        [Tooltip("≤ 0 → RoundConfig.roundTimeLimitSeconds")]
        public float roundTimeLimitSeconds = -1f;

        [Header("Spawn")]
        public bool useRandomSpawn;

        [Header("MoveToTarget — losowy cel (lokalnie X/Z)")]
        public float targetRandomAbsX = 4f;
        public float targetRandomMinZ = -6f;
        public float targetRandomMaxZ = 6f;
        public float targetSuccessRadius = 1.2f;

        [Header("Losowy spawn gracza/piłki (lokalnie)")]
        public float randomPlayerAbsX = 3f;
        public float randomPlayerMinZ = -7f;
        public float randomPlayerMaxZ = 7f;

        [Header("ShootAtEmptyGoal — łatwy start (lokalnie)")]
        public Vector3 easyPlayerLocalPosition = new Vector3(0f, 0.5f, -4f);
        public Vector3 easyBallLocalPosition = new Vector3(0f, 0.22f, -2.2f);
        public float easyPlayerYawDeg = 0f;
    }
}
