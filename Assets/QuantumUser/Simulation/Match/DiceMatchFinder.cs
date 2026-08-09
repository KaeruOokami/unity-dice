namespace Quantum
{
    using DiceGame.Core;

    /// <summary>
    /// Frame adapter over production <see cref="MatchClusterFinder"/> (Jumbo bridged + sinking weights).
    /// </summary>
    public static unsafe class DiceMatchFinder
    {
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
                || action.IsErasing
                || action.IsSpawning)
            {
                return false;
            }

            face = action.TopFace;
            if (face < 2 || face > 6)
            {
                return false;
            }

            var snapshots = new MatchDiceSnapshot[MatchClusterFinder.MaxDice];
            var entities = new EntityRef[MatchClusterFinder.MaxDice];
            var diceCount = CollectSnapshots(frame, snapshots, entities);
            if (diceCount <= 0)
            {
                return false;
            }

            var actionId = -1;
            for (var i = 0; i < diceCount; i++)
            {
                if (entities[i] == actionDice)
                {
                    actionId = snapshots[i].Id;
                    break;
                }
            }

            if (actionId < 0)
            {
                return false;
            }

            var memberIds = new int[MatchClusterFinder.MaxCluster];
            var memberCount = MatchClusterFinder.TryFindClusterTouching(
                snapshots,
                diceCount,
                actionId,
                face,
                memberIds);
            if (memberCount <= 0)
            {
                return false;
            }

            clusterSize = memberCount;
            for (var i = 0; i < memberCount; i++)
            {
                var id = memberIds[i];
                for (var d = 0; d < diceCount; d++)
                {
                    if (snapshots[d].Id == id)
                    {
                        BeginErase(frame, entities[d], actingPlayer);
                        break;
                    }
                }
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

        static int CollectSnapshots(Frame frame, MatchDiceSnapshot[] snapshots, EntityRef[] entities)
        {
            var count = 0;
            var filter = frame.Filter<Dice, GridPose>();
            while (filter.Next(out var entity, out var dice, out var pose) && count < MatchClusterFinder.MaxDice)
            {
                var caps = CoreDiceBridge.GetCapabilities(dice.Kind);
                var isSink = dice.IsErasing && dice.Tier == DiceStackTier.Bottom;
                // Production: spawning/rolling/carried excluded; erasing non-jumbo excluded from new clusters
                // via Eligible + weight. Keep erasing jumbo eligible for sinking pass.
                var eligible = !dice.IsCarried
                    && !dice.IsSpawning
                    && !dice.IsMotionBusy
                    && (!dice.IsErasing || (caps.HasExpandedFootprint && isSink));

                snapshots[count] = new MatchDiceSnapshot
                {
                    Id = count,
                    CellX = pose.X,
                    CellY = pose.Y,
                    Tier = dice.Tier == DiceStackTier.Top ? 1 : 0,
                    TopFace = dice.TopFace,
                    HasExpandedFootprint = caps.HasExpandedFootprint,
                    IsSinkErasing = isSink,
                    KeepsJumboTopOccupancy = caps.HasExpandedFootprint && isSink && ResolveKeepsJumboTop(dice),
                    ParticipatesInBothTiersWhileSinking = caps.ParticipatesInBothTiersWhileSinking,
                    SinkingMatchWeightPerTier = caps.SinkingMatchWeightPerTier,
                    Eligible = eligible,
                };
                entities[count] = entity;
                count++;
            }

            return count;
        }

        static bool ResolveKeepsJumboTop(in Dice dice)
        {
            if (!dice.IsErasing || dice.EraseTicksTotal <= 0)
            {
                return true;
            }

            // Production releases Top occupancy after SinkTopOccupancyThreshold of sink progress.
            var progress = 1f - (dice.EraseTicksRemaining / (float)dice.EraseTicksTotal);
            return progress < JumboFootprintCells.SinkTopOccupancyThreshold;
        }

        static void BeginErase(Frame frame, EntityRef entity, PlayerRef actingPlayer)
        {
            if (!frame.Unsafe.TryGetPointer<Dice>(entity, out var dice) || dice->IsErasing)
            {
                return;
            }

            var caps = CoreDiceBridge.GetCapabilities(dice->Kind);
            var ticks = dice->Tier == DiceStackTier.Top
                ? ResolveRadianceTicks(frame)
                : ResolveSinkTicks(frame);

            if (caps.SinkDurationMultiplier > 1f)
            {
                ticks = (int)(ticks * caps.SinkDurationMultiplier);
            }

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
    }
}
