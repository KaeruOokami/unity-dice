namespace Quantum
{
    /// <summary>
    /// Stacked occupancy queries (Bottom/Top + Ghost solid rules).
    /// Mirrors <c>DiceRegistry</c> / <c>GhostPlacementRules</c> subset without Unity types.
    /// </summary>
    public static unsafe class CellOccupancy
    {
        public static bool TryGetAt(
            Frame frame,
            int x,
            int y,
            DiceStackTier tier,
            out EntityRef entity,
            out Dice dice)
        {
            entity = EntityRef.None;
            dice = default;
            var filter = frame.Filter<Dice, GridPose>();
            while (filter.Next(out var e, out var d, out var pose))
            {
                if (d.IsCarried)
                {
                    continue;
                }

                if (pose.X == x && pose.Y == y && d.Tier == tier)
                {
                    entity = e;
                    dice = d;
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetBottomAt(Frame frame, int x, int y, out EntityRef entity, out Dice dice)
        {
            return TryGetAt(frame, x, y, DiceStackTier.Bottom, out entity, out dice);
        }

        public static bool TryGetTopAt(Frame frame, int x, int y, out EntityRef entity, out Dice dice)
        {
            return TryGetAt(frame, x, y, DiceStackTier.Top, out entity, out dice);
        }

        public static bool IsPlayerPassThrough(Dice dice)
        {
            return DiceKindCapabilities.For(dice.Kind).IsPlayerPassThrough;
        }

        public static bool HasSolidBottomAt(Frame frame, int x, int y)
        {
            return TryGetBottomAt(frame, x, y, out _, out var dice) && !IsPlayerPassThrough(dice);
        }

        public static bool HasSolidTopAt(Frame frame, int x, int y)
        {
            return TryGetTopAt(frame, x, y, out _, out var dice) && !IsPlayerPassThrough(dice);
        }

        public static bool CanPlaceBottomAt(Frame frame, Board board, int x, int y)
        {
            if (!BoardBootstrapSystem.IsInsideBoard(board, x, y))
            {
                return false;
            }

            // Follows DiceRegistry.CanPlaceBottomDiceAt: any Bottom occupant blocks (incl. Ghost).
            return !TryGetBottomAt(frame, x, y, out _, out _);
        }

        public static bool CanPlaceTopAt(Frame frame, Board board, int x, int y)
        {
            if (!BoardBootstrapSystem.IsInsideBoard(board, x, y))
            {
                return false;
            }

            if (!HasSolidBottomAt(frame, x, y) || HasSolidTopAt(frame, x, y))
            {
                return false;
            }

            // Any committed Top (including Ghost) occupies the Top slot for stacking.
            if (TryGetTopAt(frame, x, y, out _, out _))
            {
                return false;
            }

            return true;
        }

        public static bool TryResolveDropTier(
            Frame frame,
            Board board,
            int x,
            int y,
            out DiceStackTier tier)
        {
            if (CanPlaceBottomAt(frame, board, x, y))
            {
                tier = DiceStackTier.Bottom;
                return true;
            }

            if (CanPlaceTopAt(frame, board, x, y))
            {
                tier = DiceStackTier.Top;
                return true;
            }

            tier = default;
            return false;
        }

        public static bool CanPawnEnterCell(Frame frame, Board board, int x, int y, EntityRef ignorePawn)
        {
            if (!BoardBootstrapSystem.IsInsideBoard(board, x, y))
            {
                return false;
            }

            if (BoardBootstrapSystem.IsPawnOccupied(frame, x, y, ignorePawn))
            {
                return false;
            }

            // Solid Top blocks entry; Ghost-only / empty / solid Bottom alone are enterable.
            return !HasSolidTopAt(frame, x, y);
        }

        public static void ResolveStanding(
            Frame frame,
            int x,
            int y,
            out bool isOnFloor,
            out DiceStackTier standingTier)
        {
            if (HasSolidBottomAt(frame, x, y) && !HasSolidTopAt(frame, x, y))
            {
                isOnFloor = false;
                standingTier = DiceStackTier.Bottom;
                return;
            }

            isOnFloor = true;
            standingTier = DiceStackTier.Bottom;
        }

        public static EntityRef TryGetStandingDice(Frame frame, int x, int y, bool isOnFloor)
        {
            if (isOnFloor)
            {
                return EntityRef.None;
            }

            return TryGetBottomAt(frame, x, y, out var entity, out _) ? entity : EntityRef.None;
        }
    }
}
