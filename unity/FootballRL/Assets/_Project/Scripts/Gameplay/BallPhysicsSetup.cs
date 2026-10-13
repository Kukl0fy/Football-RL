using UnityEngine;

namespace FootballRL.Gameplay
{
    /// <summary>Stosuje <see cref="BallPhysicsConfig"/> do Rigidbody piłki przy starcie.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BallPhysicsSetup : MonoBehaviour
    {
        [SerializeField] BallPhysicsConfig config;
        [SerializeField] PhysicsMaterial ballPhysicsMaterial;

        public BallPhysicsConfig Config => config;

        public void Configure(BallPhysicsConfig physicsConfig, PhysicsMaterial sharedBallMaterial)
        {
            config = physicsConfig;
            ballPhysicsMaterial = sharedBallMaterial;
            Apply();
        }

        void Awake()
        {
            Apply();
        }

        void Apply()
        {
            if (config == null)
                return;

            var rb = GetComponent<Rigidbody>();
            rb.mass = config.mass;
            rb.linearDamping = config.linearDamping;
            rb.angularDamping = config.angularDamping;
            rb.useGravity = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var col = GetComponent<SphereCollider>();
            if (col != null && ballPhysicsMaterial != null)
                col.sharedMaterial = ballPhysicsMaterial;
        }
    }
}
