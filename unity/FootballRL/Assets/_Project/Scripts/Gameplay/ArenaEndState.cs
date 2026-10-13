using FootballRL.Contracts;
using UnityEngine;

namespace FootballRL.Gameplay
{
    /// <summary>Stan końca rundy do odczytu przed resetem (nie pełny snapshot G5).</summary>
    public readonly struct ArenaEndState
    {
        public ArenaPhase Phase { get; }
        public long EpisodeId { get; }
        public RoundEndReason Reason { get; }
        public bool HasScoringTeam { get; }
        public int ScoringTeamId { get; }
        public float ElapsedSeconds { get; }
        public float RemainingSeconds { get; }
        public Vector3 BallPosition { get; }
        public Vector3 BallVelocity { get; }
        public Vector3 PlayerPosition { get; }
        public Vector3 PlayerVelocity { get; }

        public ArenaEndState(
            ArenaPhase phase,
            long episodeId,
            RoundEndReason reason,
            bool hasScoringTeam,
            int scoringTeamId,
            float elapsedSeconds,
            float remainingSeconds,
            Vector3 ballPosition,
            Vector3 ballVelocity,
            Vector3 playerPosition,
            Vector3 playerVelocity)
        {
            Phase = phase;
            EpisodeId = episodeId;
            Reason = reason;
            HasScoringTeam = hasScoringTeam;
            ScoringTeamId = scoringTeamId;
            ElapsedSeconds = elapsedSeconds;
            RemainingSeconds = remainingSeconds;
            BallPosition = ballPosition;
            BallVelocity = ballVelocity;
            PlayerPosition = playerPosition;
            PlayerVelocity = playerVelocity;
        }
    }
}
