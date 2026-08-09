namespace Quantum
{
    using Photon.Deterministic;

    /// <summary>
    /// Board bootstrap: board singleton, production-equivalent initial dice, pawns on standing dice.
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

            if (!TryClaimStandingDice(frame, out var gridX, out var gridY, out var standingTier))
            {
                Log.Error(
                    $"BoardBootstrapSystem: No standing dice available for player {player}. " +
                    "Initial spawn must place at least one dice per player.");
                return;
            }

            var entity = frame.Create();
            var cellSize = frame.RuntimeConfig.CellSize;
            if (cellSize <= FP._0)
            {
                cellSize = FP._1;
            }

            var worldX = cellSize * gridX;
            var worldZ = cellSize * gridY;
            frame.Set(entity, new PlayerPawn
            {
                Player = player,
                CarriedDice = EntityRef.None,
                HasCarriedDice = false,
                IsOnFloor = false,
                StandingTier = standingTier,
                FacingX = 0,
                FacingY = 1,
                HasWorldPose = true,
                WorldX = worldX,
                WorldZ = worldZ,
                MoveSpeed = FP._0,
            });
            frame.Set(entity, new GridPose
            {
                X = gridX,
                Y = gridY,
            });
            frame.Set(entity, Transform2D.Create(new FPVector2(worldX, worldZ)));
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
            board->PartitionX = frame.RuntimeConfig.BoardPartitionX > 0
                ? frame.RuntimeConfig.BoardPartitionX
                : 0;
            board->Initialized = true;

            frame.GetOrAddSingleton<MatchPending>();
            frame.GetOrAddSingleton<SpawnState>();
            frame.GetOrAddSingleton<VersusAttackState>();
            SpawnInitialPlayerDice(frame, *board);
        }

        /// <summary>
        /// Port of <c>DiceSpawnSystem.SpawnInitialPlayerDice</c> (non-versus random path):
        /// Bottom-only slots, then spawn until InitialDiceCount (at least RequiredPlayerCount).
        /// Continuous spawn keeps weighted Bottom/Top separately.
        /// Standing dice for players are the first successfully spawned dice in creation order.
        /// </summary>
        static void SpawnInitialPlayerDice(Frame frame, Board board)
        {
            var minimumStanding = frame.RuntimeConfig.RequiredPlayerCount;
            if (minimumStanding <= 0)
            {
                minimumStanding = 1;
            }

            var initialCount = DiceSpawnRolls.ResolveInitialDiceCount(frame, minimumStanding);
            // Initial dice are Bottom only (1000‰); Top remains continuous-spawn territory.
            const int bottomOnlyWeightPermille = 1000;
            var spawned = 0;

            for (var i = 0; i < initialCount; i++)
            {
                if (!DiceSpawnCellPicker.TryPickRandomSpawnSlot(
                        frame,
                        board,
                        bottomOnlyWeightPermille,
                        out var x,
                        out var y,
                        out var tier))
                {
                    break;
                }

                if (tier != DiceStackTier.Bottom)
                {
                    // No Bottom slots left — stop rather than stacking Top during initial fill.
                    break;
                }

                var face = DiceSpawnRolls.RollTopFace(frame);
                var kind = DiceSpawnRolls.RollKind(frame);
                if (!TrySpawnDice(frame, x, y, kind, DiceStackTier.Bottom, face, default))
                {
                    continue;
                }

                spawned++;
            }

            if (spawned < minimumStanding)
            {
                Log.Error(
                    $"BoardBootstrapSystem: Failed to spawn {minimumStanding} standing dice. Spawned {spawned}.");
            }
        }

        /// <summary>
        /// Claims the next initial standing die: lowest entity index among dice whose cell has no pawn.
        /// Matches production order (first spawned dice → P1, second → P2).
        /// </summary>
        static bool TryClaimStandingDice(
            Frame frame,
            out int gridX,
            out int gridY,
            out DiceStackTier standingTier)
        {
            gridX = 0;
            gridY = 0;
            standingTier = DiceStackTier.Bottom;

            EntityRef best = EntityRef.None;
            var bestIndex = int.MaxValue;
            var filter = frame.Filter<Dice, GridPose>();
            while (filter.Next(out var entity, out var dice, out var pose))
            {
                if (dice.IsCarried || dice.IsErasing)
                {
                    continue;
                }

                if (CellOccupancy.IsPlayerPassThrough(frame, in dice))
                {
                    continue;
                }

                if (IsPawnOccupied(frame, pose.X, pose.Y, EntityRef.None))
                {
                    continue;
                }

                if (entity.Index >= bestIndex)
                {
                    continue;
                }

                best = entity;
                bestIndex = entity.Index;
                gridX = pose.X;
                gridY = pose.Y;
                standingTier = dice.Tier;
            }

            return best.IsValid;
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
            var spawnMotionTicks = ResolveSpawnMotionTicks(frame);
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
                IsSpawning = true,
                EraseTicksRemaining = 0,
                EraseTicksTotal = 0,
                Owner = owner,
                IsMotionBusy = true,
                MotionTicksRemaining = spawnMotionTicks,
                HasPendingMatch = false,
                PendingMatchPlayer = default,
            });
            frame.Set(dice, new GridPose { X = x, Y = y });
            SyncTransform(frame, dice, x, y, tier);
            return true;
        }

        static int ResolveSpawnMotionTicks(Frame frame)
        {
            var ticks = frame.RuntimeConfig.SpawnMotionTicks;
            return ticks > 0 ? ticks : MatchSimDefaults.SpawnMotionTicks;
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
