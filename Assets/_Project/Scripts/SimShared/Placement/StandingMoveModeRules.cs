namespace DiceGame.SimShared.Placement
{
    /// <summary>
    /// Standing-move mode subset matching <c>DiceBehaviorBase.ResolveStandingMoveMode</c>.
    /// </summary>
    public enum StandingMoveMode
    {
        None = 0,
        PlayerOnly = 1,
        Roll = 2,
        Slide = 3,
    }

    public static class StandingMoveModeRules
    {
        public static StandingMoveMode Resolve(
            bool canGridRoll,
            bool slideUntilBlocked,
            bool isPlayerPassThrough)
        {
            if (isPlayerPassThrough)
            {
                return StandingMoveMode.None;
            }

            if (slideUntilBlocked)
            {
                return StandingMoveMode.Slide;
            }

            if (canGridRoll)
            {
                return StandingMoveMode.Roll;
            }

            return StandingMoveMode.PlayerOnly;
        }

        /// <summary>Walk couple roll (Wood / Normal roll kinds).</summary>
        public static bool AllowsWalkCoupleRoll(StandingMoveMode mode)
        {
            return mode == StandingMoveMode.Roll;
        }

        /// <summary>Walk couple ice slide (production GroundIceSlide).</summary>
        public static bool AllowsWalkCoupleSlide(StandingMoveMode mode)
        {
            return mode == StandingMoveMode.Slide;
        }
    }
}
