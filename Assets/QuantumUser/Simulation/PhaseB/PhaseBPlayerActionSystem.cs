namespace Quantum
{
    /// <summary>
    /// Phase B: discrete cell move + lift/drop dice from player input buttons.
    /// Jump is reserved for a future hop rule; currently treated as an alternate pickup on the same cell.
    /// </summary>
    public unsafe class PhaseBPlayerActionSystem : SystemMainThreadFilter<PhaseBPlayerActionSystem.Filter>
    {
        public override void Update(Frame frame, ref Filter filter)
        {
            if (!frame.TryGetSingleton<PhaseBBoard>(out var board) || !board.Initialized)
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
                TryMove(frame, board, ref filter, dx, dy);
            }

            if (input->Lift.WasPressed || input->Jump.WasPressed)
            {
                TryLiftOrDrop(frame, ref filter);
            }
        }

        static void TryMove(Frame frame, PhaseBBoard board, ref Filter filter, int dx, int dy)
        {
            var nextX = filter.Pose->X + dx;
            var nextY = filter.Pose->Y + dy;
            if (!PhaseBBootstrapSystem.IsInsideBoard(board, nextX, nextY))
            {
                return;
            }

            if (PhaseBBootstrapSystem.IsPawnOccupied(frame, nextX, nextY, filter.Entity))
            {
                return;
            }

            filter.Pose->X = nextX;
            filter.Pose->Y = nextY;
            PhaseBBootstrapSystem.SyncTransform(frame, filter.Entity, nextX, nextY);

            if (filter.Pawn->HasCarriedDice && filter.Pawn->CarriedDice.IsValid)
            {
                if (frame.Unsafe.TryGetPointer<PhaseBGridPose>(filter.Pawn->CarriedDice, out var carriedPose))
                {
                    carriedPose->X = nextX;
                    carriedPose->Y = nextY;
                    PhaseBBootstrapSystem.SyncTransform(frame, filter.Pawn->CarriedDice, nextX, nextY);
                }
            }
        }

        static void TryLiftOrDrop(Frame frame, ref Filter filter)
        {
            if (filter.Pawn->HasCarriedDice)
            {
                DropCarriedDice(frame, ref filter);
                return;
            }

            var dice = PhaseBBootstrapSystem.TryFindUncarriedDiceAt(
                frame,
                filter.Pose->X,
                filter.Pose->Y,
                EntityRef.None);
            if (dice.IsValid == false)
            {
                return;
            }

            if (frame.Unsafe.TryGetPointer<PhaseBDice>(dice, out var dicePtr) == false)
            {
                return;
            }

            dicePtr->IsCarried = true;
            filter.Pawn->CarriedDice = dice;
            filter.Pawn->HasCarriedDice = true;
        }

        static void DropCarriedDice(Frame frame, ref Filter filter)
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
            if (PhaseBBootstrapSystem.IsPawnOccupied(frame, x, y, filter.Entity))
            {
                return;
            }

            var otherDice = PhaseBBootstrapSystem.TryFindUncarriedDiceAt(frame, x, y, dice);
            if (otherDice.IsValid)
            {
                return;
            }

            if (frame.Unsafe.TryGetPointer<PhaseBDice>(dice, out var dicePtr))
            {
                dicePtr->IsCarried = false;
            }

            if (frame.Unsafe.TryGetPointer<PhaseBGridPose>(dice, out var pose))
            {
                pose->X = x;
                pose->Y = y;
                PhaseBBootstrapSystem.SyncTransform(frame, dice, x, y);
            }

            filter.Pawn->HasCarriedDice = false;
            filter.Pawn->CarriedDice = EntityRef.None;
        }

        public struct Filter
        {
            public EntityRef Entity;
            public PhaseBPlayerPawn* Pawn;
            public PhaseBGridPose* Pose;
        }
    }
}
