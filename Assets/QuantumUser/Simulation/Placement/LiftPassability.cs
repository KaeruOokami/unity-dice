namespace Quantum
{
    using DiceGame.SimShared.Lift;

    /// <summary>
    /// Frame adapter over production <see cref="LiftEligibility"/> / facing-only lift target.
    /// </summary>
    public static unsafe class LiftPassability
    {
        public static bool TryResolveLiftTarget(
            Frame frame,
            Board board,
            in PlayerPawn pawn,
            in GridPose pose,
            out EntityRef target)
        {
            target = EntityRef.None;
            if (!LiftEligibility.HasFacing(pawn.FacingX, pawn.FacingY))
            {
                return false;
            }

            var standingDice = CellOccupancy.TryGetStandingDice(frame, pose.X, pose.Y, pawn.IsOnFloor);
            LiftAdjacency.FacingNeighbor(
                pose.X,
                pose.Y,
                pawn.FacingX,
                pawn.FacingY,
                out var nx,
                out var ny);
            return TryResolveAtNeighbor(
                frame,
                board,
                in pawn,
                standingDice,
                nx,
                ny,
                out target);
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
            var effective = EffectiveDiceQuery.ResolveAt(frame, diceEntity, in dice, diceX, diceY);
            var hasTop = CellOccupancy.TryGetTopAt(frame, diceX, diceY, out _, out _);
            return LiftEligibility.CanLift(
                pawn.IsOnFloor,
                pawn.StandingTier == DiceStackTier.Top ? 1 : 0,
                standingDice.IsValid && standingDice == diceEntity,
                effective.Capabilities.CanBeLiftedByPlayer,
                effective.IsPlayerMovable,
                dice.IsCarried,
                dice.IsErasing,
                dice.IsMotionBusy,
                dice.IsSpawning,
                dice.Tier == DiceStackTier.Top ? 1 : 0,
                hasTop);
        }
    }
}
