using UnityEngine;

namespace FootballRL.Gameplay
{
    /// <summary>Korzeń prefabu areny — jawne referencje pod przyszły gameplay (G2+).</summary>
    public class ArenaRoot : MonoBehaviour
    {
        [SerializeField] ArenaConfig arenaConfig;
        [SerializeField] Transform playersRoot;
        [SerializeField] Transform obstaclesRoot;
        [SerializeField] Rigidbody ballBody;
        [SerializeField] Transform ballTransform;

        public ArenaConfig ArenaConfig => arenaConfig;
        public Transform PlayersRoot => playersRoot;
        public Transform ObstaclesRoot => obstaclesRoot;
        public Rigidbody BallBody => ballBody;
        Transform BallTransform => ballTransform;

        void Awake()
        {
            ArenaLayout.Apply(this);
        }

        void OnValidate()
        {
            ArenaLayout.ScheduleApply();
        }

        public void SetReferences(
            ArenaConfig config,
            Transform players,
            Transform obstacles,
            Rigidbody ball,
            Transform ballTf)
        {
            arenaConfig = config;
            playersRoot = players;
            obstaclesRoot = obstacles;
            ballBody = ball;
            ballTransform = ballTf;
        }
    }
}
