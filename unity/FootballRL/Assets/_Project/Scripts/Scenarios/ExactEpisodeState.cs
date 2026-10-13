using System;
using UnityEngine;

namespace FootballRL.Scenarios
{
    [Serializable]
    public struct ExactEpisodeState
    {
        public bool hasBall;
        public Vector3 ballLocalPosition;
        public Quaternion ballLocalRotation;
        public bool hasPlayer;
        public int playerId;
        public Vector3 playerLocalPosition;
        public Quaternion playerLocalRotation;
        public bool hasTarget;
        public Vector3 targetLocalPosition;
    }
}
