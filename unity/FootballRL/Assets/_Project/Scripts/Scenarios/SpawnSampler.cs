using System;
using FootballRL.Gameplay;
using UnityEngine;

namespace FootballRL.Scenarios
{
    public static class SpawnSampler
    {
        public const int MaxAttempts = 32;

        const float PlayerHalfWidth = 0.45f;
        const float PlayerHalfDepth = 0.45f;
        const float PlayerHeight = 1f;
        const float MinBallPlayerSeparation = 0.75f;

        public static bool TrySample(
            ScenarioDefinition scenario,
            ArenaConfig arenaConfig,
            ArenaEpisodeSpawnState prefabSpawn,
            System.Random random,
            ExactEpisodeState exactState,
            bool useExactState,
            out SpawnLayout layout,
            out string error)
        {
            layout = default;
            error = null;

            if (scenario == null)
            {
                error = "Brak ScenarioDefinition.";
                return false;
            }

            if (useExactState)
                return TryBuildExact(exactState, scenario, arenaConfig, out layout, out error);

            if (scenario.kind == ScenarioKind.ManualSandbox && !scenario.useRandomSpawn)
            {
                if (!TryBuildPrefabSpawn(prefabSpawn, scenario, out layout, out error))
                    return false;
                return ValidateLayout(layout, scenario, arenaConfig, out error);
            }

            if (scenario.kind == ScenarioKind.ShootAtEmptyGoal && !scenario.useRandomSpawn)
            {
                if (!TryBuildShootEasy(scenario, out layout, out error))
                    return false;
                return ValidateLayout(layout, scenario, arenaConfig, out error);
            }

            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                if (TryBuildRandom(scenario, arenaConfig, random, out layout) &&
                    ValidateLayout(layout, scenario, arenaConfig, out error))
                    return true;
            }

            error ??= $"SpawnSampler: nie udało się wylosować poprawnego startu po {MaxAttempts} próbach.";
            return false;
        }

        static bool TryBuildExact(
            ExactEpisodeState exact,
            ScenarioDefinition scenario,
            ArenaConfig arenaConfig,
            out SpawnLayout layout,
            out string error)
        {
            layout = default;
            error = null;

            if (exact.hasPlayer)
            {
                layout.playerLocalPosition = exact.playerLocalPosition;
                layout.playerLocalRotation = exact.playerLocalRotation;
            }
            else
            {
                error = "ExactEpisodeState wymaga pozycji gracza.";
                return false;
            }

            if (exact.hasBall)
            {
                layout.ballLocalPosition = exact.ballLocalPosition;
                layout.ballLocalRotation = exact.ballLocalRotation;
            }
            else
            {
                error = "ExactEpisodeState wymaga pozycji piłki.";
                return false;
            }

            layout.hasTarget = exact.hasTarget || scenario.kind == ScenarioKind.MoveToTarget;
            if (layout.hasTarget)
            {
                layout.targetLocalPosition = exact.hasTarget
                    ? exact.targetLocalPosition
                    : new Vector3(0f, 0.05f, 2f);
                layout.targetSuccessRadius = scenario.targetSuccessRadius;
            }

            return ValidateLayout(layout, scenario, arenaConfig, out error);
        }

        static bool TryBuildPrefabSpawn(
            ArenaEpisodeSpawnState prefabSpawn,
            ScenarioDefinition scenario,
            out SpawnLayout layout,
            out string error)
        {
            layout = default;
            error = null;
            if (prefabSpawn == null)
            {
                error = "Brak ArenaEpisodeSpawnState na prefabie.";
                return false;
            }

            prefabSpawn.GetStored(out var ball, out var player);
            layout.ballLocalPosition = ball.localPosition;
            layout.ballLocalRotation = ball.localRotation;
            layout.playerLocalPosition = player.localPosition;
            layout.playerLocalRotation = player.localRotation;
            layout.hasTarget = scenario.kind == ScenarioKind.MoveToTarget;
            if (layout.hasTarget)
            {
                layout.targetLocalPosition = new Vector3(0f, 0.05f, 3f);
                layout.targetSuccessRadius = scenario.targetSuccessRadius;
            }

            return true;
        }

