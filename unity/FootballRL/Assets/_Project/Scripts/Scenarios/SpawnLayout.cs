using UnityEngine;

namespace FootballRL.Scenarios
{
    public struct SpawnLayout
    {
        public bool hasTarget;
        public Vector3 targetLocalPosition;
        public float targetSuccessRadius;
        public Vector3 playerLocalPosition;
        public Quaternion playerLocalRotation;
        public Vector3 ballLocalPosition;
        public Quaternion ballLocalRotation;
    }
}
