using FootballRL.Contracts;
using FootballRL.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FootballRL.Input
{
    /// <summary>Klawiatura (Input System) → SubmitCommand: ruch G2 + kopnięcie G3.</summary>
    public class KeyboardCommandSource : MonoBehaviour
    {
        [SerializeField] ArenaController arena;
        [SerializeField] int playerId;

        readonly ManualKickInputState _kickState = new ManualKickInputState();
        long _commandId;
        PlayerCommand _lastSubmittedMovement = PlayerCommand.Zero;
        bool _spaceWasPressed;

        public ManualKickInputState KickState => _kickState;

        public void Configure(ArenaController arenaController, int controlledPlayerId)
        {
            arena = arenaController;
            playerId = controlledPlayerId;
        }

        void Update()
        {
            if (arena == null)
                return;

            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.rKey.wasPressedThisFrame)
            {
                if (arena.Phase == ArenaPhase.Running)
                    arena.RequestManualResetWhileRunning();
                else if (arena.Phase == ArenaPhase.Ended)
                    arena.RequestManualResetWhileEnded();
            }

            if (arena.Phase != ArenaPhase.Running)
                return;

            if (arena.IsKickAngleRegulated)
            {
                KeyboardKickMapping.ApplyArrowAdjustments(
                    _kickState,
                    keyboard.upArrowKey.wasPressedThisFrame,
                    keyboard.downArrowKey.wasPressedThisFrame,
                    keyboard.leftArrowKey.wasPressedThisFrame,
                    keyboard.rightArrowKey.wasPressedThisFrame);
            }
            else
            {
                _kickState.SetLoft(arena.LockedKickLoft);
            }

            var movement = KeyboardCommandMapping.FromKeyStates(
                keyboard.wKey.isPressed,
                keyboard.sKey.isPressed,
                keyboard.aKey.isPressed,
                keyboard.dKey.isPressed,
                keyboard.qKey.isPressed,
                keyboard.eKey.isPressed);
            movement.Loft = _kickState.SelectedLoft;
            movement.Kick = 0f;

            var spacePressed = keyboard.spaceKey.isPressed;
            var fireKick = KeyboardKickMapping.ShouldFireKickOnSpace(spacePressed, _spaceWasPressed);
            _spaceWasPressed = spacePressed;

            if (fireKick)
            {
                var kickCmd = movement;
                kickCmd.Kick = _kickState.SelectedKick;
                kickCmd.Loft = _kickState.SelectedLoft;
                SubmitCommand(kickCmd);
                _lastSubmittedMovement = movement;
                return;
            }

            if (!movement.Equals(_lastSubmittedMovement))
            {
                SubmitCommand(movement);
                _lastSubmittedMovement = movement;
            }
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                return;

            CancelMovementAndPendingKick();
        }

        void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus)
                return;

            CancelMovementAndPendingKick();
        }

        void CancelMovementAndPendingKick()
        {
            _spaceWasPressed = false;
            SubmitZeroCommand();
        }

        void SubmitZeroCommand()
        {
            var zero = PlayerCommand.Zero;
            zero.Loft = _kickState.SelectedLoft;
            SubmitCommand(zero);
            _lastSubmittedMovement = zero;
        }

        void SubmitCommand(PlayerCommand command)
        {
            _commandId++;
            arena.SubmitCommand(playerId, command, _commandId);
        }
    }
}
