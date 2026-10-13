using System.Collections.Generic;
using FootballRL.Contracts;
using FootballRL.Scenarios;
using UnityEngine;

namespace FootballRL.Gameplay
{
    public static class ArenaStateReader
    {
        public static ArenaSnapshot Capture(ArenaController controller)
        {
            var arenaRoot = controller.ArenaRootTransform;
            var cfg = controller.ArenaConfig;
            var snapshot = new ArenaSnapshot
            {
                Arena = new ArenaSnapshotArena
                {
                    ContractVersion = ArenaContract.Version,
                    EpisodeId = controller.EpisodeId,
                    Tick = controller.PhysicsTick,
                    ElapsedSeconds = controller.ElapsedSeconds,
                    RemainingSeconds = controller.GetRemainingSeconds(),
                    Phase = controller.Phase,
                    PitchWidthX = cfg != null ? cfg.pitchWidthX : 0f,
                    PitchLengthZ = cfg != null ? cfg.pitchPlayLengthZ : 0f
                },
                Ball = CaptureBall(controller, arenaRoot),
                Players = CapturePlayers(controller, arenaRoot),
                Goals = CaptureGoals(controller, arenaRoot, cfg),
                Target = CaptureTarget(controller, arenaRoot),
                Obstacles = CaptureObstacles(controller, arenaRoot),
                RoundEnd = CaptureRoundEnd(controller)
            };
            return snapshot;
        }

        static ArenaSnapshotBall CaptureBall(ArenaController controller, Transform arenaRoot)
        {
            if (!controller.TryGetBallWorldState(out var worldPos, out var worldVel, out var worldAngVel, out var present))
            {
                return new ArenaSnapshotBall { Present = false };
            }

            return new ArenaSnapshotBall
            {
                Present = present,
                LocalPosition = ToLocal(arenaRoot, worldPos),
                LocalVelocity = ToLocalDirection(arenaRoot, worldVel),
                LocalAngularVelocity = ToLocalDirection(arenaRoot, worldAngVel)
            };
        }

        static ArenaSnapshotPlayer[] CapturePlayers(ArenaController controller, Transform arenaRoot)
        {
            var list = new List<ArenaSnapshotPlayer>();
            foreach (var entry in controller.GetPlayerSnapshotEntries())
            {
                var forward = entry.Motor.transform.forward;
                list.Add(new ArenaSnapshotPlayer
                {
                    PlayerId = entry.PlayerId,
                    TeamId = entry.TeamId,
                    LocalPosition = ToLocal(arenaRoot, entry.WorldPosition),
                    LocalForward = ToLocalDirection(arenaRoot, forward).normalized,
                    LocalVelocity = ToLocalDirection(arenaRoot, entry.WorldVelocity),
                    AngularVelocityY = entry.AngularVelocityY,
                    CooldownRemainingSeconds = entry.CooldownRemaining,
                    CanKickNow = entry.CanKickNow
                });
            }

            list.Sort((a, b) => a.PlayerId.CompareTo(b.PlayerId));
            return list.ToArray();
        }

        static ArenaSnapshotGoal[] CaptureGoals(ArenaController controller, Transform arenaRoot, ArenaConfig cfg)
        {
            var defs = controller.GoalDefinitions;
            if (defs == null || cfg == null)
                return System.Array.Empty<ArenaSnapshotGoal>();

            var goals = new ArenaSnapshotGoal[defs.Length];
            for (var i = 0; i < defs.Length; i++)
            {
                var g = defs[i];
                var centerWorld = arenaRoot.TransformPoint(new Vector3(0f, cfg.goalHeight * 0.5f, g.goalLineZ));
                var dirWorld = arenaRoot.TransformDirection(new Vector3(0f, 0f, g.enterDirectionZ));
                goals[i] = new ArenaSnapshotGoal
                {
                    DefendingTeamId = g.defendingTeamId,
                    LineCenterLocal = new Vector3(0f, cfg.goalHeight * 0.5f, g.goalLineZ),
                    DirectionLocal = new Vector3(0f, 0f, g.enterDirectionZ).normalized,
                    Width = cfg.goalWidth,
                    Height = cfg.goalHeight
                };
            }

            return goals;
        }

        static ArenaSnapshotTarget CaptureTarget(ArenaController controller, Transform arenaRoot)
        {
            if (!controller.TryGetExerciseTargetLocal(out var localPos, out var radius, out var present))
            {
                return new ArenaSnapshotTarget { Present = false };
            }

            return new ArenaSnapshotTarget
            {
                Present = present,
                LocalPosition = localPos,
                SuccessRadius = radius
            };
        }

        static ArenaSnapshotObstacle[] CaptureObstacles(ArenaController controller, Transform arenaRoot)
        {
            var source = controller.GetObstacleEntries();
            if (source == null || source.Count == 0)
                return System.Array.Empty<ArenaSnapshotObstacle>();

            var list = new List<ArenaSnapshotObstacle>();
            foreach (var o in source)
            {
                list.Add(new ArenaSnapshotObstacle
                {
                    StableId = o.StableId,
                    LocalPosition = o.transform.localPosition,
                    LocalSize = o.transform.localScale,
                    LocalRotation = o.transform.localRotation
                });
            }

            list.Sort((a, b) => a.StableId.CompareTo(b.StableId));
            return list.ToArray();
        }

        static ArenaSnapshotRoundEnd CaptureRoundEnd(ArenaController controller)
        {
            if (controller.Phase != ArenaPhase.Ended)
                return new ArenaSnapshotRoundEnd { HasRoundEnd = false };

            var hasScoring = controller.TryGetEndState(out var end) && end.HasScoringTeam;
            return new ArenaSnapshotRoundEnd
            {
                HasRoundEnd = true,
                Reason = controller.LastEndReason,
                HasScoringTeam = hasScoring,
                ScoringTeamId = hasScoring ? end.ScoringTeamId : 0
            };
        }

        static Vector3 ToLocal(Transform arenaRoot, Vector3 world) =>
            arenaRoot != null ? arenaRoot.InverseTransformPoint(world) : world;

        static Vector3 ToLocalDirection(Transform arenaRoot, Vector3 worldDir) =>
            arenaRoot != null ? arenaRoot.InverseTransformDirection(worldDir) : worldDir;
    }
}
