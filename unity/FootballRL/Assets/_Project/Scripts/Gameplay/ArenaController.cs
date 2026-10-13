using System;
using System.Collections.Generic;
using FootballRL.Contracts;
using UnityEngine;

namespace FootballRL.Gameplay
{
    /// <summary>Jedyny właściciel kroku fizyki zawodników, kopnięć i reguł rundy na arenie.</summary>
    public class ArenaController : MonoBehaviour
    {
        [SerializeField] ArenaRoot arenaRoot;
        [SerializeField] MovementConfig movementConfig;
        [SerializeField] KickConfig kickConfig;
        [SerializeField] RoundConfig roundConfig;
        [SerializeField] ArenaEpisodeSpawnState episodeSpawnState;
        [SerializeField] GoalDefinition[] goals =
        {
            new GoalDefinition { defendingTeamId = 1, goalLineZ = 10f, enterDirectionZ = 1f },
            new GoalDefinition { defendingTeamId = 0, goalLineZ = -10f, enterDirectionZ = -1f }
        };

        [SerializeField] List<PlayerMotor> players = new List<PlayerMotor>();

        readonly Dictionary<int, PlayerCommand> _activeCommands = new Dictionary<int, PlayerCommand>();
        readonly Dictionary<int, long> _lastCommandIds = new Dictionary<int, long>();
        readonly Dictionary<int, PlayerMotor> _motorsByPlayerId = new Dictionary<int, PlayerMotor>();
        readonly Dictionary<int, long> _kickConsumedCommandIds = new Dictionary<int, long>();
        readonly Dictionary<int, float> _kickCooldownRemaining = new Dictionary<int, float>();
        readonly Dictionary<int, KickAttemptResult> _lastKickAttempts = new Dictionary<int, KickAttemptResult>();

        Rigidbody _ballBody;
        Transform _ballTransform;

        ArenaPhase _phase = ArenaPhase.Running;
        long _episodeId = 1;
        float _elapsedSeconds;
        RoundEndReason _endReason = RoundEndReason.None;
        bool _hasScoringTeam;
        int _scoringTeamId;
        float _postRoundDelayRemaining;
        bool _pendingSegmentEvaluate;
        Vector3 _segmentStartBallPos;
        bool _segmentSampleValid;
        Vector3 _frozenBallPos;
        Vector3 _frozenBallVel;
        Vector3 _frozenPlayerPos;
        Vector3 _frozenPlayerVel;
        bool _ballKinematicBeforeEnded;
        readonly Dictionary<int, bool> _playerKinematicBeforeEnded = new Dictionary<int, bool>();

        public MovementConfig MovementConfig => movementConfig;
        public KickConfig KickConfig => kickConfig;
        public RoundConfig RoundConfig => roundConfig;
        public ArenaPhase Phase => _phase;
        public long EpisodeId => _episodeId;
        public float ElapsedSeconds => _elapsedSeconds;
        public RoundEndReason LastEndReason => _endReason;

        public event Action<KickSucceededEventArgs> KickSucceeded;
        public event Action<RoundEndedEventArgs> RoundEnded;

        void Awake()
        {
            if (arenaRoot == null)
                arenaRoot = GetComponent<ArenaRoot>();

            if (arenaRoot != null)
            {
                _ballBody = arenaRoot.BallBody;
                if (_ballBody != null)
                    _ballTransform = _ballBody.transform;
            }

            if (episodeSpawnState == null)
                episodeSpawnState = GetComponent<ArenaEpisodeSpawnState>();

            RefreshPlayerRegistry();
            CaptureSpawnIfNeeded();
            BeginEpisodeRunning();
        }

        public void SetReferences(
            ArenaRoot root,
            MovementConfig movement,
            KickConfig kick,
            RoundConfig round,
            IEnumerable<PlayerMotor> motors)
        {
            arenaRoot = root;
            movementConfig = movement;
            if (kick != null)
                kickConfig = kick;
            if (round != null)
                roundConfig = round;
            players = motors != null ? new List<PlayerMotor>(motors) : new List<PlayerMotor>();
            if (arenaRoot != null)
            {
                _ballBody = arenaRoot.BallBody;
                if (_ballBody != null)
                    _ballTransform = _ballBody.transform;
            }

            RebuildPlayerIndex();
        }

