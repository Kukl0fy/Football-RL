using UnityEngine;

namespace FootballRL.Gameplay
{
    /// <summary>Fizyka piłki i parametry materiałów kontaktu (startowe, nie pomiar futbolu).</summary>
    [CreateAssetMenu(fileName = "BallPhysicsConfig", menuName = "FootballRL/Ball Physics Config")]
    public class BallPhysicsConfig : ScriptableObject
    {
        [Header("Rigidbody")]
        public float mass = 0.45f;
        public float linearDamping = 0.08f;
        public float angularDamping = 0.12f;

        [Header("Piłka — PhysicMaterial")]
        public float ballDynamicFriction = 0.35f;
        public float ballStaticFriction = 0.35f;
        public float ballBounciness = 0.55f;
        public PhysicsMaterialCombine ballFrictionCombine = PhysicsMaterialCombine.Average;
        public PhysicsMaterialCombine ballBounceCombine = PhysicsMaterialCombine.Average;

        [Header("Podłoże — PhysicMaterial")]
        public float pitchDynamicFriction = 0.45f;
        public float pitchStaticFriction = 0.45f;
        public float pitchBounciness = 0.05f;

        [Header("Ściany — PhysicMaterial")]
        public float wallDynamicFriction = 0.25f;
        public float wallStaticFriction = 0.25f;
        public float wallBounciness = 0.65f;

        [Header("Bramka (rama) — PhysicMaterial")]
        public float goalFrameDynamicFriction = 0.3f;
        public float goalFrameStaticFriction = 0.3f;
        public float goalFrameBounciness = 0.4f;
    }
}
