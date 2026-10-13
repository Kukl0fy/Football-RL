namespace FootballRL.Gameplay
{
    public struct KickAttemptResult
    {
        public bool Attempted;
        public bool Success;
        public KickRejectReason Reason;
        public float ImpulseMagnitude;
        public float AngleDeg;
        public int PlayerId;

        public static KickAttemptResult NotAttempted => new KickAttemptResult { Attempted = false, Reason = KickRejectReason.BelowThreshold };

        public static KickAttemptResult SuccessResult(int playerId, float impulse, float angleDeg) =>
            new KickAttemptResult
            {
                Attempted = true,
                Success = true,
                Reason = KickRejectReason.None,
                ImpulseMagnitude = impulse,
                AngleDeg = angleDeg,
                PlayerId = playerId
            };

        public static KickAttemptResult Fail(int playerId, KickRejectReason reason, float angleDeg = 0f) =>
            new KickAttemptResult
            {
                Attempted = true,
                Success = false,
                Reason = reason,
                PlayerId = playerId,
                AngleDeg = angleDeg
            };
    }
}
