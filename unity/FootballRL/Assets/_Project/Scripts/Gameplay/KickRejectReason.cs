namespace FootballRL.Gameplay
{
    public enum KickRejectReason
    {
        None,
        BelowThreshold,
        Cooldown,
        OutOfRange,
        OutOfCone,
        TooHigh,
        Occluded
    }
}
