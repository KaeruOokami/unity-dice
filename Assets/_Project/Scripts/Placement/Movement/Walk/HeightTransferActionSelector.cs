namespace DiceGame.Placement
{
    /// <summary>
    /// Decision table: <see cref="HeightTransferFacts"/> → one <see cref="HeightTransferAction"/>.
    /// </summary>
    public static class HeightTransferActionSelector
    {
        public static HeightTransferAction Select(in HeightTransferFacts f) {
            if (f.FromLevel == SurfaceHeightLevel.Floor || f.StandingDice == null) {
                return HeightTransferAction.Blocked;
            }

            if (!f.PreferCoupledGridRoll && f.CanSameTierTransfer) {
                return HeightTransferAction.SameTierTransfer;
            }

            if (!f.HasLowerLevelFallbackTarget) {
                return HeightTransferAction.Blocked;
            }

            if (f.CanDissolveDescentHold) {
                return HeightTransferAction.DissolveDescentHold;
            }

            if (f.CanLowerLevelPlayerOnlyJump) {
                return HeightTransferAction.LowerLevelPlayerOnlyJump;
            }

            return HeightTransferAction.Blocked;
        }

        public static bool IsStepHeightRejectReason(string rejectReason) {
            return rejectReason != null && rejectReason.StartsWith("step-height");
        }
    }
}
