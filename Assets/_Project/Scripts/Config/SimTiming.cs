namespace DiceGame.Config
{
    /// <summary>
    /// Shared lockstep timing. SO duration fields store ticks at this rate.
    /// </summary>
    public static class SimTiming
    {
        public const int TickHz = 60;
        public const float TickSeconds = 1f / TickHz;

        public static float TicksToSeconds(int ticks)
        {
            if (ticks <= 0)
            {
                return 0f;
            }

            return ticks * TickSeconds;
        }

        public static int ClampTicks(int ticks, int fallback)
        {
            return ticks > 0 ? ticks : fallback;
        }
    }
}
