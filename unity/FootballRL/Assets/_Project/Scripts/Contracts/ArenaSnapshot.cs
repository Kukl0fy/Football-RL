using UnityEngine;

namespace FootballRL.Contracts
{
    public struct ArenaSnapshotArena
    {
        public int ContractVersion;
        public long EpisodeId;
        public long Tick;
        public float ElapsedSeconds;
        public float RemainingSeconds;
        public ArenaPhase Phase;
        public float PitchWidthX;
        public float PitchLengthZ;
    }

    public struct ArenaSnapshotBall
    {
        public bool Present;
        public Vector3 LocalPosition;
        public Vector3 LocalVelocity;
        public Vector3 LocalAngularVelocity;
    }

    public struct ArenaSnapshotPlayer
    {
        public int PlayerId;
        public int TeamId;
        public Vector3 LocalPosition;
        public Vector3 LocalForward;
        public Vector3 LocalVelocity;
        public float AngularVelocityY;
        public float CooldownRemainingSeconds;
        public bool CanKickNow;
    }

    public struct ArenaSnapshotGoal
    {
        public int DefendingTeamId;
        public Vector3 LineCenterLocal;
        public Vector3 DirectionLocal;
        public float Width;
        public float Height;
    }

    public struct ArenaSnapshotTarget
    {
        public bool Present;
        public Vector3 LocalPosition;
        public float SuccessRadius;
    }

    public struct ArenaSnapshotObstacle
    {
        public int StableId;
        public Vector3 LocalPosition;
        public Vector3 LocalSize;
        public Quaternion LocalRotation;
    }

    public struct ArenaSnapshotRoundEnd
    {
        public bool HasRoundEnd;
        public RoundEndReason Reason;
        public bool HasScoringTeam;
        public int ScoringTeamId;
    }

    public struct ArenaSnapshot
    {
        public ArenaSnapshotArena Arena;
        public ArenaSnapshotBall Ball;
        public ArenaSnapshotPlayer[] Players;
        public ArenaSnapshotGoal[] Goals;
        public ArenaSnapshotTarget Target;
        public ArenaSnapshotObstacle[] Obstacles;
        public ArenaSnapshotRoundEnd RoundEnd;
    }
}
