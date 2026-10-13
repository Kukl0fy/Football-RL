using UnityEngine;

namespace FootballRL.Gameplay
{
    public readonly struct PlayerSnapshotEntry
    {
        public int PlayerId { get; }
        public int TeamId { get; }
        public PlayerMotor Motor { get; }
        public Vector3 WorldPosition { get; }
        public Vector3 WorldVelocity { get; }
        public float AngularVelocityY { get; }
        public float CooldownRemaining { get; }
        public bool CanKickNow { get; }

        public PlayerSnapshotEntry(
            int playerId,
            int teamId,
            PlayerMotor motor,
            Vector3 worldPosition,
            Vector3 worldVelocity,
            float angularVelocityY,
            float cooldownRemaining,
            bool canKickNow)
        {
            PlayerId = playerId;
            TeamId = teamId;
            Motor = motor;
            WorldPosition = worldPosition;
            WorldVelocity = worldVelocity;
            AngularVelocityY = angularVelocityY;
            CooldownRemaining = cooldownRemaining;
            CanKickNow = canKickNow;
        }
    }
}
