namespace DiceGame.SimShared.Lift
{
    /// <summary>
    /// Copied from production <c>CharacterController.LiftPhase</c> state machine.
    /// Phase advances on logical busy complete (production OnLiftComplete / OnPlaceComplete).
    /// </summary>
    public static class LiftPhaseMachine
    {
        public const int None = 0;
        public const int Lifting = 1;
        public const int Carrying = 2;
        public const int Placing = 3;

        public static bool IsBusy(int phase)
        {
            return phase == Lifting || phase == Placing;
        }

        public static bool CanBeginLift(int phase, bool isJumping, bool hasPushFollow)
        {
            // CharacterController.TryBeginLift gates.
            return phase == None && !isJumping && !hasPushFollow;
        }

        public static bool CanBeginPlace(int phase)
        {
            return phase == Carrying;
        }

        /// <summary>
        /// Production: after TryBeginCarry logical busy ends → OnLiftComplete.
        /// </summary>
        public static int OnLiftLogicalComplete(int phase)
        {
            if (phase == Lifting)
            {
                return Carrying;
            }

            return phase;
        }

        /// <summary>
        /// Production: after TryPlaceAt logical busy ends → OnPlaceComplete.
        /// </summary>
        public static int OnPlaceLogicalComplete(int phase)
        {
            if (phase == Placing)
            {
                return None;
            }

            return phase;
        }

        /// <summary>
        /// Production TryPlaceAt failure rolls phase back to Carrying.
        /// </summary>
        public static int OnPlaceRejected()
        {
            return Carrying;
        }
    }
}
