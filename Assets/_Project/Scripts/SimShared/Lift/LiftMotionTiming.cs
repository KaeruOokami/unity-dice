namespace DiceGame.SimShared.Lift
{
    /// <summary>
    /// Thin alias kept for call-site stability; logic lives in <see cref="LiftPhaseMachine"/>
    /// (copied from production CharacterController.LiftPhase).
    /// </summary>
    public static class LiftMotionTiming
    {
        public const int PhaseNone = LiftPhaseMachine.None;
        public const int PhaseLifting = LiftPhaseMachine.Lifting;
        public const int PhaseCarrying = LiftPhaseMachine.Carrying;
        public const int PhasePlacing = LiftPhaseMachine.Placing;

        public static int ResolveBusyTicks(int durationTicks, int fallback)
        {
            if (durationTicks > 0)
            {
                return durationTicks;
            }

            return fallback > 0 ? fallback : 1;
        }

        public static bool IsLiftBusy(int phase) => LiftPhaseMachine.IsBusy(phase);
    }
}
