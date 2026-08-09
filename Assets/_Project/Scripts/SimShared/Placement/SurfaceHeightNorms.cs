namespace DiceGame.SimShared.Placement
{
    /// <summary>
    /// Height norms matching production <c>SurfaceHeightLevel</c>: Floor=0, Bottom=1, Top=2.
    /// Distinct from push stack tiers (Bottom=0, Top=1).
    /// </summary>
    public static class SurfaceHeightNorms
    {
        public const int Floor = 0;
        public const int Bottom = 1;
        public const int Top = 2;

        public static int FromStanding(bool isOnFloor, bool standingIsTop)
        {
            if (isOnFloor)
            {
                return Floor;
            }

            return standingIsTop ? Top : Bottom;
        }

        /// <summary>
        /// Production <c>WalkTransferPolicy.IsLandingTierAtOrBelowStandingTier</c>:
        /// standing Bottom cannot land on Top.
        /// </summary>
        public static bool IsLandingTierAtOrBelowStanding(int standingNorm, int landingNorm)
        {
            if (standingNorm == Bottom && landingNorm == Top)
            {
                return false;
            }

            return true;
        }
    }
}
