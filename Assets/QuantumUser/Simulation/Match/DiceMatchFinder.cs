namespace Quantum
{
    /// <summary>
    /// Lite match: same-tier orthogonal flood-fill where cluster weight &gt;= TopFace (faces 2窶・).
    /// Begins timed erasure (LogicalMotions finishes destroy). Jumbo bridges deferred.
    /// </summary>
    public static unsafe class DiceMatchFinder
    {
        const int MaxCluster = 64;
        static readonly int[] NeighborX = { 0, 1, 0, -1 };
        static readonly int[] NeighborY = { 1, 0, -1, 0 };

        public static bool TryBeginEraseClustersTouching(
            Frame frame,
            EntityRef actionDice,
            PlayerRef actingPlayer,
            out int face,
            out int clusterSize)
        {
            face = 0;
            clusterSize = 0;

            if (!actionDice.IsValid
                || !frame.TryGet<Dice>(actionDice, out var action)
                || action.IsCarried
                || action.IsErasing)
            {
                return false;
            }

            face = action.TopFace;
            if (face < 2 || face > 6)
            {
                return false;
            }

            if (!frame.TryGet<GridPose>(actionDice, out var actionPose))
            {
                return false;
            }

            var members = stackalloc EntityRef[MaxCluster];
            var count = FloodFillSameTierFace(
                frame,
                actionPose.X,
                actionPose.Y,
                action.Tier,
                face,
                members,
                MaxCluster);

            if (count < face)
            {
                return false;
            }

            var hasAction = false;
            for (var i = 0; i < count; i++)
            {
                if (members[i] == actionDice)
                {
                    hasAction = true;
                    break;
                }
            }

            if (!hasAction)
            {
                return false;
            }

            clusterSize = count;
            for (var i = 0; i < count; i++)
            {
                BeginErase(frame, members[i], actingPlayer);
            }

            return true;
        }

        public static void FinishEraseAndDemote(Frame frame, EntityRef entity)
        {
            if (!entity.IsValid || !frame.Exists(entity))
            {
                return;
            }

            if (!frame.TryGet<Dice>(entity, out var dice)
                || !frame.TryGet<GridPose>(entity, out var pose))
            {
                frame.Destroy(entity);
                return;
            }

            var x = pose.X;
            var y = pose.Y;
            var wasBottom = dice.Tier == DiceStackTier.Bottom;
            frame.Destroy(entity);

            if (wasBottom
                && CellOccupancy.TryGetTopAt(frame, x, y, out var topEntity, out _))
            {
                if (frame.Unsafe.TryGetPointer<Dice>(topEntity, out var topDice)
                    && !topDice->IsErasing)
                {
                    topDice->Tier = DiceStackTier.Bottom;
                    BoardBootstrapSystem.SyncTransform(frame, topEntity, x, y, DiceStackTier.Bottom);
                }
            }
        }

        static void BeginErase(Frame frame, EntityRef entity, PlayerRef actingPlayer)
        {
            if (!frame.Unsafe.TryGetPointer<Dice>(entity, out var dice) || dice->IsErasing)
            {
                return;
            }

            var ticks = dice->Tier == DiceStackTier.Top
                ? ResolveRadianceTicks(frame)
                : ResolveSinkTicks(frame);

            dice->IsErasing = true;
            dice->EraseTicksRemaining = ticks;
            dice->EraseTicksTotal = ticks;
            if (actingPlayer != PlayerRef.None)
            {
                dice->Owner = actingPlayer;
            }
        }

        static int ResolveSinkTicks(Frame frame)
        {
            var ticks = frame.RuntimeConfig.SinkEraseTicks;
            return ticks > 0 ? ticks : MatchSimDefaults.SinkEraseTicks;
        }

        static int ResolveRadianceTicks(Frame frame)
        {
            var ticks = frame.RuntimeConfig.RadianceEraseTicks;
            return ticks > 0 ? ticks : MatchSimDefaults.RadianceEraseTicks;
        }

        static int FloodFillSameTierFace(
            Frame frame,
            int startX,
            int startY,
            DiceStackTier tier,
            int face,
            EntityRef* members,
            int capacity)
        {
            var boardW = BoardDefaults.BoardWidth;
            var boardH = BoardDefaults.BoardHeight;
            if (frame.TryGetSingleton<Board>(out var board) && board.Initialized)
            {
                boardW = board.Width;
                boardH = board.Height;
            }

            var visitCount = boardW * boardH;
            if (visitCount <= 0)
            {
                return 0;
            }

            var visitFlags = stackalloc byte[visitCount];
            for (var i = 0; i < visitCount; i++)
            {
                visitFlags[i] = 0;
            }

            var queueX = stackalloc int[MaxCluster];
            var queueY = stackalloc int[MaxCluster];
            var head = 0;
            var tail = 0;
            var count = 0;
            queueX[tail] = startX;
            queueY[tail] = startY;
            tail++;

            while (head < tail && count < capacity)
            {
                var x = queueX[head];
                var y = queueY[head];
                head++;

                if (x < 0 || y < 0 || x >= boardW || y >= boardH)
                {
                    continue;
                }

                var flagIndex = y * boardW + x;
                if (visitFlags[flagIndex] != 0)
                {
                    continue;
                }

                visitFlags[flagIndex] = 1;

                if (!CellOccupancy.TryGetAt(frame, x, y, tier, out var entity, out var dice))
                {
                    continue;
                }

                if (dice.TopFace != face || dice.IsCarried || dice.IsErasing)
                {
                    continue;
                }

                // Jumbo footprint matching deferred.
                if (DiceKindCapabilities.For(dice.Kind).HasExpandedFootprint)
                {
                    continue;
                }

                members[count++] = entity;

                for (var i = 0; i < 4; i++)
                {
                    if (tail >= MaxCluster)
                    {
                        break;
                    }

                    queueX[tail] = x + NeighborX[i];
                    queueY[tail] = y + NeighborY[i];
                    tail++;
                }
            }

            return count;
        }
    }
}
