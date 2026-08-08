namespace Quantum
{
    using System;
    using Photon.Deterministic;

    /// <summary>
    /// Phase B: create board singleton, seed dice, and spawn a pawn when a player is added.
    /// </summary>
    public unsafe class PhaseBBootstrapSystem : SystemMainThread, ISignalOnPlayerAdded
    {
        public override void OnInit(Frame frame)
        {
            EnsureBoard(frame);
        }

        public override void Update(Frame frame)
        {
            EnsureBoard(frame);
        }

        public void OnPlayerAdded(Frame frame, PlayerRef player, bool firstTime)
        {
            EnsureBoard(frame);

            var existing = frame.Filter<PhaseBPlayerPawn>();
            while (existing.Next(out _, out var pawn))
            {
                if (pawn.Player == player)
                {
                    return;
                }
            }

            var board = frame.GetSingleton<PhaseBBoard>();
            var entity = frame.Create();
            var startX = player == 0 ? 0 : board.Width - 1;
            frame.Set(entity, new PhaseBPlayerPawn
            {
                Player = player,
                CarriedDice = EntityRef.None,
                HasCarriedDice = false,
            });
            frame.Set(entity, new PhaseBGridPose
            {
                X = startX,
                Y = 0,
            });
            SyncTransform(frame, entity, startX, 0);
        }

        static void EnsureBoard(Frame frame)
        {
            var board = frame.Unsafe.GetOrAddSingletonPointer<PhaseBBoard>();
            if (board->Initialized)
            {
                return;
            }

            board->Width = ResolveWidth(frame);
            board->Height = ResolveHeight(frame);
            board->Initialized = true;
            SpawnInitialDice(frame, board->Width, board->Height);
        }

        static void SpawnInitialDice(Frame frame, int width, int height)
        {
            var count = frame.RuntimeConfig.PhaseBInitialDiceCount;
            if (count <= 0)
            {
                count = PhaseBSimDefaults.InitialDiceCount;
            }

            for (var i = 0; i < count; i++)
            {
                var x = 1 + (i % Math.Max(1, width - 2));
                var y = 1 + (i % Math.Max(1, height - 2));
                if (IsPawnOccupied(frame, x, y, EntityRef.None)
                    || TryFindUncarriedDiceAt(frame, x, y, EntityRef.None).IsValid)
                {
                    continue;
                }

                var face = frame.RNG->Next(
                    PhaseBSimDefaults.MinFaceValue,
                    PhaseBSimDefaults.MaxFaceValue + 1);
                var dice = frame.Create();
                frame.Set(dice, new PhaseBDice
                {
                    FaceValue = face,
                    IsCarried = false,
                });
                frame.Set(dice, new PhaseBGridPose { X = x, Y = y });
                SyncTransform(frame, dice, x, y);
            }
        }

        internal static void SyncTransform(Frame frame, EntityRef entity, int x, int y)
        {
            var cellSize = frame.RuntimeConfig.PhaseBCellSize;
            if (cellSize <= FP._0)
            {
                cellSize = FP._1;
            }

            var position = new FPVector2(cellSize * x, cellSize * y);
            if (frame.Has<Transform2D>(entity))
            {
                var transform = frame.Get<Transform2D>(entity);
                transform.Position = position;
                frame.Set(entity, transform);
            }
            else
            {
                frame.Set(entity, Transform2D.Create(position));
            }
        }

        internal static bool IsInsideBoard(PhaseBBoard board, int x, int y)
        {
            return x >= 0 && y >= 0 && x < board.Width && y < board.Height;
        }

        /// <summary>True when another player pawn occupies the cell. Dice do not block standing (needed to lift).</summary>
        internal static bool IsPawnOccupied(Frame frame, int x, int y, EntityRef ignore)
        {
            var filter = frame.Filter<PhaseBPlayerPawn, PhaseBGridPose>();
            while (filter.Next(out var entity, out _, out var pose))
            {
                if (entity == ignore)
                {
                    continue;
                }

                if (pose.X == x && pose.Y == y)
                {
                    return true;
                }
            }

            return false;
        }

        internal static EntityRef TryFindUncarriedDiceAt(Frame frame, int x, int y, EntityRef ignore)
        {
            var filter = frame.Filter<PhaseBDice, PhaseBGridPose>();
            while (filter.Next(out var entity, out var dice, out var pose))
            {
                if (entity == ignore || dice.IsCarried)
                {
                    continue;
                }

                if (pose.X == x && pose.Y == y)
                {
                    return entity;
                }
            }

            return EntityRef.None;
        }

        static int ResolveWidth(Frame frame)
        {
            var width = frame.RuntimeConfig.PhaseBBoardWidth;
            return width > 0 ? width : PhaseBSimDefaults.BoardWidth;
        }

        static int ResolveHeight(Frame frame)
        {
            var height = frame.RuntimeConfig.PhaseBBoardHeight;
            return height > 0 ? height : PhaseBSimDefaults.BoardHeight;
        }
    }
}
