using UnityEngine;

namespace FootballRL.Gameplay
{
    [CreateAssetMenu(fileName = "RoundConfig", menuName = "FootballRL/Round Config")]
    public class RoundConfig : ScriptableObject
    {
        [Tooltip("Limit czasu rundy w Running (s).")]
        public float roundTimeLimitSeconds = 60f;

        [Tooltip("Pauza przed automatycznym resetem po Ended (s).")]
        public float autoResetDelaySeconds = 1.5f;

        [Tooltip("Po Ended automatycznie wywołaj ResetEpisode po pauzie.")]
        public bool autoResetEnabled = true;
    }
}
