namespace Quantum
{
    /// <summary>
    /// Player actions: move (+ push), hop (Jump), adjacent lift/drop, match trigger with ownership.
    /// </summary>
    public unsafe class PlayerActionSystem : SystemMainThreadFilter<PlayerActionSystem.Filter>
    {
        public override void Update(Frame frame, ref Filter filter)
        {
            if (!frame.TryGetSingleton<Board>(out var board) || !board.Initialized)
            {
                return;
            }

            var input = frame.GetPlayerInput(filter.Pawn->Player);
            var dx = 0;
            var dy = 0;
            if (input->MoveN.WasPressed)
            {
                dy = 1;
            }
            else if (input->MoveS.WasPressed)
            {
                dy = -1;
            }
            else if (input->MoveE.WasPressed)
            {
                dx = 1;
            }
            else if (input->MoveW.WasPressed)
            {
                dx = -1;
            }

            if (dx != 0 || dy != 0)
            {
                TryMoveOrPush(frame, board, ref filter, dx, dy);
            }

            if (input->Jump.WasPressed)
            {
                TryHop(frame, board, ref filter);
            }

            if (input->Lift.WasPressed)
            {
                TryLiftOrDrop(frame, board, ref filter);
            }
        }

        static void TryMoveOrPush(Frame frame, Board board, ref Filter filter, int dx, int dy)
        {
            filter.Pawn->FacingX = dx;
            filter.Pawn->FacingY = dy;

            var nextX = filter.Pose->X + dx;
            var nextY = filter.Pose->Y + dy;
            if (CellOccupancy.CanPawnEnterCell(frame, board, nextX, nextY, filter.Entity))
            {
                ApplyPawnMove(frame, ref filter, nextX, nextY);
                return;
            }

            // Blocked by solid occupancy: attempt one-cell push then step in.
            var pawn = *filter.Pawn;
            var pose = *filter.Pose;
            if (!PushPassability.TryPushOneCell(frame, board, in pawn, in pose, dx, dy, out _))
            {
                return;
            }

            if (CellOccupancy.CanPawnEnterCell(frame, board, nextX, nextY, filter.Entity))
            {
                ApplyPawnMove(frame, ref filter, nextX, nextY);
            }
        }

        static void TryHop(Frame frame, Board board, ref Filter filter)
        {
            var dx = filter.Pawn->FacingX;
            var dy = filter.Pawn->FacingY;
            if (dx == 0 && dy == 0)
            {
                dy = 1;
            }

            var nextX = filter.Pose->X + dx;
            var nextY = filter.Pose->Y + dy;
            if (!CellOccupancy.CanPawnEnterCell(frame, board, nextX, nextY, filter.Entity))
            {
                return;
            }

            ApplyPawnMove(frame, ref filter, nextX, nextY);
        }

        static void ApplyPawnMove(Frame frame, ref Filter filter, int nextX, int nextY)
        {
            filter.Pose->X = nextX;
            filter.Pose->Y = nextY;

            CellOccupancy.ResolveStanding(frame, nextX, nextY, out var onFloor, out var standingTier);
            filter.Pawn->IsOnFloor = onFloor;
            filter.Pawn->StandingTier = standingTier;
            BoardBootstrapSystem.SyncTransform(frame, filter.Entity, nextX, nextY, standingTier);

            if (filter.Pawn->HasCarriedDice && filter.Pawn->CarriedDice.IsValid)
            {
                if (frame.Unsafe.TryGetPointer<GridPose>(filter.Pawn->CarriedDice, out var carriedPose))
                {
                    carriedPose->X = nextX;
                    carriedPose->Y = nextY;
                    BoardBootstrapSystem.SyncTransform(
                        frame,
                        filter.Pawn->CarriedDice,
                        nextX,
                        nextY,
                        DiceStackTier.Top);
                }
            }
        }

        static void TryLiftOrDrop(Frame frame, Board board, ref Filter filter)
        {
            if (filter.Pawn->HasCarriedDice)
            {
                DropCarriedDice(frame, board, ref filter);
                return;
            }

            var pawn = *filter.Pawn;
            var pose = *filter.Pose;
            if (!LiftPassability.TryResolveLiftTarget(frame, board, in pawn, in pose, out var dice)
                || !dice.IsValid)
            {
                return;
            }

            if (frame.Unsafe.TryGetPointer<Dice>(dice, out var dicePtr) == false)
            {
                return;
            }

            dicePtr->IsCarried = true;
            dicePtr->Owner = filter.Pawn->Player;
            filter.Pawn->CarriedDice = dice;
            filter.Pawn->HasCarriedDice = true;

            if (frame.Unsafe.TryGetPointer<GridPose>(dice, out var dicePose))
            {
                dicePose->X = filter.Pose->X;
                dicePose->Y = filter.Pose->Y;
                BoardBootstrapSystem.SyncTransform(
                    frame,
                    dice,
                    filter.Pose->X,
                    filter.Pose->Y,
                    DiceStackTier.Top);
            }
        }

        static void DropCarriedDice(Frame frame, Board board, ref Filter filter)
        {
            var dice = filter.Pawn->CarriedDice;
            if (dice.IsValid == false)
            {
                filter.Pawn->HasCarriedDice = false;
                filter.Pawn->CarriedDice = EntityRef.None;
                return;
            }

            var x = filter.Pose->X;
            var y = filter.Pose->Y;
            if (!CellOccupancy.TryResolveDropTier(frame, board, x, y, out var tier))
            {
                return;
            }

            if (frame.Unsafe.TryGetPointer<Dice>(dice, out var dicePtr) == false)
            {
                return;
            }

            dicePtr->IsCarried = false;
            dicePtr->Tier = tier;
            dicePtr->Owner = filter.Pawn->Player;

            if (frame.Unsafe.TryGetPointer<GridPose>(dice, out var pose))
            {
                pose->X = x;
                pose->Y = y;
                BoardBootstrapSystem.SyncTransform(frame, dice, x, y, tier);
            }

            filter.Pawn->HasCarriedDice = false;
            filter.Pawn->CarriedDice = EntityRef.None;

            CellOccupancy.ResolveStanding(frame, x, y, out var onFloor, out var standingTier);
            filter.Pawn->IsOnFloor = onFloor;
            filter.Pawn->StandingTier = standingTier;

            frame.SetSingleton(new MatchPending
            {
                HasPending = true,
                ActionDice = dice,
                ActingPlayer = filter.Pawn->Player,
            });
        }

        public struct Filter
        {
            public EntityRef Entity;
            public PlayerPawn* Pawn;
            public GridPose* Pose;
        }
    }
}
