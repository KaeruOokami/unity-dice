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

        public static bool IsPlayerPassThrough(Frame frame, in Dice dice)
        {
            return EffectiveDiceQuery.Resolve(frame, in dice).IsPlayerPassThrough;
        }

        public static bool HasSolidBottomAt(Frame frame, int x, int y)
        {
            return TryGetBottomAt(frame, x, y, out _, out var dice) && !IsPlayerPassThrough(frame, in dice);
        }

        public static bool HasSolidTopAt(Frame frame, int x, int y)
        {
            return TryGetTopAt(frame, x, y, out _, out var dice) && !IsPlayerPassThrough(frame, in dice);
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
            tier = default;
            if (!DiceGame.SimShared.Lift.CarryPlacementRules.TryResolveTarget(
                    x,
                    y,
                    (cx, cy) => CanPlaceBottomAt(frame, board, cx, cy),
                    (cx, cy) => CanPlaceTopAt(frame, board, cx, cy),
                    // Quantum occupancy: accept-top == place-top for now (Ghost accept deferred).
                    (cx, cy) => CanPlaceTopAt(frame, board, cx, cy),
                    out var tierNorm))
            {
                return false;
            }

            tier = tierNorm == 1 ? DiceStackTier.Top : DiceStackTier.Bottom;
            return true;
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
            if (HasSolidTopAt(frame, x, y))
            {
                isOnFloor = false;
                standingTier = DiceStackTier.Top;
                return;
            }

            if (HasSolidBottomAt(frame, x, y))
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

            if (TryGetTopAt(frame, x, y, out var topEntity, out var topDice)
                && !IsPlayerPassThrough(frame, in topDice))
            {
                return topEntity;
            }

            if (TryGetBottomAt(frame, x, y, out var bottomEntity, out var bottomDice)
                && !IsPlayerPassThrough(frame, in bottomDice))
            {
                return bottomEntity;
            }

            return EntityRef.None;
        }
    }
}
