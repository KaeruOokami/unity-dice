namespace Quantum
{
    using DiceGame.SimShared.Ghost;
    using DiceGame.SimShared.GridMove;
    using CoreKind = DiceGame.Core.DiceKind;
    using CoreOrientation = DiceGame.Core.DiceOrientation;
    using CoreTier = DiceGame.Core.DiceStackTier;
    using GhostLandingMode = DiceGame.Core.GhostLandingMode;
    using SimDiceState = DiceGame.SimShared.Motion.DiceState;

    /// <summary>
    /// Frame adapter implementing production <c>CellOccupancyQuery</c> surface for Domain grid-roll.
    /// </summary>
    public unsafe class FrameGridRollOccupancy : IGridRollOccupancy
    {
        readonly Frame frame;
        readonly Board board;

        public FrameGridRollOccupancy(Frame frame, Board board)
        {
            this.frame = frame;
            this.board = board;
        }

        public bool IsPassableCell(int x, int y)
        {
            return BoardBootstrapSystem.IsInsideBoard(board, x, y);
        }

        public bool BlocksRollBetween(int fromX, int fromY, int toX, int toY)
        {
            return DiceGame.SimShared.Board.PartitionRules.BlocksTraversal(
                board.PartitionX,
                ignoresPartitionBoundary: false,
                fromX,
                toX);
        }

        public bool TryGetOccupancyTier(int x, int y, out CellOccupancyTier tier)
        {
            tier = CellOccupancyTier.Invalid;
            if (!IsPassableCell(x, y))
            {
                return false;
            }

            if (CellOccupancy.HasSolidTopAt(frame, x, y))
            {
                tier = CellOccupancyTier.Top;
                return true;
            }

            if (CellOccupancy.HasSolidBottomAt(frame, x, y))
            {
                tier = CellOccupancyTier.Bottom;
                return true;
            }

            tier = CellOccupancyTier.Floor;
            return true;
        }

        public bool CanOverwriteTopAt(int x, int y)
        {
            return IsPassableCell(x, y)
                && CellOccupancy.HasSolidBottomAt(frame, x, y)
                && !CellOccupancy.HasSolidTopAt(frame, x, y);
        }

        public bool HasSolidTopAt(int x, int y)
        {
            return CellOccupancy.HasSolidTopAt(frame, x, y);
        }

        public bool TryResolveLandingTier(
            CoreTier fromTier,
            int fromX,
            int fromY,
            int cellX,
            int cellY,
            CoreKind moverKind,
            out CoreTier landingTier,
            out GhostLandingMode ghostLanding,
            out SimDiceState ghostFrom,
            out SimDiceState ghostTo)
        {
            landingTier = default;
            ghostLanding = GhostLandingMode.None;
            ghostFrom = default;
            ghostTo = default;

            if (!IsPassableCell(cellX, cellY))
            {
                return false;
            }

            var moverCaps = CoreDiceBridge.GetCapabilities((DiceKind)(int)moverKind);
            if (moverCaps.IsPlayerPassThrough)
            {
                return TryResolveSolidLanding(fromTier, cellX, cellY, out landingTier);
            }

            var fromTierNorm = fromTier == CoreTier.Top ? 1 : 0;
            if (TryGetGhostOccupant(cellX, cellY, fromTierNorm, out var sameTierGhost)
                && GhostSwapRules.TryResolveSameTierCellSwap(
                    fromTierNorm,
                    moverCaps.IsPlayerPassThrough,
                    fromX,
                    fromY,
                    in sameTierGhost,
                    out _,
                    out _,
                    out _,
                    out var gToX,
                    out var gToY,
                    out var gToTier))
            {
                landingTier = fromTier;
                ghostLanding = GhostLandingMode.CellSwap;
                ghostFrom = new SimDiceState(cellX, cellY, CoreOrientation.Default, fromTier, CoreKind.Ghost);
                ghostTo = new SimDiceState(
                    gToX,
                    gToY,
                    CoreOrientation.Default,
                    gToTier == 1 ? CoreTier.Top : CoreTier.Bottom,
                    CoreKind.Ghost);
                return true;
            }

            if (!TryResolveSolidLanding(fromTier, cellX, cellY, out landingTier))
            {
                return false;
            }

            var landingNorm = landingTier == CoreTier.Top ? 1 : 0;
            if (!TryGetGhostOccupant(cellX, cellY, landingNorm, out var landingGhost))
            {
                return true;
            }

            if (fromTier == CoreTier.Top
                && landingTier == CoreTier.Bottom
                && GhostSwapRules.TryResolveInCellPromote(
                    moverCaps.IsPlayerPassThrough,
                    cellX,
                    cellY,
                    in landingGhost,
                    out _,
                    out _))
            {
                ghostLanding = GhostLandingMode.InCellPromoteGhost;
                ghostFrom = new SimDiceState(cellX, cellY, CoreOrientation.Default, CoreTier.Bottom, CoreKind.Ghost);
                ghostTo = new SimDiceState(cellX, cellY, CoreOrientation.Default, CoreTier.Top, CoreKind.Ghost);
                return true;
            }

            if (fromTier == CoreTier.Bottom
                && landingTier == CoreTier.Top
                && GhostSwapRules.TryResolveAscentGhostSwap(
                    moverCaps.IsPlayerPassThrough,
                    fromX,
                    fromY,
                    in landingGhost,
                    out _,
                    out _,
                    out gToX,
                    out gToY))
            {
                ghostLanding = GhostLandingMode.CellSwap;
                ghostFrom = new SimDiceState(cellX, cellY, CoreOrientation.Default, CoreTier.Top, CoreKind.Ghost);
                ghostTo = new SimDiceState(gToX, gToY, CoreOrientation.Default, CoreTier.Bottom, CoreKind.Ghost);
                return true;
            }

            return true;
        }

        bool TryResolveSolidLanding(CoreTier fromTier, int cellX, int cellY, out CoreTier landingTier)
        {
            landingTier = default;
            if (fromTier == CoreTier.Bottom)
            {
                if (CellOccupancy.CanPlaceBottomAt(frame, board, cellX, cellY))
                {
                    landingTier = CoreTier.Bottom;
                    return true;
                }

                if (CanOverwriteTopAt(cellX, cellY))
                {
                    landingTier = CoreTier.Top;
                    return true;
                }

                return false;
            }

            if (CanOverwriteTopAt(cellX, cellY))
            {
                landingTier = CoreTier.Top;
                return true;
            }

            if (CellOccupancy.CanPlaceBottomAt(frame, board, cellX, cellY))
            {
                landingTier = CoreTier.Bottom;
                return true;
            }

            return false;
        }

        bool TryGetGhostOccupant(int x, int y, int tierNorm, out GhostSwapRules.Occupant occupant)
        {
            occupant = default;
            var tier = tierNorm == 1 ? DiceStackTier.Top : DiceStackTier.Bottom;
            if (!CellOccupancy.TryGetAt(frame, x, y, tier, out _, out var dice) || dice.IsCarried)
            {
                return false;
            }

            var caps = CoreDiceBridge.GetCapabilities(dice.Kind);
            if (!caps.AllowsDiceSwapThrough)
            {
                return false;
            }

            occupant = new GhostSwapRules.Occupant
            {
                Valid = true,
                AllowsDiceSwapThrough = caps.AllowsDiceSwapThrough,
                IsPlayerPassThroughKind = caps.IsPlayerPassThrough,
                IsBusy = dice.IsMotionBusy || dice.IsSpawning,
                IsErasing = dice.IsErasing,
                IsCarried = dice.IsCarried,
                Tier = tierNorm,
                CellX = x,
                CellY = y,
            };
            return true;
        }
    }
}
