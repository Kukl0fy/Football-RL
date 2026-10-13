using FootballRL.Contracts;
using UnityEngine;

namespace FootballRL.Gameplay
{
    /// <summary>Logika kopnięcia — wywoływana z kroku fizyki areny.</summary>
    public static class PlayerKicker
    {
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

            var kickOrigin = anchor != null
                ? anchor.GetKickWorldPosition()
                : motor.transform.TransformPoint(PlayerKickAnchor.DefaultLocalKickOffset);
            var ballCenter = ballBody.worldCenterOfMass;

            if (ballCenter.y - config.groundY > config.maxBallCenterHeight + 0.001f)
                return KickAttemptResult.Fail(playerId, KickRejectReason.TooHigh, angleDeg);

            var toBall = ballCenter - kickOrigin;
            var distance = toBall.magnitude;
            if (distance > config.maxRange + 0.001f)
                return KickAttemptResult.Fail(playerId, KickRejectReason.OutOfRange, angleDeg);

            var forward = motor.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.forward;
            forward.Normalize();

            var toBallPlanar = toBall;
            toBallPlanar.y = 0f;
            if (toBallPlanar.sqrMagnitude < 1e-6f)
                return KickAttemptResult.Fail(playerId, KickRejectReason.OutOfCone, angleDeg);

            toBallPlanar.Normalize();
            var angleToForward = Vector3.Angle(forward, toBallPlanar);
            if (angleToForward > config.coneHalfAngleDeg + 0.01f)
                return KickAttemptResult.Fail(playerId, KickRejectReason.OutOfCone, angleDeg);

            var ownerColliders = anchor != null ? anchor.OwnerColliders : motor.GetComponentsInChildren<Collider>();
            if (IsOccluded(kickOrigin, ballCenter, ownerColliders, ballBody, config))
                return KickAttemptResult.Fail(playerId, KickRejectReason.Occluded, angleDeg);

            var impulseMag = KickMath.ComputeImpulseMagnitude(command.Kick, config);
            if (impulseMag <= 0f)
                return KickAttemptResult.Fail(playerId, KickRejectReason.BelowThreshold, angleDeg);

            var direction = KickMath.BuildImpulseDirection(forward, angleDeg);
            ballBody.WakeUp();
            ballBody.AddForce(direction * impulseMag, ForceMode.Impulse);

            return KickAttemptResult.SuccessResult(playerId, impulseMag, angleDeg);
        }

        static bool IsOccluded(Vector3 origin, Vector3 target, Collider[] ownerColliders, Rigidbody ballBody,
            KickConfig config)
        {
            var dir = target - origin;
            var dist = dir.magnitude;
            if (dist < 1e-4f)
                return false;

            var hits = Physics.RaycastAll(origin, dir / dist, dist, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
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
