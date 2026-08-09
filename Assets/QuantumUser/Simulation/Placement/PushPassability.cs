namespace Quantum
{
    using DiceGame.SimShared.Dice;
    using DiceGame.SimShared.Magnet;
    using DiceGame.SimShared.Push;
    using DiceGame.SimShared.Slide;

    /// <summary>
    /// Frame adapter: EffectiveBehavior + one-cell / Ice until-blocked push + Magnet chain.
    /// </summary>
    public static unsafe class PushPassability
    {
        const int MaxChain = MagnetChainCollector.MaxChain;
        const int MaxElasticDepth = 8;

        public static bool TryPushOneCell(
            Frame frame,
            Board board,
            in PlayerPawn pawn,
            in GridPose pose,
            int dx,
            int dy,
            out EntityRef pushed,
            out int fromX,
            out int fromY)
        {
            pushed = EntityRef.None;
            fromX = 0;
            fromY = 0;

            var tx = pose.X + dx;
            var ty = pose.Y + dy;
            if (!BoardBootstrapSystem.IsInsideBoard(board, tx, ty))
            {
                return false;
            }

            if (!TryResolvePushTarget(frame, in pawn, tx, ty, out var target, out var dice))
            {
                return false;
            }

            var standing = CellOccupancy.TryGetStandingDice(frame, pose.X, pose.Y, pawn.IsOnFloor);
            var playerTier = pawn.IsOnFloor ? 0 : (pawn.StandingTier == DiceStackTier.Top ? 1 : 0);
            var diceTier = dice.Tier == DiceStackTier.Top ? 1 : 0;
            var effective = EffectiveDiceQuery.ResolveAt(frame, target, in dice, tx, ty);
            var caps = effective.Capabilities;

            if (!effective.IsPlayerMovable)
            {
                return false;
            }

            if (!PushEligibility.CanPush(
                    pawn.IsOnFloor,
                    pose.X,
                    pose.Y,
                    playerTier,
                    tx,
                    ty,
                    diceTier,
                    caps.CanBePushedByPlayer,
                    dice.IsCarried,
                    dice.IsErasing,
                    dice.IsMotionBusy || dice.IsSpawning,
                    standing.IsValid && standing == target))
            {
                return false;
            }

            if (caps.SlideUntilBlocked)
            {
                return TryPushIceSlide(
                    frame,
                    board,
                    in pawn,
                    target,
                    tx,
                    ty,
                    diceTier,
                    dx,
                    dy,
                    0,
                    out pushed,
                    out fromX,
                    out fromY);
            }

            if (!TryPlanAndApplyOneCell(
                    frame,
                    board,
                    target,
                    tx,
                    ty,
                    diceTier,
                    dx,
                    dy,
                    caps.PushUsesRoll,
                    caps.HasMagnetCoupling,
                    pawn.Player,
                    out fromX,
                    out fromY))
            {
                return false;
            }

            pushed = target;
            return true;
        }

        static bool TryPushIceSlide(
            Frame frame,
            Board board,
            in PlayerPawn pawn,
            EntityRef target,
            int fromX0,
            int fromY0,
            int diceTier,
            int dx,
            int dy,
            int elasticDepth,
            out EntityRef pushed,
            out int fromX,
            out int fromY)
        {
            pushed = EntityRef.None;
            fromX = fromX0;
            fromY = fromY0;

            if (!frame.TryGet<Dice>(target, out var moverDice)
                || !IceSlideFrameQueries.TryPlan(
                    frame,
                    board,
                    fromX0,
                    fromY0,
                    diceTier,
                    dx,
                    dy,
                    moverDice.Kind,
                    out var plan))
            {
                if (elasticDepth >= MaxElasticDepth)
                {
                    return false;
                }

                return TryElasticTransfer(
                    frame,
                    board,
                    in pawn,
                    fromX0,
                    fromY0,
                    diceTier,
                    dx,
                    dy,
                    elasticDepth,
                    out pushed,
                    out fromX,
                    out fromY);
            }

            var slideTicks = frame.RuntimeConfig.SlideDurationTicks > 0
                ? frame.RuntimeConfig.SlideDurationTicks
                : MatchSimDefaults.SlideDurationTicks;
            if (!ApplyDiceMove(
                    frame,
                    target,
                    fromX0,
                    fromY0,
                    plan.DestX,
                    plan.DestY,
                    plan.LandingTier,
                    plan.DemoteUnsupportedTopAtFrom,
                    pushUsesRoll: false,
                    dx,
                    dy,
                    IceSlideUntilBlockedPlanner.ResolveMotionTicks(slideTicks, plan.StepCount),
                    pawn.Player))
            {
                return false;
            }

            IceSlideFrameQueries.ApplyGhostSwap(frame, in plan);

            if (elasticDepth < MaxElasticDepth)
            {
                TryStartElasticAt(
                    frame,
                    board,
                    plan.DestX,
                    plan.DestY,
                    plan.LandingTier,
                    dx,
                    dy,
                    pawn.Player,
                    elasticDepth + 1);
            }

            pushed = target;
            return true;
        }

        static bool TryElasticTransfer(
            Frame frame,
            Board board,
            in PlayerPawn pawn,
            int fromX0,
            int fromY0,
            int diceTier,
            int dx,
            int dy,
            int elasticDepth,
            out EntityRef pushed,
            out int fromX,
            out int fromY)
        {
            pushed = EntityRef.None;
            fromX = fromX0;
            fromY = fromY0;
            var nx = fromX0 + dx;
            var ny = fromY0 + dy;
            if (!TryGetElasticTarget(frame, nx, ny, diceTier, out var elastic, out var elasticTier))
            {
                return false;
            }

            return TryPushIceSlide(
                frame,
                board,
                in pawn,
                elastic,
                nx,
                ny,
                elasticTier,
                dx,
                dy,
                elasticDepth + 1,
                out pushed,
                out fromX,
                out fromY);
        }

        static void TryStartElasticAt(
            Frame frame,
            Board board,
            int stoppedX,
            int stoppedY,
            int stoppedTier,
            int dx,
            int dy,
            PlayerRef player,
            int elasticDepth)
        {
            var nx = stoppedX + dx;
            var ny = stoppedY + dy;
            if (!TryGetElasticTarget(frame, nx, ny, stoppedTier, out var elastic, out var elasticTier))
            {
                return;
            }

            if (!frame.TryGet<Dice>(elastic, out var elasticDice)
                || !IceSlideFrameQueries.TryPlan(
                    frame,
                    board,
                    nx,
                    ny,
                    elasticTier,
                    dx,
                    dy,
                    elasticDice.Kind,
                    out var plan))
            {
                return;
            }

            var slideTicks = frame.RuntimeConfig.SlideDurationTicks > 0
                ? frame.RuntimeConfig.SlideDurationTicks
                : MatchSimDefaults.SlideDurationTicks;
            ApplyDiceMove(
                frame,
                elastic,
                nx,
                ny,
                plan.DestX,
                plan.DestY,
                plan.LandingTier,
                plan.DemoteUnsupportedTopAtFrom,
                pushUsesRoll: false,
                dx,
                dy,
                IceSlideUntilBlockedPlanner.ResolveMotionTicks(slideTicks, plan.StepCount),
                player);
            IceSlideFrameQueries.ApplyGhostSwap(frame, in plan);

            if (elasticDepth < MaxElasticDepth)
            {
                TryStartElasticAt(
                    frame,
                    board,
                    plan.DestX,
                    plan.DestY,
                    plan.LandingTier,
                    dx,
                    dy,
                    player,
                    elasticDepth + 1);
            }
        }

        static bool TryGetElasticTarget(
            Frame frame,
            int x,
            int y,
            int preferredTier,
            out EntityRef entity,
            out int tierNorm)
        {
            entity = EntityRef.None;
            tierNorm = preferredTier;
            var tier = preferredTier == 1 ? DiceStackTier.Top : DiceStackTier.Bottom;
            if (!CellOccupancy.TryGetAt(frame, x, y, tier, out entity, out var dice)
                || dice.IsCarried
                || dice.IsErasing
                || dice.IsMotionBusy
                || dice.IsSpawning)
            {
                return false;
            }

            if (!CoreDiceBridge.GetCapabilities(dice.Kind).TransfersSlideOnCollision)
            {
                return false;
            }

            tierNorm = dice.Tier == DiceStackTier.Top ? 1 : 0;
            return true;
        }

        static bool TryPlanAndApplyOneCell(
            Frame frame,
            Board board,
            EntityRef target,
            int tx,
            int ty,
            int diceTier,
            int dx,
            int dy,
            bool pushUsesRoll,
            bool hasMagnetCoupling,
            PlayerRef player,
            out int fromX,
            out int fromY)
        {
            fromX = tx;
            fromY = ty;

            var chain = stackalloc EntityRef[MaxChain];
            var chainCount = 1;
            chain[0] = target;
            if (hasMagnetCoupling)
            {
                chainCount = CollectMagnetChain(frame, target, tx, ty, diceTier, dx, dy, chain);
                if (!CanAllChainMembersTakeOneCell(frame, board, chain, chainCount, dx, dy))
                {
                    return false;
                }
            }

            // Apply arms first (production builds then executes; arms must leave before origin if overlapping dest).
            for (var i = chainCount - 1; i >= 0; i--)
            {
                var entity = chain[i];
                if (!frame.TryGet<GridPose>(entity, out var pose)
                    || !frame.TryGet<Dice>(entity, out var dice))
                {
                    return false;
                }

                var memberTier = dice.Tier == DiceStackTier.Top ? 1 : 0;
                var memberRoll = CoreDiceBridge.GetCapabilities(dice.Kind).PushUsesRoll;
                if (!OneCellPushPlanner.TryPlan(
                        pose.X,
                        pose.Y,
                        memberTier,
                        dx,
                        dy,
                        board.Width,
                        board.Height,
                        (x, y) => CellOccupancy.CanPlaceBottomAt(frame, board, x, y),
                        (x, y) => CellOccupancy.CanPlaceTopAt(frame, board, x, y),
                        (x, y) => CellOccupancy.HasSolidBottomAt(frame, x, y),
                        out var destX,
                        out var destY,
                        out var landingTier,
                        out var demoteTop))
                {
                    return false;
                }

                if (!ApplyDiceMove(
                        frame,
                        entity,
                        pose.X,
                        pose.Y,
                        destX,
                        destY,
                        landingTier,
                        demoteTop,
                        i == 0 ? pushUsesRoll : memberRoll,
                        dx,
                        dy,
                        ResolvePushMotionTicks(frame),
                        player))
                {
                    return false;
                }
            }

            return true;
        }

        static int CollectMagnetChain(
            Frame frame,
            EntityRef origin,
            int originX,
            int originY,
            int tier,
            int dx,
            int dy,
            EntityRef* chain)
        {
            chain[0] = origin;
            var count = 1;
            MagnetChainCollector.GetPerpendicular(dx, dy, out var ax0, out var ay0, out var ax1, out var ay1);
            count = CollectArm(frame, originX, originY, tier, ax0, ay0, chain, count);
            count = CollectArm(frame, originX, originY, tier, ax1, ay1, chain, count);
            return count;
        }

        static int CollectArm(
            Frame frame,
            int originX,
            int originY,
            int tierNorm,
            int armX,
            int armY,
            EntityRef* chain,
            int count)
        {
            var x = originX + armX;
            var y = originY + armY;
            var tier = tierNorm == 1 ? DiceStackTier.Top : DiceStackTier.Bottom;
            while (count < MaxChain
                   && CellOccupancy.TryGetAt(frame, x, y, tier, out var entity, out var dice)
                   && !dice.IsCarried
                   && !dice.IsErasing
                   && CoreDiceBridge.GetCapabilities(dice.Kind).HasMagnetCoupling)
            {
                chain[count++] = entity;
                x += armX;
                y += armY;
            }

            return count;
        }

        static bool CanAllChainMembersTakeOneCell(
            Frame frame,
            Board board,
            EntityRef* chain,
            int chainCount,
            int dx,
            int dy)
        {
            for (var i = 0; i < chainCount; i++)
            {
                if (!frame.TryGet<GridPose>(chain[i], out var pose)
                    || !frame.TryGet<Dice>(chain[i], out var dice)
                    || dice.IsMotionBusy
                    || dice.IsSpawning
                    || dice.IsErasing
                    || dice.IsCarried)
                {
                    return false;
                }

                var memberTier = dice.Tier == DiceStackTier.Top ? 1 : 0;
                if (!OneCellPushPlanner.TryPlan(
                        pose.X,
                        pose.Y,
                        memberTier,
                        dx,
                        dy,
                        board.Width,
                        board.Height,
                        (x, y) => CellOccupancy.CanPlaceBottomAt(frame, board, x, y),
                        (x, y) => CellOccupancy.CanPlaceTopAt(frame, board, x, y),
                        (x, y) => CellOccupancy.HasSolidBottomAt(frame, x, y),
                        out _,
                        out _,
                        out _,
                        out _))
                {
                    return false;
                }
            }

            return true;
        }

        static bool ApplyDiceMove(
            Frame frame,
            EntityRef target,
            int fromX,
            int fromY,
            int destX,
            int destY,
            int landingTierNorm,
            bool demoteTopAtFrom,
            bool pushUsesRoll,
            int dx,
            int dy,
            int motionTicks,
            PlayerRef player)
        {
            if (!frame.Unsafe.TryGetPointer<Dice>(target, out var dicePtr)
                || !frame.Unsafe.TryGetPointer<GridPose>(target, out var dicePose))
            {
                return false;
            }

            EntityRef topEntity = EntityRef.None;
            var hadTop = demoteTopAtFrom
                && CellOccupancy.TryGetTopAt(frame, fromX, fromY, out topEntity, out _);

            dicePose->X = destX;
            dicePose->Y = destY;
            dicePtr->Tier = landingTierNorm == 1 ? DiceStackTier.Top : DiceStackTier.Bottom;

            if (pushUsesRoll
                && DiceFaceOrientation.TryRoll(
                    dx,
                    dy,
                    dicePtr->TopFace,
                    dicePtr->NorthFace,
                    dicePtr->EastFace,
                    out var nextTop,
                    out var nextNorth,
                    out var nextEast))
            {
                dicePtr->TopFace = nextTop;
                dicePtr->NorthFace = nextNorth;
                dicePtr->EastFace = nextEast;
            }

            dicePtr->IsMotionBusy = true;
            dicePtr->MotionTicksRemaining = motionTicks > 0 ? motionTicks : MatchSimDefaults.PushMotionTicks;
            BoardBootstrapSystem.SyncTransform(frame, target, destX, destY, dicePtr->Tier);

            if (hadTop
                && topEntity.IsValid
                && frame.Unsafe.TryGetPointer<Dice>(topEntity, out var topDice)
                && !topDice->IsErasing)
            {
                topDice->Tier = DiceStackTier.Bottom;
                BoardBootstrapSystem.SyncTransform(frame, topEntity, fromX, fromY, DiceStackTier.Bottom);
            }

            MatchSettleRequest.Request(frame, target, player);
            return true;
        }

        static bool TryResolvePushTarget(
            Frame frame,
            in PlayerPawn pawn,
            int tx,
            int ty,
            out EntityRef target,
            out Dice dice)
        {
            if (pawn.IsOnFloor)
            {
                return CellOccupancy.TryGetBottomAt(frame, tx, ty, out target, out dice);
            }

            return CellOccupancy.TryGetTopAt(frame, tx, ty, out target, out dice);
        }

        static int ResolvePushMotionTicks(Frame frame)
        {
            var ticks = frame.RuntimeConfig.PushMotionTicks;
            return ticks > 0 ? ticks : MatchSimDefaults.PushMotionTicks;
        }
    }
}
