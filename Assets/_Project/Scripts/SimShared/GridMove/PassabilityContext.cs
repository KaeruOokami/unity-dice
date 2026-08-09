namespace DiceGame.SimShared.GridMove
{
    /// <summary>
    /// Copied from production <c>PassabilityContext</c> (owner slot omitted for Domain).
    /// </summary>
    public readonly struct PassabilityContext
    {
        public bool IsJumping { get; }
        public bool AllowJumpGridMove { get; }
        public bool AllowJumpTierChange { get; }
        public float FootingWorldY { get; }

        PassabilityContext(
            bool isJumping,
            bool allowJumpGridMove,
            bool allowJumpTierChange,
            float footingWorldY)
        {
            IsJumping = isJumping;
            AllowJumpGridMove = allowJumpGridMove;
            AllowJumpTierChange = allowJumpTierChange;
            FootingWorldY = footingWorldY;
        }

        public static PassabilityContext ForGround(float footingWorldY)
        {
            return new PassabilityContext(false, false, false, footingWorldY);
        }

        public static PassabilityContext Jump(
            bool allowJumpGridMove,
            bool allowJumpTierChange,
            float footingWorldY)
        {
            return new PassabilityContext(true, allowJumpGridMove, allowJumpTierChange, footingWorldY);
        }
    }

    public enum CellOccupancyTier
    {
        Invalid = -1,
        Floor = 0,
        Bottom = 1,
        Top = 2
    }

    /// <summary>
    /// Domain surface of production <c>CellOccupancyQuery</c> used by grid-roll passability.
    /// </summary>
    public interface IGridRollOccupancy
    {
        bool IsPassableCell(int x, int y);
        bool BlocksRollBetween(int fromX, int fromY, int toX, int toY);
        bool TryGetOccupancyTier(int x, int y, out CellOccupancyTier tier);
        bool CanOverwriteTopAt(int x, int y);
        bool TryResolveLandingTier(
            DiceGame.Core.DiceStackTier fromTier,
            int fromX,
            int fromY,
            int cellX,
            int cellY,
            DiceGame.Core.DiceKind moverKind,
            out DiceGame.Core.DiceStackTier landingTier,
            out DiceGame.Core.GhostLandingMode ghostLanding,
            out Motion.DiceState ghostFrom,
            out Motion.DiceState ghostTo);
        bool HasSolidTopAt(int x, int y);
    }
}
