namespace Quantum
{
    using System;
    using Photon.Deterministic;

    /// <summary>
    /// Board bootstrap: board singleton, mixed-kind stacked dice seed, pawn spawn.
    /// </summary>
    public unsafe class BoardBootstrapSystem : SystemMainThread, ISignalOnPlayerAdded
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

            var existing = frame.Filter<PlayerPawn>();
            while (existing.Next(out _, out var pawn))
            {
                if (pawn.Player == player)
                {
                    return;
                }
            }

            var board = frame.GetSingleton<Board>();
            var entity = frame.Create();
            var startX = player == 0 ? 0 : board.Width - 1;
            frame.Set(entity, new PlayerPawn
            {
                Player = player,
                CarriedDice = EntityRef.None,
                HasCarriedDice = false,
                IsOnFloor = true,
                StandingTier = DiceStackTier.Bottom,
                FacingX = 0,
                FacingY = 1,
            });
            frame.Set(entity, new GridPose
            {
                X = startX,
                Y = 0,
            });
            SyncTransform(frame, entity, startX, 0);
        }

        static void EnsureBoard(Frame frame)
        {
            var board = frame.Unsafe.GetOrAddSingletonPointer<Board>();
            if (board->Initialized)
            {
                return;
            }

            board->Width = ResolveWidth(frame);
            board->Height = ResolveHeight(frame);
            board->Initialized = true;

            frame.GetOrAddSingleton<MatchPending>();
            frame.GetOrAddSingleton<SpawnState>();
            frame.GetOrAddSingleton<VersusAttackState>();
            SpawnInitialDice(frame, board->Width, board->Height);
        }

        static void SpawnInitialDice(Frame frame, int width, int height)
        {
            // Scripted seed: Iron, Ghost, and a face-2 pair for match demos, then fillers.
            // Iron (unliftable), Ghost (pass-through), stack demo, and a face-2 gap for match-by-drop.
            TrySpawnDice(frame, 1, 1, DiceKind.Iron, DiceStackTier.Bottom, 6, default);
            TrySpawnDice(frame, 2, 1, DiceKind.Ghost, DiceStackTier.Bottom, 3, default);
            TrySpawnDice(frame, 1, 2, DiceKind.Normal, DiceStackTier.Bottom, 2, default);
            TrySpawnDice(frame, 3, 2, DiceKind.Normal, DiceStackTier.Bottom, 2, default);
            TrySpawnDice(frame, 1, 3, DiceKind.Normal, DiceStackTier.Bottom, 4, default);
            TrySpawnDice(frame, 1, 3, DiceKind.Wood, DiceStackTier.Top, 5, default);
            TrySpawnDice(frame, 3, 3, DiceKind.Magnet, DiceStackTier.Bottom, 3, default);
            TrySpawnDice(frame, 2, 4, DiceKind.Normal, DiceStackTier.Bottom, 2, default);

            var count = frame.RuntimeConfig.InitialDiceCount;
            if (count <= 0)
            {
                count = BoardDefaults.InitialDiceCount;
            }

            for (var i = 0; i < count; i++)
            {
                var x = 1 + (i % Math.Max(1, width - 1));
                var y = 1 + ((i * 2) % Math.Max(1, height - 1));
                var face = frame.RNG->Next(
                    BoardDefaults.MinFaceValue,
                    BoardDefaults.MaxFaceValue + 1);
                var kindRoll = frame.RNG->Next(0, 5);
                var kind = kindRoll switch
                {
                    0 => DiceKind.Wood,
                    1 => DiceKind.Ice,
                    _ => DiceKind.Normal,
                };

                if (!CellOccupancy.TryResolveDropTier(
                        frame,
                        frame.GetSingleton<Board>(),
                        x,
                        y,
                        out var tier))
                {
                    continue;
                }

                TrySpawnDice(frame, x, y, kind, tier, face, default);
            }
        }

        public static bool TrySpawnDice(
            Frame frame,
            int x,
            int y,
            DiceKind kind,
            DiceStackTier tier,
            int topFace,
            PlayerRef owner)
        {
            if (!frame.TryGetSingleton<Board>(out var board)
                || !IsInsideBoard(board, x, y))
            {
                return false;
            }

            if (tier == DiceStackTier.Bottom
                && !CellOccupancy.CanPlaceBottomAt(frame, board, x, y))
            {
                return false;
            }

            if (tier == DiceStackTier.Top
                && !CellOccupancy.CanPlaceTopAt(frame, board, x, y))
            {
                return false;
            }

            DiceOrientation.CreateWithTopFace(topFace, out var top, out var north, out var east);
            var dice = frame.Create();
            frame.Set(dice, new Dice
            {
                Kind = kind,
                Tier = tier,
                TopFace = top,
                NorthFace = north,
                EastFace = east,
                IsCarried = false,
                IsErasing = false,
                EraseTicksRemaining = 0,
                EraseTicksTotal = 0,
                Owner = owner,
            });
            frame.Set(dice, new GridPose { X = x, Y = y });
            SyncTransform(frame, dice, x, y, tier);
            return true;
        }

        internal static void SyncTransform(
            Frame frame,
            EntityRef entity,
            int x,
            int y,
            DiceStackTier tier = DiceStackTier.Bottom)
        {
            var cellSize = frame.RuntimeConfig.CellSize;
            if (cellSize <= FP._0)
            {
                cellSize = FP._1;
            }

            // Transform2D XY maps to world XZ in the view binder; tier height is view-only.
            _ = tier;
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

        internal static bool IsInsideBoard(Board board, int x, int y)
        {
            return x >= 0 && y >= 0 && x < board.Width && y < board.Height;
        }

        internal static bool IsPawnOccupied(Frame frame, int x, int y, EntityRef ignore)
        {
            var filter = frame.Filter<PlayerPawn, GridPose>();
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

        static int ResolveWidth(Frame frame)
        {
            var width = frame.RuntimeConfig.BoardWidth;
            return width > 0 ? width : BoardDefaults.BoardWidth;
        }

        static int ResolveHeight(Frame frame)
        {
            var height = frame.RuntimeConfig.BoardHeight;
            return height > 0 ? height : BoardDefaults.BoardHeight;
        }
    }
}
