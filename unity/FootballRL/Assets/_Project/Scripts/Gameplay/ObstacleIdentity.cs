using UnityEngine;

namespace FootballRL.Gameplay
{
    [DisallowMultipleComponent]
    public class ObstacleIdentity : MonoBehaviour
    {
        [SerializeField] int stableId;

        public int StableId => stableId;

        public void Configure(int id) => stableId = id;
    }
}