        public void RefreshPlayerRegistry()
        {
            RebuildPlayerIndex();
            foreach (var kv in _motorsByPlayerId)
            {
                if (!_activeCommands.ContainsKey(kv.Key))
                    _activeCommands[kv.Key] = PlayerCommand.Zero;
                if (!_kickCooldownRemaining.ContainsKey(kv.Key))
                    _kickCooldownRemaining[kv.Key] = 0f;
            }
        }

        void RebuildPlayerIndex()
        {
            _motorsByPlayerId.Clear();

            if (players.Count == 0 && arenaRoot != null && arenaRoot.PlayersRoot != null)
            {
                players.Clear();
                foreach (Transform child in arenaRoot.PlayersRoot)
                {
                    var motor = child.GetComponent<PlayerMotor>();
                    if (motor != null)
                        players.Add(motor);
                }
            }

            foreach (var motor in players)
            {
                if (motor == null)
                    continue;

                var identity = motor.GetComponent<PlayerIdentity>();
                if (identity == null)
                    continue;

                if (movementConfig != null)
                    motor.SetMovementConfig(movementConfig);

                _motorsByPlayerId[identity.PlayerId] = motor;
            }
        }

        public void SubmitCommand(int playerId, PlayerCommand command, long commandId)
        {
            if (_phase != ArenaPhase.Running)
                return;

            if (!_motorsByPlayerId.ContainsKey(playerId))
            {
                Debug.LogWarning($"ArenaController: nieznany playerId={playerId}.");
                return;
            }

            _activeCommands[playerId] = command;
            _lastCommandIds[playerId] = commandId;
        }

        public bool TryGetActiveCommand(int playerId, out PlayerCommand command)
        {
            return _activeCommands.TryGetValue(playerId, out command);
        }

        public bool TryGetLastCommandId(int playerId, out long commandId)
        {
            return _lastCommandIds.TryGetValue(playerId, out commandId);
        }

        public float GetKickCooldownRemaining(int playerId)
        {
            return _kickCooldownRemaining.TryGetValue(playerId, out var t) ? t : 0f;
        }

        public bool TryGetLastKickAttempt(int playerId, out KickAttemptResult result)
        {
            return _lastKickAttempts.TryGetValue(playerId, out result);
        }

        public float GetRemainingSeconds()
        {
            var limit = roundConfig != null ? roundConfig.roundTimeLimitSeconds : 60f;
            return Mathf.Max(0f, limit - _elapsedSeconds);
        }

        public bool TryGetEndState(out ArenaEndState state)
        {
            if (_phase != ArenaPhase.Ended)
            {
                state = default;
                return false;
            }

            EnsureBallBody();
            var ballPos = _frozenBallPos;
            var ballVel = _frozenBallVel;
            var playerPos = _frozenPlayerPos;
            var playerVel = _frozenPlayerVel;

            state = new ArenaEndState(
                _phase,
                _episodeId,
                _endReason,
                _hasScoringTeam,
                _scoringTeamId,
                _elapsedSeconds,
                GetRemainingSeconds(),
                ballPos,
                ballVel,
                playerPos,
                playerVel);
            return true;
        }

        void GetPrimaryPlayerBody(out Vector3 pos, out Vector3 vel)
        {
            pos = Vector3.zero;
            vel = Vector3.zero;
            foreach (var kv in _motorsByPlayerId)
            {
                var body = kv.Value.Body;
                pos = body.position;
                vel = body.linearVelocity;
                return;
            }
        }

        public void RequestManualResetWhileRunning()
        {
            if (_phase != ArenaPhase.Running)
                return;

            EndRound(RoundEndReason.ManualReset, false, 0);
        }

        public void RequestManualResetWhileEnded()
        {
            if (_phase != ArenaPhase.Ended)
                return;

            ResetEpisode();
        }

