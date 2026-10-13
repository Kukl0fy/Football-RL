using FootballRL.Contracts;
using UnityEngine;

namespace FootballRL.Gameplay
{
    /// <summary>Ruch i obrót na Rigidbody — wywoływany wyłącznie z kroku fizyki areny.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerMotor : MonoBehaviour
    {
        [SerializeField] MovementConfig movementConfig;

        Rigidbody _body;

        public MovementConfig MovementConfig => movementConfig;

        public void SetMovementConfig(MovementConfig config) => movementConfig = config;

        void Awake()
        {
            EnsureBody();
        }

        void EnsureBody()
        {
            if (_body != null)
                return;

            _body = GetComponent<Rigidbody>();
            ApplyRigidbodyDefaults();
        }

        void ApplyRigidbodyDefaults()
        {
            _body.useGravity = true;
            _body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        /// <summary>Stosuje komendę ruchu/obrotu w jednym kroku fixedDeltaTime.</summary>
        public void ApplyCommand(PlayerCommand command, float deltaTime)
        {
            EnsureBody();
            if (movementConfig == null || deltaTime <= 0f)
                return;

            ApplyTurn(command.Turn, deltaTime);
            ApplyPlanarMovement(command.Forward, command.Strafe, deltaTime);
        }

        void ApplyTurn(float turn, float deltaTime)
        {
            if (Mathf.Approximately(turn, 0f))
                return;

            var angle = turn * movementConfig.turnSpeedDegPerSec * deltaTime;
            var delta = Quaternion.Euler(0f, angle, 0f);
            _body.MoveRotation(_body.rotation * delta);
        }

        void ApplyPlanarMovement(float forward, float strafe, float deltaTime)
        {
            var planarInput = Vector2.ClampMagnitude(new Vector2(strafe, forward), 1f);
            var localDirection = new Vector3(planarInput.x, 0f, planarInput.y);
            var worldTarget = transform.TransformDirection(localDirection) * movementConfig.maxSpeed;

            var velocity = _body.linearVelocity;
            var horizontal = new Vector3(velocity.x, 0f, velocity.z);
            var maxDelta = movementConfig.maxAcceleration * deltaTime;
            var newHorizontal = Vector3.MoveTowards(horizontal, worldTarget, maxDelta);
            _body.linearVelocity = new Vector3(newHorizontal.x, velocity.y, newHorizontal.z);
        }
    }
}
