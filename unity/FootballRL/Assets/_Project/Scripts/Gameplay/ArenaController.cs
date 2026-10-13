using System;
using System.Collections.Generic;
using FootballRL.Contracts;
using UnityEngine;

namespace FootballRL.Gameplay
{
    /// <summary>Jedyny właściciel kroku fizyki zawodników i kopnięć na arenie.</summary>
    public class ArenaController : MonoBehaviour
    {
        [SerializeField] ArenaRoot arenaRoot;
        [SerializeField] MovementConfig movementConfig;
        [SerializeField] KickConfig kickConfig;
        [SerializeField] List<PlayerMotor> players = new List<PlayerMotor>();

        readonly Dictionary<int, PlayerCommand> _activeCommands = new Dictionary<int, PlayerCommand>();
        readonly Dictionary<int, long> _lastCommandIds = new Dictionary<int, long>();
        readonly Dictionary<int, PlayerMotor> _motorsByPlayerId = new Dictionary<int, PlayerMotor>();
        readonly Dictionary<int, long> _kickConsumedCommandIds = new Dictionary<int, long>();
        readonly Dictionary<int, float> _kickCooldownRemaining = new Dictionary<int, float>();
        readonly Dictionary<int, KickAttemptResult> _lastKickAttempts = new Dictionary<int, KickAttemptResult>();

        Rigidbody _ballBody;

        public MovementConfig MovementConfig => movementConfig;
        public KickConfig KickConfig => kickConfig;

        /// <summary>Lokalne zdarzenie areny — udane kopnięcie.</summary>
        public event Action<KickSucceededEventArgs> KickSucceeded;

        void Awake()
        {
            if (arenaRoot == null)
                arenaRoot = GetComponent<ArenaRoot>();

            if (arenaRoot != null)
                _ballBody = arenaRoot.BallBody;

            RefreshPlayerRegistry();
        }

        public void SetReferences(
            ArenaRoot root,
            MovementConfig movement,
            KickConfig kick,
            IEnumerable<PlayerMotor> motors)
        {
            arenaRoot = root;
            movementConfig = movement;
            kickConfig = kick;
            players = motors != null ? new List<PlayerMotor>(motors) : new List<PlayerMotor>();
            if (arenaRoot != null)
                _ballBody = arenaRoot.BallBody;
            RebuildPlayerIndex();
        }

        /// <summary>Odświeża mapę zawodników (Awake + testy batch w edytorze).</summary>
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

        /// <summary>Publiczne wejście: ostatnia komenda przed krokiem fizyki zastępuje poprzednią.</summary>
        public void SubmitCommand(int playerId, PlayerCommand command, long commandId)
        {
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

        void FixedUpdate()
        {
            StepArenaPhysics(Time.fixedDeltaTime);
        }

        /// <summary>Jeden krok areny: cooldown → ruch → kopnięcie.</summary>
        public void StepArenaPhysics(float deltaTime)
        {
            TickKickCooldowns(deltaTime);
            StepPlayerMotors(deltaTime);
            StepKicks();
        }

        /// <summary>Testy batch: tylko cooldown, bez ruchu i kopnięć.</summary>
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

        /// <summary>Jeden krok ruchu zawodników (FixedUpdate + testy batch G2).</summary>
        public void StepPlayerMotors(float deltaTime)
        {
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
                _ballBody = arenaRoot.BallBody;
        }

        /// <summary>Kopnięcia w bieżącym kroku (po ruchu).</summary>
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
                KickSucceeded?.Invoke(new KickSucceededEventArgs(playerId, motor, result.ImpulseMagnitude, result.AngleDeg));
            }
        }
    }
}
