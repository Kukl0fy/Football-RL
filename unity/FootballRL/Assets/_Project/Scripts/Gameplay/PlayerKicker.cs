using FootballRL.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FootballRL.Gameplay
{
    /// <summary>Logika kopnięcia — wywoływana z kroku fizyki areny.</summary>
    public static class PlayerKicker
    {
        /// <summary>Geometria + cooldown (bez progu siły Kick).</summary>
        public static bool CanKickNow(
            PlayerMotor motor,
            PlayerKickAnchor anchor,
            Rigidbody ballBody,
            float cooldownRemaining,
            KickConfig config)
        {
            if (config == null || motor == null || ballBody == null)
                return false;

            if (cooldownRemaining > 0f)
                return false;

            return PassesKickGeometry(motor, anchor, ballBody, config, out _);
        }

        public static KickAttemptResult TryKick(
            int playerId,
            PlayerMotor motor,
            PlayerKickAnchor anchor,
            Rigidbody ballBody,
            PlayerCommand command,
            float cooldownRemaining,
            KickConfig config)
        {
            if (config == null || motor == null || ballBody == null)
                return KickAttemptResult.NotAttempted;

            if (command.Kick <= config.kickThreshold)
                return KickAttemptResult.NotAttempted;

            var angleDeg = KickMath.LoftToAngleDeg(command.Loft);

            if (cooldownRemaining > 0f)
                return KickAttemptResult.Fail(playerId, KickRejectReason.Cooldown, angleDeg);

            if (!PassesKickGeometry(motor, anchor, ballBody, config, out var rejectReason))
                return KickAttemptResult.Fail(playerId, rejectReason, angleDeg);

            var impulseMag = KickMath.ComputeImpulseMagnitude(command.Kick, config);
            if (impulseMag <= 0f)
                return KickAttemptResult.Fail(playerId, KickRejectReason.BelowThreshold, angleDeg);

            var forward = motor.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.forward;
            forward.Normalize();

            var direction = KickMath.BuildImpulseDirection(forward, angleDeg);
            ballBody.WakeUp();
            ballBody.AddForce(direction * impulseMag, ForceMode.Impulse);

            return KickAttemptResult.SuccessResult(playerId, impulseMag, angleDeg);
        }

        static bool PassesKickGeometry(
            PlayerMotor motor,
            PlayerKickAnchor anchor,
            Rigidbody ballBody,
            KickConfig config,
            out KickRejectReason rejectReason)
        {
            rejectReason = KickRejectReason.BelowThreshold;

            var kickOrigin = anchor != null
                ? anchor.GetKickWorldPosition()
                : motor.transform.TransformPoint(PlayerKickAnchor.DefaultLocalKickOffset);
            var ballCenter = ballBody.worldCenterOfMass;

            if (ballCenter.y - config.groundY > config.maxBallCenterHeight + 0.001f)
            {
                rejectReason = KickRejectReason.TooHigh;
                return false;
            }

            var toBall = ballCenter - kickOrigin;
            if (toBall.magnitude > config.maxRange + 0.001f)
            {
                rejectReason = KickRejectReason.OutOfRange;
                return false;
            }

            var forward = motor.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.forward;
            forward.Normalize();

            var toBallPlanar = toBall;
            toBallPlanar.y = 0f;
            if (toBallPlanar.sqrMagnitude < 1e-6f)
            {
                rejectReason = KickRejectReason.OutOfCone;
                return false;
            }

            toBallPlanar.Normalize();
            if (Vector3.Angle(forward, toBallPlanar) > config.coneHalfAngleDeg + 0.01f)
            {
                rejectReason = KickRejectReason.OutOfCone;
                return false;
            }

            var ownerColliders = anchor != null ? anchor.OwnerColliders : motor.GetComponentsInChildren<Collider>();
            if (IsOccluded(kickOrigin, ballCenter, ownerColliders, ballBody, config))
            {
                rejectReason = KickRejectReason.Occluded;
                return false;
            }

            return true;
        }

        static bool IsOccluded(Vector3 origin, Vector3 target, Collider[] ownerColliders, Rigidbody ballBody,
            KickConfig config)
        {
            var dir = target - origin;
            var dist = dir.magnitude;
            if (dist < 1e-4f)
                return false;

            var direction = dir / dist;
            var layerMask = Physics.DefaultRaycastLayers;
            var query = QueryTriggerInteraction.Ignore;
            var hitBuffer = new RaycastHit[32];
            var hitCount = 0;

            var physicsScene = ballBody != null ? ballBody.gameObject.scene.GetPhysicsScene() : default;
            if (physicsScene.IsValid())
                hitCount = physicsScene.Raycast(origin, direction, hitBuffer, dist, layerMask, query);
            else
            {
                var globalHits = Physics.RaycastAll(origin, direction, dist, layerMask, query);
                hitCount = System.Math.Min(globalHits.Length, hitBuffer.Length);
                for (var i = 0; i < hitCount; i++)
                    hitBuffer[i] = globalHits[i];
            }

            System.Array.Sort(hitBuffer, 0, hitCount, System.Collections.Generic.Comparer<RaycastHit>.Create(
                (a, b) => a.distance.CompareTo(b.distance)));

            for (var i = 0; i < hitCount; i++)
            {
                var hit = hitBuffer[i];
                if (hit.rigidbody == ballBody)
                    return false;

                if (IsOwnerCollider(hit.collider, ownerColliders))
                    continue;

                if (IsPitchLikeSurface(hit.collider, config))
                    continue;

                return true;
            }

            return false;
        }

        static bool IsPitchLikeSurface(Collider col, KickConfig config)
        {
            if (col == null || config == null)
                return false;

            var bounds = col.bounds;
            if (bounds.max.y > config.groundY + 0.12f)
                return false;

            return bounds.size.x >= 2f || bounds.size.z >= 2f;
        }

        static bool IsOwnerCollider(Collider col, Collider[] ownerColliders)
        {
            if (col == null || ownerColliders == null)
                return false;

            foreach (var owner in ownerColliders)
            {
                if (owner != null && owner == col)
                    return true;
            }

            return false;
        }
    }
}
