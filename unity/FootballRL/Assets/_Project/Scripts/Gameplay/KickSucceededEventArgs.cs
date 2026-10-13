using UnityEngine;

namespace FootballRL.Gameplay
{
    public readonly struct KickSucceededEventArgs
    {
        public int PlayerId { get; }
        public PlayerMotor Player { get; }
        public float ImpulseMagnitude { get; }
        public float AngleDeg { get; }

        public KickSucceededEventArgs(int playerId, PlayerMotor player, float impulseMagnitude, float angleDeg)
        {
            PlayerId = playerId;
            Player = player;
            ImpulseMagnitude = impulseMagnitude;
            AngleDeg = angleDeg;
        }
    }
}
