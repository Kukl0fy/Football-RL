using UnityEngine;

namespace FootballRL.Gameplay
{
    /// <summary>Jawny punkt kopnięcia na prefabie gracza (bez FixedUpdate).</summary>
    public class PlayerKickAnchor : MonoBehaviour
    {
        [SerializeField] Transform kickPoint;

        Collider[] _ownerColliders;

        public static readonly Vector3 DefaultLocalKickOffset = new Vector3(0f, 0.15f, 0.45f);

        public Transform KickPoint => kickPoint != null ? kickPoint : transform;

        public Vector3 GetKickWorldPosition()
        {
            if (kickPoint != null && kickPoint != transform)
                return kickPoint.position;

            return transform.TransformPoint(DefaultLocalKickOffset);
        }

        public Collider[] OwnerColliders
        {
            get
            {
                if (_ownerColliders == null || _ownerColliders.Length == 0)
                    _ownerColliders = GetComponentsInChildren<Collider>();
                return _ownerColliders;
            }
        }

        public void SetKickPoint(Transform point) => kickPoint = point;
    }
}
