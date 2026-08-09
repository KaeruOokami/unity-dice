namespace Quantum
{
    using DiceGame.Core;
    using DiceGame.SimShared.Jump;
    using DiceGame.SimShared.Move;
    using DiceGame.SimShared.Placement;
    using SimMoveAction = DiceGame.SimShared.Move.MoveAction;

    /// <summary>
    /// Frame adapter: builds Domain <see cref="MoveFacts"/> and selects <see cref="MoveActionSelector"/>.
    /// </summary>
    public static unsafe class FrameMoveActionResolver
    {
        public static SimMoveAction Resolve(
            Frame frame,
            Board board,
            in PlayerPawn pawn,
            in GridPose pose,
            EntityRef pawnEntity,
            int nextX,
            int nextY,
            int dirX,
            int dirY,
            bool isJumping)
        {
            if (!BoardBootstrapSystem.IsInsideBoard(board, nextX, nextY))
            {
                return SimMoveAction.Blocked;
            }

            if (BoardBootstrapSystem.IsPawnOccupied(frame, nextX, nextY, pawnEntity))
            {
                return SimMoveAction.Blocked;
            }

            var fromLevel = SurfaceHeightNorms.FromStanding(pawn.IsOnFloor, pawn.StandingTier == DiceStackTier.Top);
            if (!PawnTransferPolicy.TryResolveLanding(
                    frame,
                    board,
                    nextX,
                    nextY,
                    out var landingOnFloor,
                    out _,
                    out var targetLevel))
            {
                return SimMoveAction.Blocked;
            }

            var hasStanding = false;
            var canJumpCouple = false;
            var isSink = false;
            var isPlayerMovable = false;
            var caps = default(DiceCapabilities);
            var canJumpGridRoll = false;
            var canGridRoll = false;
            var allowJumpGridMove = false;
            var hasIceSlide = false;

            if (!pawn.IsOnFloor
                && CellOccupancy.TryGetAt(
                    frame,
                    pose.X,
                    pose.Y,
                    pawn.StandingTier,
                    out var standingEntity,
                    out var standingDice))
            {
                hasStanding = true;
                var effective = EffectiveDiceQuery.ResolveAt(
                    frame,
                    standingEntity,
                    in standingDice,
                    pose.X,
                    pose.Y);
                canJumpCouple = effective.CanJumpCoupleWithPlayer;
                isSink = effective.IsSinkErasing;
                isPlayerMovable = effective.IsPlayerMovable;
                caps = effective.Capabilities;
                canGridRoll = caps.CanGridRoll;
                hasIceSlide = !isJumping && caps.SlideUntilBlocked;

                if (isJumping
                    && TryResolveJumpCapability(frame, in pawn, in pose, out var capability)
                    && capability.AllowDiceGridMove)
                {
                    allowJumpGridMove = true;
                    canJumpGridRoll = JumpGridRollProbe.CanBuildAny(
                        frame,
                        board,
                        in pose,
                        in standingDice,
                        pawn.StandingTier,
                        dirX,
                        dirY,
                        capability.MaxDistance,
                        capability.AllowTierChange,
                        effective);
                }
            }

            var mode = JumpPlayerTransferRules.ResolveStandingMoveMode(
                isJumping,
                hasStanding,
                isPlayerMovable,
                canJumpCouple,
                isSink,
                caps);

            var withinFull = PawnTransferPolicy.CanTransferToCell(
                frame,
                board,
                in pawn,
                in pose,
                pawnEntity,
                nextX,
                nextY,
                isJumping);
            var withinDescent = withinFull
                || (isJumping
                    && targetLevel < fromLevel
                    && JumpPlayerTransferRules.CanUsePlayerOnlyLowerLevelJump(
                        isJumping,
                        isSink,
                        canJumpCouple,
                        isPlayerMovable));

            var canPlaceBottom = CellOccupancy.CanPlaceBottomAt(frame, board, nextX, nextY);
            var floorPassable = landingOnFloor;
            var hasFloorMount = fromLevel == SurfaceHeightNorms.Floor
                && !floorPassable
                && CellOccupancy.HasSolidBottomAt(frame, nextX, nextY);
            var canTierLand = fromLevel == SurfaceHeightNorms.Bottom
                && targetLevel == SurfaceHeightNorms.Top
                && CellOccupancy.CanPlaceTopAt(frame, board, nextX, nextY);

            var facts = new MoveFacts(
                fromLevel,
                targetLevel,
                isJumping,
                mode,
                hasStanding,
                canJumpCouple,
                isSink,
                isPlayerMovable,
                withinFull,
                withinDescent,
                hasExpandedFootprintWalk: false,
                blocksDiceCoupledStackEntry: false,
                isPlayerFloorPassable: floorPassable && targetLevel == SurfaceHeightNorms.Floor,
                canPlaceBottomAtToCell: canPlaceBottom,
                hasFloorMountBottom: hasFloorMount,
                hasIceSlideDisplacement: hasIceSlide,
                canJumpGridRoll: canJumpGridRoll,
                canTopFall: false,
                canTierLand: canTierLand,
                canGridRoll: canGridRoll && !isJumping,
                allowJumpGridMove: allowJumpGridMove);

            return MoveActionSelector.Select(in facts);
        }

        static bool TryResolveJumpCapability(
            Frame frame,
            in PlayerPawn pawn,
            in GridPose pose,
            out JumpCoupledMoveCapability capability)
        {
            capability = default;
            if (!pawn.IsJumping)
            {
                return false;
            }

            var config = new JumpInputPolicy.WindowConfig
            {
                Gravity = ResolveJumpGravity(frame),
                TwoCellMaxTimeline = ResolveJumpTimelinePermille(
                    frame.RuntimeConfig.JumpGridTwoCellMaxTimelinePermille,
                    MatchSimDefaults.JumpGridTwoCellMaxTimelinePermille),
                OneCellMaxTimeline = ResolveJumpTimelinePermille(
                    frame.RuntimeConfig.JumpGridOneCellMaxTimelinePermille,
                    MatchSimDefaults.JumpGridOneCellMaxTimelinePermille),
                TierChangeMinTimeline = ResolveJumpTimelinePermille(
                    frame.RuntimeConfig.JumpGridTierChangeMinTimelinePermille,
                    MatchSimDefaults.JumpGridTierChangeMinTimelinePermille),
                TierChangeMaxTimeline = ResolveJumpTimelinePermille(
                    frame.RuntimeConfig.JumpGridTierChangeMaxTimelinePermille,
                    MatchSimDefaults.JumpGridTierChangeMaxTimelinePermille),
            };

            var motion = new VerticalMotionState
            {
                Offset = pawn.JumpOffsetY.AsFloat,
                VelocityY = pawn.JumpVelocityY.AsFloat,
                IsGrounded = false,
            };
            if (!JumpInputPolicy.TryEvaluate(
                    true,
                    pawn.JumpDiceGridMoved,
                    in config,
                    motion,
                    pawn.JumpHeight.AsFloat,
                    out capability))
            {
                return false;
            }

            var canJumpCouple = false;
            var isSinkErasing = false;
            var blocksCross = false;
            var blocksUpTier = false;
            if (!pawn.IsOnFloor
                && CellOccupancy.TryGetAt(
                    frame,
                    pose.X,
                    pose.Y,
                    pawn.StandingTier,
                    out var diceEntity,
                    out var dice))
            {
                var effective = EffectiveDiceQuery.ResolveAt(frame, diceEntity, in dice, pose.X, pose.Y);
                canJumpCouple = effective.CanJumpCoupleWithPlayer;
                isSinkErasing = effective.IsSinkErasing;
                blocksCross = effective.Capabilities.BlocksJumpCrossCellMove;
                blocksUpTier = effective.Capabilities.BlocksJumpUpwardTierChange;
            }

            capability = JumpInputPolicy.ApplyStandingDiceOverrides(
                capability,
                canJumpCouple,
                isSinkErasing,
                blocksCross,
                blocksUpTier);
            return true;
        }

        static float ResolveJumpGravity(Frame frame)
        {
            var milli = frame.RuntimeConfig.JumpGravityMilli;
            if (milli <= 0)
            {
                milli = MatchSimDefaults.JumpGravityMilli;
            }

            return milli / 1000f;
        }

        static float ResolveJumpTimelinePermille(int configured, int fallback)
        {
            var v = configured > 0 ? configured : fallback;
            return v / 1000f;
        }
    }

    /// <summary>
    /// Probe-only: can Domain JumpGridRollPolicy build any plan for facing dir.
    /// </summary>
    public static unsafe class JumpGridRollProbe
    {
        public static bool CanBuildAny(
            Frame frame,
            Board board,
            in GridPose pose,
            in Dice dice,
            DiceStackTier standingTier,
            int dirX,
            int dirY,
            int maxDistance,
            bool allowTierChange,
            EffectiveDiceBehavior effective)
        {
            if (!effective.IsPlayerMovable || !effective.Capabilities.CanGridRoll)
            {
                return false;
            }

            Direction direction;
            if (dirX == 1 && dirY == 0)
            {
                direction = Direction.East;
            }
            else if (dirX == -1 && dirY == 0)
            {
                direction = Direction.West;
            }
            else if (dirX == 0 && dirY == 1)
            {
                direction = Direction.North;
            }
            else if (dirX == 0 && dirY == -1)
            {
                direction = Direction.South;
            }
            else
            {
                return false;
            }

            var fromState = new DiceGame.SimShared.Motion.DiceState(
                pose.X,
                pose.Y,
                CoreDiceBridge.ToCoreOrientation(dice.TopFace, dice.NorthFace, dice.EastFace),
                CoreDiceBridge.ToCoreTier(standingTier),
                CoreDiceBridge.ToCoreKind(dice.Kind));
            var context = DiceGame.SimShared.GridMove.PassabilityContext.Jump(
                allowJumpGridMove: true,
                allowJumpTierChange: allowTierChange,
                footingWorldY: 0f);
            var occupancy = new FrameGridRollOccupancy(frame, board);
            var kindMax = effective.Capabilities.GetEffectiveMaxJumpGridMoveDistance();
            return DiceGame.SimShared.GridMove.JumpGridRollPolicy.TryBuildBestPlan(
                occupancy,
                fromState,
                direction,
                maxDistance,
                kindMax,
                allowsRoll: true,
                context,
                out _,
                out _);
        }
    }
}
