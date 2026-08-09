namespace Quantum
{
    using DiceGame.SimShared.Ghost;
    using DiceGame.SimShared.Slide;

    /// <summary>
    /// Shared Frame queries for Domain Ice until-blocked (Ghost + solid place + partition).
    /// </summary>
    public static unsafe class IceSlideFrameQueries
    {
        public static bool TryPlan(
            Frame frame,
            Board board,
            int fromX,
            int fromY,
            int fromTier,
            int dirX,
            int dirY,
            DiceKind moverKind,
            out IceSlideUntilBlockedPlanner.PlanResult plan)
        {
            var caps = CoreDiceBridge.GetCapabilities(moverKind);
            bool TryOccupant(int x, int y, int tier, out GhostSwapRules.Occupant occ)
            {
                return TryGetOccupant(frame, x, y, tier, out occ);
            }

            return IceSlideUntilBlockedPlanner.TryPlan(
                fromX,
                fromY,
                fromTier,
                dirX,
                dirY,
                board.Width,
                board.Height,
                board.PartitionX,
                caps.IgnoresPartitionBoundary,
                caps.IsPlayerPassThrough,
                (x, y) => CanPlaceSolidBottom(frame, board, x, y),
                (x, y) => CanPlaceSolidTop(frame, board, x, y),
                (x, y) => CellOccupancy.HasSolidBottomAt(frame, x, y),
                TryOccupant,
                out plan);
        }

        static bool CanPlaceSolidBottom(Frame frame, Board board, int x, int y)
        {
            return BoardBootstrapSystem.IsInsideBoard(board, x, y)
                && !CellOccupancy.HasSolidBottomAt(frame, x, y);
        }

        static bool CanPlaceSolidTop(Frame frame, Board board, int x, int y)
        {
            return BoardBootstrapSystem.IsInsideBoard(board, x, y)
                && CellOccupancy.HasSolidBottomAt(frame, x, y)
                && !CellOccupancy.HasSolidTopAt(frame, x, y);
        }

        static bool TryGetOccupant(
            Frame frame,
            int x,
            int y,
            int tierNorm,
            out GhostSwapRules.Occupant occupant)
        {
            occupant = default;
            var tier = tierNorm == 1 ? DiceStackTier.Top : DiceStackTier.Bottom;
            if (!CellOccupancy.TryGetAt(frame, x, y, tier, out _, out var dice)
                || dice.IsCarried)
            {
                return false;
            }

            var caps = CoreDiceBridge.GetCapabilities(dice.Kind);
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

        public static void ApplyGhostSwap(Frame frame, in IceSlideUntilBlockedPlanner.PlanResult plan)
        {
            if (!plan.HasGhostSwap)
            {
                return;
            }

            var fromTier = plan.GhostFromTier == 1 ? DiceStackTier.Top : DiceStackTier.Bottom;
            if (!CellOccupancy.TryGetAt(
                    frame,
                    plan.GhostFromX,
                    plan.GhostFromY,
                    fromTier,
                    out var ghost,
                    out _))
            {
                return;
            }

            if (!frame.Unsafe.TryGetPointer<Dice>(ghost, out var ghostDice)
                || !frame.Unsafe.TryGetPointer<GridPose>(ghost, out var ghostPose))
            {
                return;
            }

            ghostPose->X = plan.GhostToX;
            ghostPose->Y = plan.GhostToY;
            ghostDice->Tier = plan.GhostToTier == 1 ? DiceStackTier.Top : DiceStackTier.Bottom;
            BoardBootstrapSystem.SyncTransform(
                frame,
                ghost,
                plan.GhostToX,
                plan.GhostToY,
                ghostDice->Tier);
        }
    }
}
