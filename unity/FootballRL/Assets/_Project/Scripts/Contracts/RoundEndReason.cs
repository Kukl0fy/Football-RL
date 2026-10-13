namespace FootballRL.Contracts
{
    public enum RoundEndReason
    {
        None = 0,
        Goal = 1,
        BallOut = 2,
        TimeLimit = 3,
        InvalidState = 4,
        ManualReset = 5
    }
}
