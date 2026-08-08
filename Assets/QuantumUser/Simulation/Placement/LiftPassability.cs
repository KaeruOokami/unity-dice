namespace Quantum
{
    /// <summary>
    /// Quantum port of <c>LiftPassability</c> + <c>CharacterLiftTargetQuery</c> (adjacent lift only).
    /// </summary>
    public static unsafe class LiftPassability
    {
        static readonly int[] NeighborX = { 0, 1, 0, -1 };
        static readonly int[] NeighborY = { 1, 0, -1, 0 };

        public static bool TryResolveLiftTarget(
            Frame frame,
            Board board,
            in PlayerPawn pawn,
            in GridPose pose,
            out EntityRef target)
        {
            target = EntityRef.None;
            var standingDice = CellOccupancy.TryGetStandingDice(frame, pose.X, pose.Y, pawn.IsOnFloor);

            if ((pawn.FacingX != 0 || pawn.FacingY != 0)
                && TryResolveAtNeighbor(
                    frame,
                    board,
                    in pawn,
                    standingDice,
                    pose.X + pawn.FacingX,
                    pose.Y + pawn.FacingY,
                    out target))
            {
                return true;
            }

            for (var i = 0; i < 4; i++)
            {
                if (TryResolveAtNeighbor(
                        frame,
                        board,
                        in pawn,
                        standingDice,
                        pose.X + NeighborX[i],
                        pose.Y + NeighborY[i],
                        out target))
                {
                    return true;
                }
            }

            return false;
        }

        static bool TryResolveAtNeighbor(
            Frame frame,
            Board board,
            in PlayerPawn pawn,
            EntityRef standingDice,
            int nx,
            int ny,
            out EntityRef target)
        {
            target = EntityRef.None;
            if (!BoardBootstrapSystem.IsInsideBoard(board, nx, ny))
            {
                return false;
            }

            if (CellOccupancy.TryGetTopAt(frame, nx, ny, out var topEntity, out var topDice)
                && CanLift(frame, in pawn, standingDice, topEntity, in topDice, nx, ny))
            {
                target = topEntity;
                return true;
            }

            if (CellOccupancy.TryGetBottomAt(frame, nx, ny, out var bottomEntity, out var bottomDice)
                && CanLift(frame, in pawn, standingDice, bottomEntity, in bottomDice, nx, ny))
            {
                target = bottomEntity;
                return true;
            }

            return false;
        }

        public static bool CanLift(
            Frame frame,
            in PlayerPawn pawn,
            EntityRef standingDice,
            EntityRef diceEntity,
            in Dice dice,
            int diceX,
            int diceY)
        {
            if (dice.IsCarried || dice.IsErasing)
            {
                return false;
            }

            if (standingDice.IsValid && standingDice == diceEntity)
            {
                return false;
            }

            if (!DiceKindCapabilities.For(dice.Kind).CanBeLiftedByPlayer)
            {
                return false;
            }

            if (pawn.IsOnFloor)
            {
                if (dice.Tier == DiceStackTier.Top)
                {
                    return true;
                }

                return dice.Tier == DiceStackTier.Bottom
                    && !CellOccupancy.TryGetTopAt(frame, diceX, diceY, out _, out _);
            }

            if (pawn.StandingTier == DiceStackTier.Bottom)
            {
                return true;
            }

            return dice.Tier == DiceStackTier.Top;
        }
    }
}
