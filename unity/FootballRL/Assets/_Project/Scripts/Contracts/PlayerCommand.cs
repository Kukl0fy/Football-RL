using System;
using UnityEngine;

namespace FootballRL.Contracts
{
    /// <summary>Wspólne wejście gracza (−1…1). Kick/Loft w G2 nie wywołują kopnięcia.</summary>
    [Serializable]
    public struct PlayerCommand : IEquatable<PlayerCommand>
    {
        public float Forward;
        public float Strafe;
        public float Turn;
        public float Kick;
        public float Loft;

        public static PlayerCommand Zero => new PlayerCommand
        {
            Forward = 0f,
            Strafe = 0f,
            Turn = 0f,
            Kick = 0f,
            Loft = -1f
        };

        public PlayerCommand WithZeroMovement()
        {
            Forward = 0f;
            Strafe = 0f;
            Turn = 0f;
            return this;
        }

        public bool Equals(PlayerCommand other)
        {
            return Forward.Equals(other.Forward)
                   && Strafe.Equals(other.Strafe)
                   && Turn.Equals(other.Turn)
                   && Kick.Equals(other.Kick)
                   && Loft.Equals(other.Loft);
        }

        public override bool Equals(object obj) => obj is PlayerCommand other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Forward, Strafe, Turn, Kick, Loft);
    }
}
