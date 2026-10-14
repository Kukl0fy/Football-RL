using UnityEngine;

namespace FootballRL.Gameplay
{
    /// <summary>Przejście całej piłki przez linię bramkową (segment poprzednia→aktualna).</summary>
    public static class GoalDetector
    {
        const float HeightBoundaryEpsilon = 0.05f;

        public static bool TryDetectGoal(
            Vector3 previousCenter,
            Vector3 currentCenter,
            float ballRadius,
            float halfGoalWidth,
            float goalHeight,
            GoalDefinition[] goals,
            out int scoringTeamId)
        {
            scoringTeamId = 0;
            if (goals == null || goals.Length == 0)
                return false;

            var maxX = halfGoalWidth - ballRadius;
            var minY = ballRadius;
            var maxY = goalHeight - ballRadius;

            foreach (var goal in goals)
            {
                if (TryCrossGoalPlane(previousCenter, currentCenter, ballRadius, goal.goalLineZ, goal.enterDirectionZ,
                        maxX, minY, maxY, out var atCross))
                {
                    scoringTeamId = OpponentTeam(goal.defendingTeamId);
                    return true;
                }
            }

            return false;
        }

        static int OpponentTeam(int defendingTeamId) => defendingTeamId == 0 ? 1 : 0;

        static bool TryCrossGoalPlane(
            Vector3 prev,
            Vector3 curr,
            float radius,
            float lineZ,
            float enterDirectionZ,
            float maxAbsX,
            float minY,
            float maxY,
            out Vector3 atCross)
        {
            atCross = default;
            var dir = enterDirectionZ >= 0f ? 1f : -1f;
            var fullyPast = lineZ + dir * radius;

            var prevPast = dir > 0f ? prev.z >= fullyPast : prev.z <= fullyPast;
            if (prevPast)
                return false;

            var currPast = dir > 0f ? curr.z >= fullyPast : curr.z <= fullyPast;
            if (!currPast)
                return false;

            if (!TrySampleCrossingPoint(prev, curr, fullyPast, out atCross))
                return false;

            if (Mathf.Abs(atCross.x) > maxAbsX)
                return false;

            if (atCross.y < minY - HeightBoundaryEpsilon || atCross.y > maxY + HeightBoundaryEpsilon)
                return false;

            return true;
        }

        static bool TrySampleCrossingPoint(Vector3 prev, Vector3 curr, float planeZ, out Vector3 point)
        {
            point = default;
            var dz = curr.z - prev.z;
            if (Mathf.Abs(dz) < 1e-8f)
                return false;

            var t = (planeZ - prev.z) / dz;
            if (t < 0f || t > 1f)
                return false;

            point = Vector3.Lerp(prev, curr, t);
            return true;
        }
    }
}
