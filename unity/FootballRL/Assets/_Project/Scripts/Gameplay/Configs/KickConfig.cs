using UnityEngine;

namespace FootballRL.Gameplay
{
    [CreateAssetMenu(fileName = "KickConfig", menuName = "FootballRL/Kick Config")]
    public class KickConfig : ScriptableObject
    {
        [Tooltip("Kick ≤ próg → brak próby kopnięcia.")]
        public float kickThreshold = 0.2f;

        [Tooltip("Maksymalny impuls (N·s) przy Kick=1.")]
        public float maxImpulse = 8f;

        [Tooltip("Cooldown po udanym kopnięciu (s).")]
        public float cooldownSeconds = 0.25f;

        [Tooltip("Maks. odległość środka piłki od punktu kopnięcia (m).")]
        public float maxRange = 1.1f;

        [Tooltip("Połowa kąta stożka względem przodu gracza (°).")]
        public float coneHalfAngleDeg = 45f;

        [Tooltip("Maks. wysokość środka piłki nad podłożem (m).")]
        public float maxBallCenterHeight = 0.5f;

        [Tooltip("Wysokość podłoża boiska (Y).")]
        public float groundY = 0f;

        public const float LoftScaleDeg = 27.5f;
    }
}
