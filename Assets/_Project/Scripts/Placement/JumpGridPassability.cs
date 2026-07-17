using DiceGame.Core;

namespace DiceGame.Placement
{
    /// <summary>Compatibility wrapper — prefer <see cref="DiceGridPassability"/>.</summary>
    public static class JumpGridPassability
    {
        public static bool TryEvaluate(
            CellOccupancyQuery occupancyQuery,
            DiceState fromState,
            Direction direction,
            int distance,
            bool hasTopOnSameCell,
            PassabilityContext context,
            out DiceStackTier landingTier,
            out DiceGridMoveKind moveKind,
            out GhostLandingMode ghostLanding,
            out DiceState ghostFrom,
            out DiceState ghostTo,
            out string rejectReason) {
            landingTier = default;
            moveKind = default;
            ghostLanding = GhostLandingMode.None;
            ghostFrom = default;
            ghostTo = default;
            rejectReason = null;

            if (!context.IsJumping) {
                rejectReason = "not-jumping";
                return false;
            }

            return DiceGridPassability.TryEvaluate(
                occupancyQuery,
                fromState,
                direction,
                distance,
                hasTopOnSameCell,
                context,
                out landingTier,
                out moveKind,
                out ghostLanding,
                out ghostFrom,
                out ghostTo,
                out rejectReason);
        }
    }
}