        static bool TryBuildShootEasy(ScenarioDefinition scenario, out SpawnLayout layout, out string error)
        {
            layout = new SpawnLayout
            {
                playerLocalPosition = scenario.easyPlayerLocalPosition,
                playerLocalRotation = Quaternion.Euler(0f, scenario.easyPlayerYawDeg, 0f),
                ballLocalPosition = scenario.easyBallLocalPosition,
                ballLocalRotation = Quaternion.identity,
                hasTarget = false
            };
            error = null;
            return true;
        }

        static bool TryBuildRandom(
            ScenarioDefinition scenario,
            ArenaConfig arenaConfig,
            System.Random random,
            out SpawnLayout layout)
        {
            layout = default;
            var halfX = Mathf.Min(scenario.randomPlayerAbsX, arenaConfig.HalfWidthX - 0.5f);
            var minZ = scenario.randomPlayerMinZ;
            var maxZ = scenario.randomPlayerMaxZ;

            layout.playerLocalPosition = new Vector3(
                RandomRange(random, -halfX, halfX),
                0.5f,
                RandomRange(random, minZ, maxZ));
            layout.playerLocalRotation = Quaternion.Euler(0f, RandomRange(random, -180f, 180f), 0f);

            layout.ballLocalPosition = new Vector3(
                RandomRange(random, -halfX, halfX),
                arenaConfig.ballRadius,
                RandomRange(random, minZ, maxZ));
            layout.ballLocalRotation = Quaternion.identity;

            layout.hasTarget = scenario.kind == ScenarioKind.MoveToTarget;
            if (layout.hasTarget)
            {
                layout.targetLocalPosition = new Vector3(
                    RandomRange(random, -scenario.targetRandomAbsX, scenario.targetRandomAbsX),
                    0.05f,
                    RandomRange(random, scenario.targetRandomMinZ, scenario.targetRandomMaxZ));
                layout.targetSuccessRadius = scenario.targetSuccessRadius;
            }

            return true;
        }

        static bool ValidateLayout(
            SpawnLayout layout,
            ScenarioDefinition scenario,
            ArenaConfig arenaConfig,
            out string error)
        {
            error = null;
            if (!IsFinite(layout.playerLocalPosition) || !IsFinite(layout.ballLocalPosition))
            {
                error = "Nieskończone współrzędne spawnu.";
                return false;
            }

            if (!IsInsidePitch(layout.playerLocalPosition, PlayerHalfWidth, PlayerHalfDepth, arenaConfig) ||
                !IsInsidePitch(layout.ballLocalPosition, arenaConfig.ballRadius, arenaConfig.ballRadius, arenaConfig))
            {
                error = "Spawn poza dozwolonym obszarem boiska.";
                return false;
            }

            if (HorizontalDistance(layout.playerLocalPosition, layout.ballLocalPosition) < MinBallPlayerSeparation)
            {
                error = "Spawn: nakładanie gracza i piłki.";
                return false;
            }

            if (layout.hasTarget)
            {
                if (!IsInsidePitch(layout.targetLocalPosition, 0.2f, 0.2f, arenaConfig))
                {
                    error = "Cel poza boiskiem.";
                    return false;
                }

                if (HorizontalDistance(layout.playerLocalPosition, layout.targetLocalPosition) < 1f ||
                    HorizontalDistance(layout.ballLocalPosition, layout.targetLocalPosition) < 0.6f)
                {
                    error = "Spawn: nakładanie z celem.";
                    return false;
                }
            }

            return true;
        }

        static bool IsInsidePitch(Vector3 localPos, float marginX, float marginZ, ArenaConfig cfg)
        {
            return Mathf.Abs(localPos.x) <= cfg.HalfWidthX - marginX
                   && Mathf.Abs(localPos.z) <= cfg.HalfPlayLengthZ - marginZ
                   && localPos.y >= -0.5f && localPos.y <= cfg.wallHeight + 1f;
        }

        static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static float RandomRange(System.Random random, float min, float max)
        {
            return (float)(min + random.NextDouble() * (max - min));
        }

        static bool IsFinite(Vector3 v) =>
            float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
    }
}
