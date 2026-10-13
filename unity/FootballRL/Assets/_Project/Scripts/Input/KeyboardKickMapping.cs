namespace FootballRL.Input
{
    /// <summary>Logika strzałek i spacji (testowalna bez Input System).</summary>
    public static class KeyboardKickMapping
    {
        public static void ApplyArrowAdjustments(
            ManualKickInputState state,
            bool up, bool down, bool left, bool right)
        {
            if (state == null)
                return;

            if (up)
                state.AdjustAngle(ManualKickInputState.AngleStepDeg);
            if (down)
                state.AdjustAngle(-ManualKickInputState.AngleStepDeg);
            if (left)
                state.AdjustKick(-ManualKickInputState.KickStep);
            if (right)
                state.AdjustKick(ManualKickInputState.KickStep);
        }

        public static bool ShouldFireKickOnSpace(bool spacePressed, bool spaceWasPressedLastFrame)
        {
            return spacePressed && !spaceWasPressedLastFrame;
        }
    }
}
