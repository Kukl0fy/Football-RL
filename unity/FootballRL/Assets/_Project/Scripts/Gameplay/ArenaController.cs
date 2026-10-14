using System;
using System.Collections.Generic;
using FootballRL.Contracts;
using FootballRL.Scenarios;
using UnityEngine;
using UnityEngine.SceneManagement;

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
        [SerializeField] ScenarioDefinition defaultScenario;
        [SerializeField] ExerciseTarget exerciseTarget;
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
        Vector3 _frozenBallAngVel;
        Vector3 _frozenPlayerPos;
        Vector3 _frozenPlayerVel;
        float _frozenPlayerAngVelY;
        bool _ballKinematicBeforeEnded;
        Collider _ballCollider;
        bool _ballColliderEnabledBeforeEnded;
        bool _bodiesFrozenForEndedRound;
        readonly Dictionary<int, bool> _playerKinematicBeforeEnded = new Dictionary<int, bool>();
        long _physicsTick;
        ResetEpisodeRequest _lastResetRequest;
        ScenarioDefinition _activeScenario;
        bool _exerciseTargetReached;
        bool _simulationPaused;

        public MovementConfig MovementConfig => movementConfig;
        public KickConfig KickConfig => kickConfig;
        public RoundConfig RoundConfig => roundConfig;
        public ArenaPhase Phase => _phase;
        public long EpisodeId => _episodeId;
        public float ElapsedSeconds => _elapsedSeconds;
        public RoundEndReason LastEndReason => _endReason;
        public long PhysicsTick => _physicsTick;
        public ScenarioDefinition ActiveScenario => _activeScenario;
        public bool ExerciseTargetReached => _exerciseTargetReached;
        public Transform ArenaRootTransform => arenaRoot != null ? arenaRoot.transform : transform;
        public ArenaConfig ArenaConfig => arenaRoot != null ? arenaRoot.ArenaConfig : null;
        public GoalDefinition[] GoalDefinitions => goals;

        /// <summary>Linie bramkowe zostają na końcach boiska z konfiguracji. Kierunek gola i drużyny bez zmian.</summary>
        public bool MatchGoalLinesToPitchLength()
        {
            var cfg = ArenaConfig;
            if (cfg == null || goals == null)
                return false;

            var half = cfg.HalfPlayLengthZ;
            var changed = false;
            for (var i = 0; i < goals.Length; i++)
            {
                var sign = goals[i].enterDirectionZ < 0f ? -1f : 1f;
                var line = sign * half;
                if (Mathf.Approximately(goals[i].goalLineZ, line))
                    continue;

                goals[i].goalLineZ = line;
                changed = true;
            }

            return changed;
        }
        public string LastResetError { get; private set; }
        public ScenarioDefinition DefaultScenarioAsset => defaultScenario;
        public ExerciseTarget ExerciseTargetComponent => exerciseTarget;

        /// <summary>Wstrzymuje krok fizyki tej areny (inne areny mogą działać dalej).</summary>
        public void SetSimulationPaused(bool paused) => _simulationPaused = paused;

        public bool IsSimulationPaused => _simulationPaused;

        public bool IsKickAngleRegulated
        {
            get
            {
                if (_activeScenario == null)
                    return true;
                if (_activeScenario.kind == ScenarioKind.ShootAtEmptyGoal)
                    return false;
                return _activeScenario.regulateKickAngle;
            }
        }

        public bool IsKickingEnabled =>
            _activeScenario == null || _activeScenario.kickingEnabled;

        public float LockedKickLoft =>
            _activeScenario != null ? _activeScenario.lockedLoft : -1f;

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
            _episodeId = 0;
            _lastResetRequest = ResetEpisodeRequest.ForScenario(
                defaultScenario,
                1);
            ResetEpisode(_lastResetRequest);
        }

        public void SetReferences(
            ArenaRoot root,
            MovementConfig movement,
            KickConfig kick,
            RoundConfig round,
            ScenarioDefinition scenario,
            ExerciseTarget target,
            IEnumerable<PlayerMotor> motors)
        {
            arenaRoot = root;
            movementConfig = movement;
            if (kick != null)
                kickConfig = kick;
            if (round != null)
                roundConfig = round;
            if (scenario != null)
                defaultScenario = scenario;
            if (target != null)
                exerciseTarget = target;
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
                if (motor == null || motor.gameObject.scene != gameObject.scene)
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
            var limit = GetRoundTimeLimitSeconds();
            return Mathf.Max(0f, limit - _elapsedSeconds);
        }

        float GetRoundTimeLimitSeconds()
        {
            if (_activeScenario != null && _activeScenario.roundTimeLimitSeconds > 0f)
                return _activeScenario.roundTimeLimitSeconds;
            return roundConfig != null ? roundConfig.roundTimeLimitSeconds : 60f;
        }

        public ArenaSnapshot CaptureSnapshot() => ArenaStateReader.Capture(this);

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

            ResetEpisode(_lastResetRequest);
        }

        public void ResetEpisode() => ResetEpisode(_lastResetRequest);

        public void ResetEpisode(ResetEpisodeRequest request)
        {
            if (!TryResetEpisodeInternal(request, out var error))
            {
                LastResetError = error;
                Debug.LogError($"ArenaController.ResetEpisode: {error}");
            }
        }

        public bool TryResetEpisode(ResetEpisodeRequest request, out string error)
        {
            return TryResetEpisodeInternal(request, out error);
        }

        bool TryResetEpisodeInternal(ResetEpisodeRequest request, out string error)
        {
            error = null;
            LastResetError = null;

            var scenario = request.Scenario != null ? request.Scenario : defaultScenario;
            if (scenario == null)
            {
                error = "Brak scenariusza resetu.";
                return false;
            }

            var random = new System.Random(request.Seed);
            if (!SpawnSampler.TrySample(
                    scenario,
                    ArenaConfig,
                    episodeSpawnState,
                    random,
                    request.ExactState,
                    request.UseExactState,
                    out var layout,
                    out error))
                return false;

            RestoreDynamicSimulation();

            EnsureBallBody();
            episodeSpawnState?.ApplyLayout(_ballTransform, _ballBody, players.ToArray(), layout);

            ApplyExerciseTargetFromLayout(scenario, layout);

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
            _exerciseTargetReached = false;

            _activeScenario = scenario;
            _lastResetRequest = new ResetEpisodeRequest
            {
                Seed = request.Seed,
                Scenario = scenario,
                UseExactState = request.UseExactState,
                ExactState = request.ExactState
            };
            _episodeId++;
            _elapsedSeconds = 0f;
            _physicsTick = 0;

            SyncPhysicsTransforms();
            BeginEpisodeRunning();
            return true;
        }

        void ApplyExerciseTargetFromLayout(ScenarioDefinition scenario, SpawnLayout layout)
        {
            if (exerciseTarget == null)
                return;

            var show = scenario.kind == ScenarioKind.MoveToTarget;
            exerciseTarget.SetActiveForScenario(show);
            if (!show)
                return;

            exerciseTarget.Configure(layout.targetSuccessRadius);
            exerciseTarget.SetLocalPosition(layout.targetLocalPosition);
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

        float GetPrimaryPlayerAngularVelocityY()
        {
            foreach (var kv in _motorsByPlayerId)
                return kv.Value.Body.angularVelocity.y;

            return 0f;
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

            StepBeforeSharedSimulation(deltaTime);
            SimulateArenaPhysics(deltaTime);
            StepAfterSharedSimulation(deltaTime);
        }

        /// <summary>Solver fizyki przypisany do sceny tej areny (lokalna fizyka G6+).</summary>
        public void SimulateArenaPhysics(float deltaTime)
        {
            if (_simulationPaused)
                return;

            var physicsScene = gameObject.scene.GetPhysicsScene();
            if (physicsScene.IsValid())
                physicsScene.Simulate(deltaTime);
        }

        /// <summary>Batch (wiele aren): ruch/kopnięcia przed wspólnym <see cref="Physics.Simulate"/>.</summary>
        public void StepBeforeSharedSimulation(float deltaTime)
        {
            if (_phase == ArenaPhase.Ended)
            {
                TickPostRoundDelay(deltaTime);
                return;
            }

            if (_simulationPaused)
                return;

            if (_pendingSegmentEvaluate)
                EvaluateAfterPhysics(deltaTime);

            if (_phase == ArenaPhase.Running)
            {
                _segmentStartBallPos = _ballBody != null ? _ballBody.position : Vector3.zero;
                StepArenaPhysics(deltaTime);
            }
        }

        /// <summary>Batch (wiele aren): reguły po wspólnym <see cref="Physics.Simulate"/>.</summary>
        public void StepAfterSharedSimulation(float deltaTime)
        {
            if (_phase == ArenaPhase.Ended || _simulationPaused)
                return;

            EvaluateAfterPhysics(deltaTime);
            if (_phase == ArenaPhase.Running && _ballBody != null)
                _segmentStartBallPos = _ballBody.position;
        }

        /// <summary>Krok wielu aren: jedna symulacja na unikalną <see cref="PhysicsScene"/> (wspólna scena = jeden solver).</summary>
        public static void StepManyWithSharedSimulation(IReadOnlyList<ArenaController> arenas, float deltaTime)
        {
            if (arenas == null || arenas.Count == 0)
                return;

            for (var i = 0; i < arenas.Count; i++)
                arenas[i]?.StepBeforeSharedSimulation(deltaTime);

            var simulatedScenes = new HashSet<PhysicsScene>();
            for (var i = 0; i < arenas.Count; i++)
            {
                var arena = arenas[i];
                if (arena == null || arena.IsSimulationPaused)
                    continue;

                var physicsScene = arena.gameObject.scene.GetPhysicsScene();
                if (!physicsScene.IsValid() || !simulatedScenes.Add(physicsScene))
                    continue;

                physicsScene.Simulate(deltaTime);
            }

            for (var i = 0; i < arenas.Count; i++)
                arenas[i]?.StepAfterSharedSimulation(deltaTime);
        }

        void EvaluateAfterPhysics(float deltaTime)
        {
            _pendingSegmentEvaluate = false;
            if (_phase != ArenaPhase.Running)
                return;

            EnsureBallBody();
            if (_ballBody == null)
                return;

            _physicsTick++;

            var curr = _ballBody.position;
            if (!_segmentSampleValid)
                _segmentSampleValid = true;

            TryEndRoundFromPhysics(_segmentStartBallPos, curr, deltaTime);

            UpdateExerciseTargetReach();
            _segmentStartBallPos = curr;
        }

        void UpdateExerciseTargetReach()
        {
            if (_exerciseTargetReached || exerciseTarget == null || !exerciseTarget.gameObject.activeInHierarchy)
                return;

            GetPrimaryPlayerBody(out var pos, out _);
            if (exerciseTarget.IsPlayerInside(pos))
                _exerciseTargetReached = true;
        }

        Vector3 WorldToArenaLocal(Vector3 worldPoint)
        {
            var root = ArenaRootTransform;
            return root != null ? root.InverseTransformPoint(worldPoint) : worldPoint;
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

            var prevLocal = WorldToArenaLocal(prevBall);
            var currLocal = WorldToArenaLocal(currBall);

            if (config != null &&
                GoalDetector.TryDetectGoal(prevLocal, currLocal, config.ballRadius, config.HalfGoalWidth,
                    config.goalHeight, goals, out var scoringTeam))
            {
                EndRound(RoundEndReason.Goal, true, scoringTeam);
                return;
            }

            if (config != null && ArenaBounds.IsBallOutOfPlay(currLocal, config.ballRadius, config))
            {
                EndRound(RoundEndReason.BallOut, false, 0);
                return;
            }

            if (_elapsedSeconds >= GetRoundTimeLimitSeconds())
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
                _frozenBallAngVel = _ballBody.angularVelocity;
            }

            GetPrimaryPlayerBody(out _frozenPlayerPos, out _frozenPlayerVel);
            _frozenPlayerAngVelY = GetPrimaryPlayerAngularVelocityY();
            FreezeBodiesAfterEnd();
            _bodiesFrozenForEndedRound = true;

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
                ResetEpisode(_lastResetRequest);
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
            if (_ballBody != null && _ballBody.gameObject.scene != gameObject.scene)
            {
                _ballBody = null;
                _ballTransform = null;
                _ballCollider = null;
            }

            if (_ballBody == null)
            {
                _ballBody = ResolveBallBodyInThisArena();
                if (_ballBody != null)
                    _ballTransform = _ballBody.transform;
            }

            if (_ballCollider == null && _ballBody != null)
                _ballCollider = _ballBody.GetComponent<Collider>();
        }

        Rigidbody ResolveBallBodyInThisArena()
        {
            EnsureInstanceArenaReferences();
            if (arenaRoot == null)
                return null;

            var fromRoot = arenaRoot.BallBody;
            if (fromRoot != null && IsRigidbodyOnThisArena(fromRoot))
                return fromRoot;

            var bodies = arenaRoot.GetComponentsInChildren<Rigidbody>(true);
            for (var i = 0; i < bodies.Length; i++)
            {
                var rb = bodies[i];
                if (!IsRigidbodyOnThisArena(rb))
                    continue;

                if (rb.GetComponent<BallPhysicsSetup>() != null)
                    return rb;
            }

            return null;
        }

        bool IsRigidbodyOnThisArena(Rigidbody body)
        {
            if (body == null)
                return false;

            if (body.gameObject.scene != gameObject.scene)
                return false;

            return arenaRoot != null && body.transform.IsChildOf(arenaRoot.transform);
        }

        void EnsureInstanceArenaReferences()
        {
            if (arenaRoot == null || arenaRoot.gameObject.scene != gameObject.scene)
                arenaRoot = GetComponent<ArenaRoot>();

            if (episodeSpawnState == null || episodeSpawnState.gameObject.scene != gameObject.scene)
                episodeSpawnState = GetComponent<ArenaEpisodeSpawnState>();
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
            if (kickConfig == null || _ballBody == null || !IsKickingEnabled)
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
                EnsureBallBody();
                if (_ballCollider != null)
                {
                    _ballColliderEnabledBeforeEnded = _ballCollider.enabled;
                    _ballCollider.enabled = false;
                }
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
            if (!_bodiesFrozenForEndedRound)
            {
                if (_ballBody != null)
                {
                    _ballBody.isKinematic = false;
                    if (_ballCollider != null)
                        _ballCollider.enabled = true;
                }

                foreach (var kv in _motorsByPlayerId)
                    kv.Value.Body.isKinematic = false;

                _playerKinematicBeforeEnded.Clear();
                return;
            }

            if (_ballBody != null)
            {
                _ballBody.isKinematic = _ballKinematicBeforeEnded;
                if (_ballCollider != null)
                    _ballCollider.enabled = _ballColliderEnabledBeforeEnded;
            }

            foreach (var kv in _motorsByPlayerId)
            {
                if (!_playerKinematicBeforeEnded.TryGetValue(kv.Key, out var wasKinematic))
                    wasKinematic = false;

                kv.Value.Body.isKinematic = wasKinematic;
            }

            _playerKinematicBeforeEnded.Clear();
            _bodiesFrozenForEndedRound = false;
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

        public bool TryGetBallWorldState(
            out Vector3 worldPosition,
            out Vector3 worldVelocity,
            out Vector3 worldAngularVelocity,
            out bool present)
        {
            EnsureBallBody();
            present = _ballBody != null;
            if (_phase == ArenaPhase.Ended && present)
            {
                worldPosition = _frozenBallPos;
                worldVelocity = _frozenBallVel;
                worldAngularVelocity = _frozenBallAngVel;
                return true;
            }

            if (!present)
            {
                worldPosition = default;
                worldVelocity = default;
                worldAngularVelocity = default;
                return false;
            }

            worldPosition = _ballBody.worldCenterOfMass;
            worldVelocity = _ballBody.linearVelocity;
            worldAngularVelocity = _ballBody.angularVelocity;
            return true;
        }

        public IEnumerable<PlayerSnapshotEntry> GetPlayerSnapshotEntries()
        {
            EnsureBallBody();
            foreach (var kv in _motorsByPlayerId)
            {
                var motor = kv.Value;
                var body = motor.Body;
                var identity = motor.GetComponent<PlayerIdentity>();
                var playerId = identity != null ? identity.PlayerId : kv.Key;
                var teamId = identity != null ? identity.TeamId : 0;
                var anchor = motor.GetComponent<PlayerKickAnchor>();
                var cooldown = GetKickCooldownRemaining(playerId);
                var canKick = kickConfig != null && _ballBody != null && IsKickingEnabled &&
                              PlayerKicker.CanKickNow(motor, anchor, _ballBody, cooldown, kickConfig);

                Vector3 pos;
                Vector3 vel;
                if (_phase == ArenaPhase.Ended)
                {
                    pos = _frozenPlayerPos;
                    vel = _frozenPlayerVel;
                }
                else
                {
                    pos = body.position;
                    vel = body.linearVelocity;
                }

                var angVelY = _phase == ArenaPhase.Ended ? _frozenPlayerAngVelY : body.angularVelocity.y;

                yield return new PlayerSnapshotEntry(
                    playerId,
                    teamId,
                    motor,
                    pos,
                    vel,
                    angVelY,
                    cooldown,
                    canKick);
            }
        }

        public bool TryGetExerciseTargetLocal(out Vector3 localPosition, out float radius, out bool present)
        {
            if (exerciseTarget == null || !exerciseTarget.gameObject.activeInHierarchy ||
                _activeScenario == null || _activeScenario.kind != ScenarioKind.MoveToTarget)
            {
                localPosition = default;
                radius = 0f;
                present = false;
                return false;
            }

            localPosition = exerciseTarget.transform.localPosition;
            radius = exerciseTarget.SuccessRadius;
            present = true;
            return true;
        }

        public IReadOnlyList<ObstacleIdentity> GetObstacleEntries()
        {
            if (arenaRoot == null || arenaRoot.ObstaclesRoot == null)
                return Array.Empty<ObstacleIdentity>();

            var list = new List<ObstacleIdentity>();
            foreach (Transform child in arenaRoot.ObstaclesRoot)
            {
                var id = child.GetComponent<ObstacleIdentity>();
                if (id != null)
                    list.Add(id);
            }

            return list;
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
                _ballBody.position = worldPos;
                _ballBody.linearVelocity = Vector3.zero;
                _ballBody.angularVelocity = Vector3.zero;
            }

            Physics.SyncTransforms();
            _segmentStartBallPos = worldPos;
            _segmentSampleValid = false;
            _pendingSegmentEvaluate = false;
        }

        public void EditorSetBallLinearVelocity(Vector3 worldVelocity)
        {
            EnsureBallBody();
            if (_ballBody == null)
                return;

            _ballBody.linearVelocity = worldVelocity;
            _ballBody.WakeUp();
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

        public void EditorPrepareForBatchVerification()
        {
            EnsureInstanceArenaReferences();
            _ballBody = null;
            _ballTransform = null;
            _ballCollider = null;
            EnsureBallBody();
            RefreshPlayerRegistry();
        }

        public void EditorSetActiveScenarioForTests(ScenarioDefinition scenario)
        {
            _activeScenario = scenario;
        }
#endif
    }
}
