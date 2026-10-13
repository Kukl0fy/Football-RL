using FootballRL.Contracts;

namespace FootballRL.Gameplay
{
    public readonly struct RoundEndedEventArgs
    {
        public long EpisodeId { get; }
        public RoundEndReason Reason { get; }
        public bool HasScoringTeam { get; }
        public int ScoringTeamId { get; }
        public float ElapsedSeconds { get; }

        public RoundEndedEventArgs(long episodeId, RoundEndReason reason, bool hasScoringTeam, int scoringTeamId,
            float elapsedSeconds)
        {
            EpisodeId = episodeId;
            Reason = reason;
            HasScoringTeam = hasScoringTeam;
            ScoringTeamId = scoringTeamId;
            ElapsedSeconds = elapsedSeconds;
        }
    }
}
