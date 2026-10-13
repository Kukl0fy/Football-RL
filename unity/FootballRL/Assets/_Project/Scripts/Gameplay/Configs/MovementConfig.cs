using UnityEngine;

namespace FootballRL.Gameplay
{
    [CreateAssetMenu(fileName = "MovementConfig", menuName = "FootballRL/Movement Config")]
    public class MovementConfig : ScriptableObject
    {
        [Tooltip("Docelowa prędkość pozioma (m/s).")]
        public float maxSpeed = 4f;

        [Tooltip("Maks. zmiana prędkości poziomej (m/s²).")]
        public float maxAcceleration = 20f;

        [Tooltip("Prędkość obrotu wokół Y (stopnie/s); Turn=1 → ta wartość w prawo.")]
        public float turnSpeedDegPerSec = 180f;
    }
}
