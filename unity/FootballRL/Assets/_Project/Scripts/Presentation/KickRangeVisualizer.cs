using FootballRL.Gameplay;
using UnityEngine;

namespace FootballRL.Presentation
{
    /// <summary>Podgląd zasięgu 1,1 m i stożka ±45° od punktu kopnięcia.</summary>
    [RequireComponent(typeof(LineRenderer))]
    public class KickRangeVisualizer : MonoBehaviour
    {
        [SerializeField] PlayerKickAnchor kickAnchor;
        [SerializeField] KickConfig kickConfig;
        [SerializeField] int arcSegments = 24;

        LineRenderer _line;

        void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.loop = false;
            _line.widthMultiplier = 0.03f;
            if (_line.sharedMaterial == null)
                _line.material = new Material(Shader.Find("Sprites/Default"));
            _line.startColor = new Color(0.2f, 0.9f, 0.3f, 0.85f);
            _line.endColor = _line.startColor;
        }

        public void Configure(PlayerKickAnchor anchor, KickConfig config)
        {
            kickAnchor = anchor;
            kickConfig = config;
        }

        void LateUpdate()
        {
            if (kickAnchor == null || kickConfig == null || _line == null)
                return;

            var origin = kickAnchor.GetKickWorldPosition();
            var forward = kickAnchor.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.forward;
            forward.Normalize();

            var halfAngle = kickConfig.coneHalfAngleDeg;
            var range = kickConfig.maxRange;
            var pointCount = arcSegments + 3;
            _line.positionCount = pointCount;

            var leftDir = Quaternion.Euler(0f, -halfAngle, 0f) * forward;
            var rightDir = Quaternion.Euler(0f, halfAngle, 0f) * forward;

            _line.SetPosition(0, origin);
            _line.SetPosition(1, origin + leftDir * range);

            for (var i = 0; i <= arcSegments; i++)
            {
                var t = i / (float)arcSegments;
                var angle = Mathf.Lerp(-halfAngle, halfAngle, t);
                var dir = Quaternion.Euler(0f, angle, 0f) * forward;
                _line.SetPosition(i + 2, origin + dir * range);
            }
        }
    }
}
