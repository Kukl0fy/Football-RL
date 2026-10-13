using FootballRL.Contracts;

namespace FootballRL.Input
{
    /// <summary>Mapowanie klawiszy → PlayerCommand (czysta funkcja do testów).</summary>
    public static class KeyboardCommandMapping
    {
        public static PlayerCommand FromKeyStates(bool w, bool s, bool a, bool d, bool q, bool e)
        {
            var forward = AxisFromPair(w, s);
            var strafe = AxisFromPair(d, a);
            var turn = AxisFromPair(e, q);

            return new PlayerCommand
            {
                Forward = forward,
                Strafe = strafe,
                Turn = turn,
                Kick = 0f,
                Loft = -1f
            };
        }

        static float AxisFromPair(bool positive, bool negative)
        {
            if (positive && negative)
                return 0f;
            if (positive)
                return 1f;
            if (negative)
                return -1f;
            return 0f;
        }
    }
}
