using UnityEngine;

namespace FootballRL.Gameplay
{
    public static class ArenaBounds
    {
        public static bool IsInvalidState(Vector3 ballCenter, Vector3 playerCenter)
        {
            return !IsFinite(ballCenter) || !IsFinite(playerCenter);
        }

        /// <summary>
        /// Dozwolony obszar: boisko (XZ + promień) oraz wnętrza bramek jako prostopadłościany
        /// (szerokość otworu ±1,5 m, wysokość 2 m, głębokość 1 m za linią Z=±10) z tym samym zapasem promienia co boisko.
        /// </summary>
        public static bool IsBallOutOfPlay(
            Vector3 ballCenter,
            float ballRadius,
            ArenaConfig config)
        {
            if (ballCenter.y < config.emergencyFloorY)
                return true;

            if (IsInsidePitch(ballCenter, ballRadius, config))
                return false;

            if (IsInsideGoalInterior(ballCenter, ballRadius, config, positiveZGoal: true))
                return false;

            if (IsInsideGoalInterior(ballCenter, ballRadius, config, positiveZGoal: false))
                return false;

            return true;
        }

        static bool IsInsidePitch(Vector3 center, float radius, ArenaConfig config)
        {
            var halfX = config.HalfWidthX;
            var halfZ = config.HalfPlayLengthZ;
            return Mathf.Abs(center.x) <= halfX + radius
                   && Mathf.Abs(center.z) <= halfZ + radius;
        }

        static bool IsInsideGoalInterior(Vector3 center, float radius, ArenaConfig config, bool positiveZGoal)
        {
            var halfGoalX = config.HalfGoalWidth;
            var goalHeight = config.goalHeight;
            var halfPlayZ = config.HalfPlayLengthZ;
            var depth = config.goalDepth;

            if (Mathf.Abs(center.x) > halfGoalX + radius)
                return false;

            if (center.y < -radius || center.y > goalHeight + radius)
                return false;

            if (positiveZGoal)
            {
                var minZ = halfPlayZ - radius;
                var maxZ = halfPlayZ + depth + radius;
                return center.z >= minZ && center.z <= maxZ;
            }

            var negMinZ = -halfPlayZ - depth - radius;
            var negMaxZ = -halfPlayZ + radius;
            return center.z >= negMinZ && center.z <= negMaxZ;
        }

        static bool IsFinite(Vector3 v) =>
            float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
