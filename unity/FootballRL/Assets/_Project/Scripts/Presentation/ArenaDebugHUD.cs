using FootballRL.Contracts;
using FootballRL.Gameplay;
using FootballRL.Input;
using UnityEngine;

namespace FootballRL.Presentation
{
    /// <summary>HUD komend, kopnięcia i ostatniej próby.</summary>
    public class ArenaDebugHUD : MonoBehaviour
    {
        [SerializeField] ArenaController arena;
        [SerializeField] KeyboardCommandSource keyboardSource;
        [SerializeField] int displayPlayerId;

        public void Configure(ArenaController arenaController, KeyboardCommandSource input, int playerId)
        {
            arena = arenaController;
            keyboardSource = input;
            displayPlayerId = playerId;
        }

        void OnGUI()
        {
            if (arena == null)
                return;

            if (!arena.TryGetActiveCommand(displayPlayerId, out var cmd))
                cmd = PlayerCommand.Zero;

            var angleDeg = keyboardSource != null
                ? keyboardSource.KickState.SelectedAngleDeg
                : KickMath.LoftToAngleDeg(cmd.Loft);
            var kickDisplay = keyboardSource != null
                ? keyboardSource.KickState.SelectedKick
                : cmd.Kick;
            var loftDisplay = keyboardSource != null
                ? keyboardSource.KickState.SelectedLoft
                : cmd.Loft;

            var cooldown = arena.GetKickCooldownRemaining(displayPlayerId);
            arena.TryGetLastKickAttempt(displayPlayerId, out var lastAttempt);
            var attemptLine = FormatLastAttempt(lastAttempt);
            var phaseLine = arena.Phase.ToString();
            var timeLine = $"{arena.GetRemainingSeconds():F1} s (ep. {arena.EpisodeId})";
            var endLine = arena.Phase == ArenaPhase.Ended
                ? FormatEndReason(arena.LastEndReason, arena)
                : "—";

            const int pad = 10;
            var rect = new Rect(pad, pad, 400f, 250f);
            GUI.Box(rect, "Arena — G4 runda");

            var inner = new Rect(rect.x + 8f, rect.y + 24f, rect.width - 16f, rect.height - 32f);
            GUI.Label(inner,
                $"Player {displayPlayerId}  |  Faza: {phaseLine}\n" +
                $"Czas: {timeLine}  |  Koniec: {endLine}\n" +
                $"Forward: {cmd.Forward:F2}  Strafe: {cmd.Strafe:F2}  Turn: {cmd.Turn:F2}\n" +
                $"Kąt: {angleDeg:F1}°  Loft: {loftDisplay:F2}\n" +
                $"Siła (Kick): {kickDisplay:F2}  Cooldown: {cooldown:F2} s\n" +
                $"Ostatnia próba: {attemptLine}\n" +
                "W/S A/D Q/E | ↑↓ kąt | ←→ siła | Spacja — strzał | R — reset");
        }

        static string FormatEndReason(RoundEndReason reason, ArenaController arena)
        {
            if (reason == RoundEndReason.None)
                return "—";

            if (reason == RoundEndReason.Goal && arena.TryGetEndState(out var state) && state.HasScoringTeam)
                return $"{reason}, drużyna {state.ScoringTeamId}";

            return reason.ToString();
        }

        static string FormatLastAttempt(KickAttemptResult attempt)
        {
            if (!attempt.Attempted)
                return "—";

            if (attempt.Success)
                return $"UDANE impuls {attempt.ImpulseMagnitude:F2} N·s @ {attempt.AngleDeg:F1}°";

            return attempt.Reason switch
            {
                KickRejectReason.BelowThreshold => "poniżej progu",
                KickRejectReason.Cooldown => "cooldown",
                KickRejectReason.OutOfRange => "zasięg",
                KickRejectReason.OutOfCone => "za plecami / stożek",
                KickRejectReason.TooHigh => "za wysoko",
                KickRejectReason.Occluded => "przesłonięcie",
                _ => "odmowa"
            };
        }
    }
}
