using DiceGame.Core;

namespace DiceGame.Placement
{
    /// <summary>Compatibility wrapper — prefer <see cref="DiceGridPassability"/>.</summary>
    public static class GroundGridPassability
    {
        public static bool TryEvaluate(
            CellOccupancyQuery occupancyQuery,
            DiceState fromState,
            Direction direction,
            int distance,
            bool hasTopOnSameCell,
            out DiceStackTier landingTier,
            out DiceGridMoveKind moveKind,
            out GhostLandingMode ghostLanding,
            out DiceState ghostFrom,
            out DiceState ghostTo,
            out string rejectReason) {
            return DiceGridPassability.TryEvaluate(
                occupancyQuery,
                fromState,
                direction,
                distance,
                hasTopOnSameCell,
                PassabilityContext.ForGround(footingWorldY: 0f),
                out landingTier,
                out moveKind,
                out ghostLanding,
                out ghostFrom,
                out ghostTo,
                out rejectReason);
        }
    }
}
