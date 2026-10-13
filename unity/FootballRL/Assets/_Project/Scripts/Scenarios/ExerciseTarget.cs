using UnityEngine;

namespace FootballRL.Scenarios
{
    /// <summary>Cel ćwiczenia nawigacyjnego — bez nagrody RL i bez końca rundy.</summary>
    public class ExerciseTarget : MonoBehaviour
    {
        [SerializeField] float successRadius = 1.2f;

        public float SuccessRadius => successRadius;

        public void Configure(float radius)
        {
            successRadius = radius;
        }

        public void SetLocalPosition(Vector3 localPosition)
        {
            transform.localPosition = localPosition;
        }

        public bool IsPlayerInside(Vector3 playerWorldPosition)
        {
            var delta = playerWorldPosition - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= successRadius * successRadius;
        }

        public void SetActiveForScenario(bool active)
        {
            gameObject.SetActive(active);
        }
    }
}