        public void ResetEpisode()
        {
            if (!ValidateWorldForReset(out var error))
            {
                EndRound(RoundEndReason.InvalidState, false, 0);
                return;
            }

            EnsureBallBody();
            episodeSpawnState?.Apply(_ballTransform, _ballBody, players.ToArray());

            foreach (var kv in _motorsByPlayerId)
            {
                _activeCommands[kv.Key] = PlayerCommand.Zero;
                _lastCommandIds[kv.Key] = _lastCommandIds.TryGetValue(kv.Key, out var id) ? id + 1 : 1;
                _kickConsumedCommandIds.Remove(kv.Key);
                _kickCooldownRemaining[kv.Key] = 0f;
                _lastKickAttempts.Remove(kv.Key);
            }

            _endReason = RoundEndReason.None;
            _hasScoringTeam = false;
            _scoringTeamId = 0;
            _postRoundDelayRemaining = 0f;
            _pendingSegmentEvaluate = false;
            _segmentSampleValid = false;

            _episodeId++;
            _elapsedSeconds = 0f;

            RestoreDynamicSimulation();
            SyncPhysicsTransforms();
            BeginEpisodeRunning();
        }

        void BeginEpisodeRunning()
        {
            _phase = ArenaPhase.Running;
            if (_ballBody != null)
            {
                _segmentStartBallPos = _ballBody.position;
                _segmentSampleValid = false;
                _pendingSegmentEvaluate = false;
            }
        }

        void CaptureSpawnIfNeeded()
        {
            if (episodeSpawnState == null)
                return;

            episodeSpawnState.CaptureFromScene(_ballTransform, players.ToArray());
        }

        bool ValidateWorldForReset(out string error)
        {
            error = null;
            EnsureBallBody();
            if (_ballBody == null || _ballTransform == null)
            {
                error = "Brak piłki.";
                return false;
            }

            if (ArenaBounds.IsInvalidState(_ballBody.position, GetPrimaryPlayerPosition()))
            {
                error = "Nieskończone pozycje przed resetem.";
                return false;
            }

            return true;
        }

        Vector3 GetPrimaryPlayerPosition()
        {
            GetPrimaryPlayerBody(out var pos, out _);
            return pos;
        }

        void FixedUpdate()
        {
            var dt = Time.fixedDeltaTime;
            if (_phase == ArenaPhase.Ended)
            {
                TickPostRoundDelay(dt);
                return;
            }

            if (_pendingSegmentEvaluate)
                EvaluateAfterPhysics(dt);

            if (_phase == ArenaPhase.Running)
            {
                _segmentStartBallPos = _ballBody != null ? _ballBody.position : Vector3.zero;
                _pendingSegmentEvaluate = true;
                StepArenaPhysics(dt);
            }
        }

        /// <summary>Batch: po Physics.Simulate — gol, bounds, czas.</summary>
        public void FinalizePhysicsStep(float deltaTime)
        {
            if (_phase == ArenaPhase.Ended)
            {
                TickPostRoundDelay(deltaTime);
                return;
            }

            EvaluateAfterPhysics(deltaTime);
            if (_phase == ArenaPhase.Running)
            {
                _segmentStartBallPos = _ballBody != null ? _ballBody.position : Vector3.zero;
                _pendingSegmentEvaluate = true;
            }
        }

        /// <summary>Batch: jeden pełny krok (ruch, symulacja, reguły).</summary>
        public void StepArenaPhysicsWithRules(float deltaTime)
        {
            if (_phase == ArenaPhase.Ended)
            {
                TickPostRoundDelay(deltaTime);
                return;
            }

            if (_pendingSegmentEvaluate)
                EvaluateAfterPhysics(deltaTime);

            if (_phase == ArenaPhase.Running)
            {
                _segmentStartBallPos = _ballBody != null ? _ballBody.position : Vector3.zero;
                StepArenaPhysics(deltaTime);
                Physics.Simulate(deltaTime);
                EvaluateAfterPhysics(deltaTime);
                if (_phase == ArenaPhase.Running && _ballBody != null)
                    _segmentStartBallPos = _ballBody.position;
            }
        }

        void EvaluateAfterPhysics(float deltaTime)
        {
            _pendingSegmentEvaluate = false;
            if (_phase != ArenaPhase.Running)
                return;

            EnsureBallBody();
            if (_ballBody == null)
                return;

            var curr = _ballBody.position;
            if (_segmentSampleValid)
                TryEndRoundFromPhysics(_segmentStartBallPos, curr, deltaTime);
            else
                _segmentSampleValid = true;

            _segmentStartBallPos = curr;
        }

