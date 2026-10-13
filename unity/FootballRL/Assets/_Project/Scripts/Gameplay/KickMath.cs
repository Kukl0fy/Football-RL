using UnityEngine;

namespace FootballRL.Gameplay
{
    public static class KickMath
    {
        public static float LoftToAngleDeg(float loft) => KickConfig.LoftScaleDeg * (loft + 1f);

        public static float AngleDegToLoft(float angleDeg) => angleDeg / KickConfig.LoftScaleDeg - 1f;

        public static float ComputeImpulseMagnitude(float kick, KickConfig config)
        {
            if (config == null || kick <= config.kickThreshold)
                return 0f;

            var power = (kick - config.kickThreshold) / (1f - config.kickThreshold);
            return config.maxImpulse * power;
        }

        public static Vector3 BuildImpulseDirection(Vector3 horizontalForward, float angleDeg)
        {
            var forward = horizontalForward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.forward;

            forward.Normalize();
            var rad = angleDeg * Mathf.Deg2Rad;
            return forward * Mathf.Cos(rad) + Vector3.up * Mathf.Sin(rad);
        }
    }
}
