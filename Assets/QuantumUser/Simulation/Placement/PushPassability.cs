namespace Quantum
{
    /// <summary>
    /// Discrete one-cell push 竕・<c>PushPassability</c> + Normal single-cell slide.
    /// Ice until-blocked / Magnet chain deferred.
    /// </summary>
    public static unsafe class PushPassability
    {
        public static bool TryPushOneCell(
            Frame frame,
            Board board,
            in PlayerPawn pawn,
            in GridPose pose,
            int dx,
            int dy,
            out EntityRef pushed)
        {
            pushed = EntityRef.None;
            var tx = pose.X + dx;
            var ty = pose.Y + dy;
            if (!BoardBootstrapSystem.IsInsideBoard(board, tx, ty))
            {
                return false;
            }

            EntityRef target;
            Dice dice;
            if (pawn.IsOnFloor)
            {
                if (!CellOccupancy.TryGetBottomAt(frame, tx, ty, out target, out dice))
                {
                    return false;
                }
            }
            else
            {
                if (!CellOccupancy.TryGetTopAt(frame, tx, ty, out target, out dice))
                {
                    return false;
                }
            }

            var standing = CellOccupancy.TryGetStandingDice(frame, pose.X, pose.Y, pawn.IsOnFloor);
            if (standing.IsValid && standing == target)
            {
                return false;
            }

            if (dice.IsCarried || dice.IsErasing)
            {
                return false;
            }

            var caps = DiceKindCapabilities.For(dice.Kind);
            if (!caps.CanBePushedByPlayer || caps.SlideUntilBlocked)
            {
                // Ice full slide deferred: treat as not pushable in this MVP.
                return false;
            }

            var destX = tx + dx;
            var destY = ty + dy;
            if (!BoardBootstrapSystem.IsInsideBoard(board, destX, destY))
            {
                return false;
            }

            // Destination must accept same tier placement (or bottom free for bottom push).
            if (dice.Tier == DiceStackTier.Bottom)
            {
                if (!CellOccupancy.CanPlaceBottomAt(frame, board, destX, destY))
                {
                    return false;
                }
            }
            else if (!CellOccupancy.CanPlaceTopAt(frame, board, destX, destY))
            {
                // Allow demote-style: if top can become bottom on empty dest.
                if (!CellOccupancy.CanPlaceBottomAt(frame, board, destX, destY))
                {
                    return false;
                }
            }

            if (!frame.Unsafe.TryGetPointer<Dice>(target, out var dicePtr)
                || !frame.Unsafe.TryGetPointer<GridPose>(target, out var dicePose))
            {
                return false;
            }

            var hadTop = false;
            EntityRef topEntity = EntityRef.None;
            if (dice.Tier == DiceStackTier.Bottom
                && CellOccupancy.TryGetTopAt(frame, tx, ty, out topEntity, out _))
            {
                hadTop = true;
            }

            dicePose->X = destX;
            dicePose->Y = destY;
            if (dice.Tier == DiceStackTier.Top
                && !CellOccupancy.HasSolidBottomAt(frame, destX, destY)
                && CellOccupancy.CanPlaceBottomAt(frame, board, destX, destY))
            {
                // Destination already has this die moving; CanPlaceBottom may see old cell.
                dicePtr->Tier = DiceStackTier.Bottom;
            }

            BoardBootstrapSystem.SyncTransform(frame, target, destX, destY, dicePtr->Tier);

            if (hadTop
                && topEntity.IsValid
                && frame.Unsafe.TryGetPointer<Dice>(topEntity, out var topDice)
                && !topDice->IsErasing)
            {
                // Unsupported top demotes in place.
                topDice->Tier = DiceStackTier.Bottom;
                BoardBootstrapSystem.SyncTransform(frame, topEntity, tx, ty, DiceStackTier.Bottom);
            }

            pushed = target;
            return true;
        }
    }
}