        void TryEndRoundFromPhysics(Vector3 prevBall, Vector3 currBall, float deltaTime)
        {
            if (_phase != ArenaPhase.Running)
                return;

            _elapsedSeconds += deltaTime;

            var config = arenaRoot != null ? arenaRoot.ArenaConfig : null;
            var playerPos = GetPrimaryPlayerPosition();

            if (ArenaBounds.IsInvalidState(currBall, playerPos))
            {
                EndRound(RoundEndReason.InvalidState, false, 0);
                return;
            }

            if (config != null &&
                GoalDetector.TryDetectGoal(prevBall, currBall, config.ballRadius, config.HalfGoalWidth,
                    config.goalHeight, goals, out var scoringTeam))
            {
                EndRound(RoundEndReason.Goal, true, scoringTeam);
                return;
            }

            if (config != null && ArenaBounds.IsBallOutOfPlay(currBall, config.ballRadius, config))
            {
                EndRound(RoundEndReason.BallOut, false, 0);
                return;
            }

            var limit = roundConfig != null ? roundConfig.roundTimeLimitSeconds : 60f;
            if (_elapsedSeconds >= limit)
                EndRound(RoundEndReason.TimeLimit, false, 0);
        }

        void EndRound(RoundEndReason reason, bool hasScoringTeam, int scoringTeamId)
        {
            if (_phase == ArenaPhase.Ended)
                return;

            _phase = ArenaPhase.Ended;
            _endReason = reason;
            _hasScoringTeam = hasScoringTeam;
            _scoringTeamId = scoringTeamId;
            _pendingSegmentEvaluate = false;

            EnsureBallBody();
            if (_ballBody != null)
            {
                _frozenBallPos = _ballBody.position;
                _frozenBallVel = _ballBody.linearVelocity;
            }

            GetPrimaryPlayerBody(out _frozenPlayerPos, out _frozenPlayerVel);
            FreezeBodiesAfterEnd();

            var delay = roundConfig != null ? roundConfig.autoResetDelaySeconds : 1.5f;
            _postRoundDelayRemaining = roundConfig == null || roundConfig.autoResetEnabled ? delay : 0f;

            RoundEnded?.Invoke(new RoundEndedEventArgs(_episodeId, reason, hasScoringTeam, scoringTeamId,
                _elapsedSeconds));
        }

        void TickPostRoundDelay(float deltaTime)
        {
            if (roundConfig == null || !roundConfig.autoResetEnabled)
                return;

            if (_postRoundDelayRemaining <= 0f)
                return;

            _postRoundDelayRemaining -= deltaTime;
            if (_postRoundDelayRemaining <= 0f)
                ResetEpisode();
        }

        /// <summary>Jeden krok areny: cooldown → ruch → kopnięcie (tylko Running).</summary>
        public void StepArenaPhysics(float deltaTime)
        {
            if (_phase != ArenaPhase.Running)
                return;

            TickKickCooldowns(deltaTime);
            StepPlayerMotors(deltaTime);
            StepKicks();
        }

        public void StepKickCooldownOnly(float deltaTime)
        {
            TickKickCooldowns(deltaTime);
        }

        void TickKickCooldowns(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            var keys = new List<int>(_kickCooldownRemaining.Keys);
            foreach (var playerId in keys)
            {
                var remaining = _kickCooldownRemaining[playerId];
                if (remaining <= 0f)
                    continue;

                remaining -= deltaTime;
                _kickCooldownRemaining[playerId] = Mathf.Max(0f, remaining);
            }
        }

        public void StepPlayerMotors(float deltaTime)
        {
            if (_phase != ArenaPhase.Running)
                return;

            foreach (var kv in _motorsByPlayerId)
            {
                if (!_activeCommands.TryGetValue(kv.Key, out var cmd))
                    cmd = PlayerCommand.Zero;

                kv.Value.ApplyCommand(cmd, deltaTime);
            }
        }

        void EnsureBallBody()
        {
            if (_ballBody == null && arenaRoot != null)
            {
                _ballBody = arenaRoot.BallBody;
                if (_ballBody != null)
                    _ballTransform = _ballBody.transform;
            }
        }

        public void StepPlayerKicks()
        {
            StepKicksInternal();
        }

        void StepKicks()
        {
            StepKicksInternal();
        }

        void StepKicksInternal()
        {
            if (_phase != ArenaPhase.Running)
                return;

            EnsureBallBody();
            if (kickConfig == null || _ballBody == null)
                return;

            foreach (var kv in _motorsByPlayerId)
            {
                var playerId = kv.Key;
                var motor = kv.Value;

                if (!_activeCommands.TryGetValue(playerId, out var cmd))
                    continue;
                if (!_lastCommandIds.TryGetValue(playerId, out var commandId))
                    continue;

                if (cmd.Kick <= kickConfig.kickThreshold)
                    continue;

                if (_kickConsumedCommandIds.TryGetValue(playerId, out var consumed) && consumed == commandId)
                    continue;

                _kickConsumedCommandIds[playerId] = commandId;

                var anchor = motor.GetComponent<PlayerKickAnchor>();
                var cooldown = GetKickCooldownRemaining(playerId);
                var result = PlayerKicker.TryKick(playerId, motor, anchor, _ballBody, cmd, cooldown, kickConfig);
                _lastKickAttempts[playerId] = result;

                if (!result.Success)
                    continue;

                _kickCooldownRemaining[playerId] = kickConfig.cooldownSeconds;
                KickSucceeded?.Invoke(new KickSucceededEventArgs(playerId, motor, result.ImpulseMagnitude,
                    result.AngleDeg));
            }
        }

        void FreezeBodiesAfterEnd()
        {
            _playerKinematicBeforeEnded.Clear();

            if (_ballBody != null)
            {
                _ballKinematicBeforeEnded = _ballBody.isKinematic;
                _ballBody.linearVelocity = Vector3.zero;
                _ballBody.angularVelocity = Vector3.zero;
                _ballBody.isKinematic = true;
            }

            foreach (var kv in _motorsByPlayerId)
            {
                var body = kv.Value.Body;
                _playerKinematicBeforeEnded[kv.Key] = body.isKinematic;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }

            Physics.SyncTransforms();
        }

        void RestoreDynamicSimulation()
        {
            if (_ballBody != null)
                _ballBody.isKinematic = _ballKinematicBeforeEnded;

            foreach (var kv in _motorsByPlayerId)
            {
                if (!_playerKinematicBeforeEnded.TryGetValue(kv.Key, out var wasKinematic))
                    wasKinematic = false;

                kv.Value.Body.isKinematic = wasKinematic;
            }

            _playerKinematicBeforeEnded.Clear();
        }

        void SyncPhysicsTransforms()
        {
            Physics.SyncTransforms();
            if (_ballBody != null)
            {
                _ballBody.WakeUp();
                foreach (var motor in players)
                {
                    if (motor != null)
                        motor.Body.WakeUp();
                }
            }
        }

#if UNITY_EDITOR
        public void EditorForceElapsedSeconds(float seconds) => _elapsedSeconds = seconds;

        public void EditorSetBallPosition(Vector3 worldPos)
        {
            EnsureBallBody();
            if (_ballTransform == null)
                return;

            _ballTransform.position = worldPos;
            if (_ballBody != null)
            {
                _ballBody.linearVelocity = Vector3.zero;
                _ballBody.angularVelocity = Vector3.zero;
            }

            Physics.SyncTransforms();
            _segmentStartBallPos = worldPos;
            _segmentSampleValid = false;
            _pendingSegmentEvaluate = false;
        }

        public void EditorConfigureForTests(RoundConfig round, bool autoResetEnabled)
        {
            roundConfig = round;
            if (roundConfig != null)
                roundConfig.autoResetEnabled = autoResetEnabled;
        }

        public void EditorEvaluateBallSegment(Vector3 previousCenter, Vector3 currentCenter, float deltaTime)
        {
            EnsureBallBody();
            if (_ballTransform != null)
                _ballTransform.position = currentCenter;
            if (_ballBody != null)
            {
                _ballBody.position = currentCenter;
                Physics.SyncTransforms();
            }

            TryEndRoundFromPhysics(previousCenter, currentCenter, deltaTime);
        }

        public void EditorDisableAutoReset()
        {
            if (roundConfig != null)
                roundConfig.autoResetEnabled = false;
        }
#endif
    }
}
