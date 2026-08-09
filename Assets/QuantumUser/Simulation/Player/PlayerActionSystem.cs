namespace Quantum
{
    using DiceGame.Core;
    using DiceGame.SimShared.Jump;
    using DiceGame.SimShared.Lift;
    using DiceGame.SimShared.Move;
    using DiceGame.SimShared.Push;
    using Photon.Deterministic;

    /// <summary>
    /// Jump / Lift: production <see cref="GravityMotion"/>, <see cref="JumpInputPolicy"/>,
    /// <see cref="LiftPhaseMachine"/> (copied Domain). View uses production <c>DiceCarryMotion</c>.
    /// </summary>
    public unsafe class PlayerActionSystem : SystemMainThreadFilter<PlayerActionSystem.Filter>
    {
        public override void Update(Frame frame, ref Filter filter)
        {
            if (!frame.TryGetSingleton<Board>(out var board) || !board.Initialized)
            {
                return;
            }

            EnsureWorldPose(frame, ref filter, board);
            TickJump(frame, filter.Pawn);
            if (TickLiftBusy(filter.Pawn))
            {
                return;
            }

            // Domain couple session owns pose + free-move lock for its duration.
            if (CoupledWalkRollAdapter.Tick(frame, board, ref filter))
            {
                return;
            }

            if (TickPushFollow(frame, board, ref filter))
            {
                return;
            }

            var input = frame.GetPlayerInput(filter.Pawn->Player);
            TickContinuousMove(frame, board, ref filter, input);

            if (input->Jump.WasPressed)
            {
                EndPushFollow(filter.Pawn);
                TryBeginJump(frame, ref filter);
            }

            if (input->Lift.WasPressed)
            {
                EndPushFollow(filter.Pawn);
                TryLiftOrDrop(frame, board, ref filter);
            }
        }

        /// <summary>
        /// Copied from production <c>CharacterController.UpdateJump</c> (GravityMotion.Step + landing hold).
        /// </summary>
        static void TickJump(Frame frame, PlayerPawn* pawn)
        {
            if (!pawn->IsJumping)
            {
                return;
            }

            var diceBusy = pawn->HasCoupledWalkRoll;
            if (JumpLandingHoldRules.ShouldFreezeJumpStep(
                    pawn->JumpDiceGridMoved,
                    diceBusy,
                    pawn->IsJumpArc))
            {
                return;
            }

            var gravity = ResolveJumpGravity(frame);
            var dt = frame.DeltaTime.AsFloat;
            if (dt <= 0f)
            {
                return;
            }

            var state = new VerticalMotionState
            {
                Offset = pawn->JumpOffsetY.AsFloat,
                VelocityY = pawn->JumpVelocityY.AsFloat,
                IsGrounded = false,
            };
            state = GravityMotion.Step(state, gravity, dt);
            pawn->JumpOffsetY = FP.FromFloat_UNSAFE(state.Offset);
            pawn->JumpVelocityY = FP.FromFloat_UNSAFE(state.VelocityY);

            if (!state.IsGrounded)
            {
                return;
            }

            if (JumpLandingHoldRules.ShouldHoldEndJump(pawn->IsJumpArc, diceBusy))
            {
                return;
            }

            pawn->IsJumping = false;
            pawn->JumpDiceGridMoved = false;
            pawn->IsJumpArc = false;
            pawn->JumpOffsetY = FP._0;
            pawn->JumpVelocityY = FP._0;
            pawn->JumpHeight = FP._0;
            pawn->JumpLaunchVy = FP._0;
        }

        /// <summary>
        /// Production OnLiftComplete / OnPlaceComplete via logical busy ticks
        /// (same durations as DiceView FreeMove Lift/Place).
        /// </summary>
        static bool TickLiftBusy(PlayerPawn* pawn)
        {
            if (!LiftPhaseMachine.IsBusy(pawn->LiftPhase))
            {
                return false;
            }

            pawn->LiftBusyTicksRemaining -= 1;
            if (pawn->LiftBusyTicksRemaining > 0)
            {
                pawn->MoveSpeed = FP._0;
                return true;
            }

            pawn->LiftBusyTicksRemaining = 0;
            if (pawn->LiftPhase == LiftPhaseMachine.Lifting)
            {
                pawn->LiftPhase = LiftPhaseMachine.OnLiftLogicalComplete(pawn->LiftPhase);
                return false;
            }

            if (pawn->LiftPhase == LiftPhaseMachine.Placing)
            {
                pawn->LiftPhase = LiftPhaseMachine.OnPlaceLogicalComplete(pawn->LiftPhase);
                pawn->HasCarriedDice = false;
                pawn->CarriedDice = EntityRef.None;
            }

            return false;
        }

        static void EnsureWorldPose(Frame frame, ref Filter filter, Board board)
        {
            if (filter.Pawn->HasWorldPose)
            {
                return;
            }

            var cellSize = ResolveCellSize(frame);
            CellSurfaceMotion.CellCenter(
                filter.Pose->X,
                filter.Pose->Y,
                cellSize.AsFloat,
                out var cx,
                out var cz);
            filter.Pawn->WorldX = FP.FromFloat_UNSAFE(cx);
            filter.Pawn->WorldZ = FP.FromFloat_UNSAFE(cz);
            filter.Pawn->MoveSpeed = FP._0;
            filter.Pawn->HasWorldPose = true;
            SyncPawnTransform(frame, filter.Entity, filter.Pawn);
        }

        static bool TickPushFollow(Frame frame, Board board, ref Filter filter)
        {
            if (!filter.Pawn->HasPushFollow)
            {
                return false;
            }

            var dice = filter.Pawn->PushFollowDice;
            if (!dice.IsValid
                || !frame.Exists(dice)
                || !frame.Unsafe.TryGetPointer<Dice>(dice, out var dicePtr)
                || !frame.Unsafe.TryGetPointer<GridPose>(dice, out var dicePose))
            {
                EndPushFollow(filter.Pawn);
                return false;
            }

            filter.Pawn->PushFollowTicksRemaining -= 1;
            if (filter.Pawn->PushFollowTicksRemaining <= 0)
            {
                ApplyPushFollowPose(
                    frame,
                    board,
                    ref filter,
                    dicePose->X,
                    dicePose->Y,
                    filter.Pawn->PushFollowDirX,
                    filter.Pawn->PushFollowDirY,
                    t01: 1f);
                EndPushFollow(filter.Pawn);
                return true;
            }

            var total = filter.Pawn->PushFollowTicksTotal;
            var remaining = filter.Pawn->PushFollowTicksRemaining;
            var t01 = total > 0 ? 1f - (remaining / (float)total) : 1f;
            ApplyPushFollowPose(
                frame,
                board,
                ref filter,
                dicePose->X,
                dicePose->Y,
                filter.Pawn->PushFollowDirX,
                filter.Pawn->PushFollowDirY,
                t01);
            return true;
        }

        static void ApplyPushFollowPose(
            Frame frame,
            Board board,
            ref Filter filter,
            int diceToX,
            int diceToY,
            int dirX,
            int dirY,
            float t01)
        {
            var cellSize = ResolveCellSize(frame).AsFloat;
            CellSurfaceMotion.CellCenter(
                filter.Pawn->PushFollowFromX,
                filter.Pawn->PushFollowFromY,
                cellSize,
                out var fromX,
                out var fromZ);
            CellSurfaceMotion.CellCenter(diceToX, diceToY, cellSize, out var toX, out var toZ);
            PushFollowMotion.Lerp(fromX, fromZ, toX, toZ, t01, out var diceX, out var diceZ);

            var half = cellSize * 0.5f;
            var radius = ResolvePushContactRadius(frame).AsFloat;
            PushFollowMotion.ContactWorldXZ(
                diceX,
                diceZ,
                dirX,
                dirY,
                half,
                radius,
                out var followerX,
                out var followerZ);
            var standX = diceToX - dirX;
            var standY = diceToY - dirY;

            if (BoardBootstrapSystem.IsInsideBoard(board, standX, standY))
            {
                filter.Pose->X = standX;
                filter.Pose->Y = standY;
                CellOccupancy.ResolveStanding(frame, standX, standY, out var onFloor, out var standingTier);
                filter.Pawn->IsOnFloor = onFloor;
                filter.Pawn->StandingTier = standingTier;
            }

            filter.Pawn->MoveSpeed = FP._0;
            WritePose(frame, ref filter, followerX, followerZ);
        }

        static void BeginPushFollow(
            Frame frame,
            ref Filter filter,
            EntityRef dice,
            int fromX,
            int fromY,
            int dirX,
            int dirY)
        {
            var ticks = ResolvePushMotionTicks(frame);
            filter.Pawn->HasPushFollow = true;
            filter.Pawn->PushFollowDice = dice;
            filter.Pawn->PushFollowDirX = dirX;
            filter.Pawn->PushFollowDirY = dirY;
            filter.Pawn->PushFollowFromX = fromX;
            filter.Pawn->PushFollowFromY = fromY;
            filter.Pawn->PushFollowTicksRemaining = ticks;
            filter.Pawn->PushFollowTicksTotal = ticks;
            filter.Pawn->MoveSpeed = FP._0;
        }

        static void EndPushFollow(PlayerPawn* pawn)
        {
            pawn->HasPushFollow = false;
            pawn->PushFollowDice = EntityRef.None;
            pawn->PushFollowDirX = 0;
            pawn->PushFollowDirY = 0;
            pawn->PushFollowFromX = 0;
            pawn->PushFollowFromY = 0;
            pawn->PushFollowTicksRemaining = 0;
            pawn->PushFollowTicksTotal = 0;
        }

        static void TickContinuousMove(Frame frame, Board board, ref Filter filter, Input* input)
        {
            if (LiftPhaseMachine.IsBusy(filter.Pawn->LiftPhase)
                || CoupledWalkRollAdapter.IsBusy(*filter.Pawn))
            {
                filter.Pawn->MoveSpeed = FP._0;
                return;
            }

            // Carrying: facing updates only (no free move while holding — matches production lift gate).
            if (filter.Pawn->HasCarriedDice
                && filter.Pawn->LiftPhase == LiftPhaseMachine.Carrying)
            {
                UpdateFacingFromInput(filter.Pawn, input);
                filter.Pawn->MoveSpeed = FP._0;
                return;
            }

            if (filter.Pawn->HasCarriedDice)
            {
                filter.Pawn->MoveSpeed = FP._0;
                return;
            }

            var moveX = 0f;
            var moveY = 0f;
            if (input->MoveE.IsDown)
            {
                moveX += 1f;
            }

            if (input->MoveW.IsDown)
            {
                moveX -= 1f;
            }

            if (input->MoveN.IsDown)
            {
                moveY += 1f;
            }

            if (input->MoveS.IsDown)
            {
                moveY -= 1f;
            }

            if (!CellSurfaceMotion.TryGetPrimaryDirection(moveX, moveY, out var faceDx, out var faceDy)
                && moveX == 0f && moveY == 0f)
            {
                filter.Pawn->MoveSpeed = FP._0;
                return;
            }

            if (faceDx != 0 || faceDy != 0)
            {
                filter.Pawn->FacingX = faceDx;
                filter.Pawn->FacingY = faceDy;
                moveX = faceDx;
                moveY = faceDy;
            }

            var dt = frame.DeltaTime.AsFloat;
            if (dt <= 0f)
            {
                return;
            }

            var maxSpeed = ResolveMaxMoveSpeed(frame).AsFloat;
            var accel = ResolveMoveAcceleration(frame).AsFloat;
            var speed = filter.Pawn->MoveSpeed.AsFloat;
            speed = CellSurfaceMotion.MoveTowards(speed, maxSpeed, accel * dt);
            filter.Pawn->MoveSpeed = FP.FromFloat_UNSAFE(speed);

            var worldX = filter.Pawn->WorldX.AsFloat;
            var worldZ = filter.Pawn->WorldZ.AsFloat;
            var nextX = worldX + moveX * speed * dt;
            var nextZ = worldZ + moveY * speed * dt;

            var cellSize = ResolveCellSize(frame).AsFloat;
            var walkHalf = cellSize * 0.5f;
            var triggerRatio = ResolveRollTriggerRatio(frame);
            var triggerHalf = walkHalf * triggerRatio;

            CellSurfaceMotion.CellCenter(
                filter.Pose->X,
                filter.Pose->Y,
                cellSize,
                out var centerX,
                out var centerZ);

            if (!CellSurfaceMotion.TryGetPrimaryDirection(moveX, moveY, out var dirX, out var dirY))
            {
                WritePose(frame, ref filter, nextX, nextZ);
                return;
            }

            if (!CellSurfaceMotion.IsAtOrPastRollTrigger(
                    nextX,
                    nextZ,
                    centerX,
                    centerZ,
                    dirX,
                    dirY,
                    triggerHalf))
            {
                CellSurfaceMotion.ClampToCellInterior(ref nextX, ref nextZ, centerX, centerZ, walkHalf);
                WritePose(frame, ref filter, nextX, nextZ);
                return;
            }

            var nextCellX = filter.Pose->X + dirX;
            var nextCellY = filter.Pose->Y + dirY;
            var pawnSnapshot = *filter.Pawn;
            var poseSnapshot = *filter.Pose;
            var isJumping = filter.Pawn->IsJumping;
            var action = FrameMoveActionResolver.Resolve(
                frame,
                board,
                in pawnSnapshot,
                in poseSnapshot,
                filter.Entity,
                nextCellX,
                nextCellY,
                dirX,
                dirY,
                isJumping);

            if (action == DiceGame.SimShared.Move.MoveAction.CoupledJumpGrid
                && TryJumpGridRollAtTrigger(frame, board, ref filter, dirX, dirY))
            {
                return;
            }

            if (action == DiceGame.SimShared.Move.MoveAction.Blocked)
            {
                CellSurfaceMotion.CancelMoveIntoDirection(worldX, worldZ, ref nextX, ref nextZ, dirX, dirY);
                CellSurfaceMotion.ClampToCellInterior(ref nextX, ref nextZ, centerX, centerZ, walkHalf);
                WritePose(frame, ref filter, nextX, nextZ);
                return;
            }

            if (action == DiceGame.SimShared.Move.MoveAction.GridRoll
                || action == DiceGame.SimShared.Move.MoveAction.IceSlide)
            {
                if (!isJumping
                    && CoupledWalkRollAdapter.TryBegin(frame, board, ref filter, dirX, dirY))
                {
                    return;
                }
            }

            if (action == DiceGame.SimShared.Move.MoveAction.PlayerWalk
                || action == DiceGame.SimShared.Move.MoveAction.PlayerWalkFloor
                || action == DiceGame.SimShared.Move.MoveAction.HeightTransfer
                || action == DiceGame.SimShared.Move.MoveAction.FloorToBottomMount
                || action == DiceGame.SimShared.Move.MoveAction.TierLanding)
            {
                if (PawnTransferPolicy.CanTransferToCell(
                        frame,
                        board,
                        in pawnSnapshot,
                        in poseSnapshot,
                        filter.Entity,
                        nextCellX,
                        nextCellY,
                        isJumping))
                {
                    filter.Pose->X = nextCellX;
                    filter.Pose->Y = nextCellY;
                    CellOccupancy.ResolveStanding(frame, nextCellX, nextCellY, out var onFloor, out var standingTier);
                    filter.Pawn->IsOnFloor = onFloor;
                    filter.Pawn->StandingTier = standingTier;
                    WritePose(frame, ref filter, nextX, nextZ);
                    return;
                }
            }

            // Ground couple fallback when selector returned ContinueToLanding / GridRoll miss.
            if (!isJumping
                && CoupledWalkRollAdapter.TryBegin(frame, board, ref filter, dirX, dirY))
            {
                return;
            }

            // Blocked edge: push (ground only — jump does not push in production).
            if (!isJumping
                && PushPassability.TryPushOneCell(
                    frame,
                    board,
                    in pawnSnapshot,
                    poseSnapshot,
                    dirX,
                    dirY,
                    out var pushed,
                    out var fromX,
                    out var fromY))
            {
                BeginPushFollow(frame, ref filter, pushed, fromX, fromY, dirX, dirY);
                ApplyPushFollowPose(frame, board, ref filter, fromX + dirX, fromY + dirY, dirX, dirY, t01: 0f);
                return;
            }

            CellSurfaceMotion.CancelMoveIntoDirection(worldX, worldZ, ref nextX, ref nextZ, dirX, dirY);
            CellSurfaceMotion.ClampToCellInterior(ref nextX, ref nextZ, centerX, centerZ, walkHalf);
            WritePose(frame, ref filter, nextX, nextZ);
        }

        static void UpdateFacingFromInput(PlayerPawn* pawn, Input* input)
        {
            var moveX = 0f;
            var moveY = 0f;
            if (input->MoveE.IsDown)
            {
                moveX += 1f;
            }

            if (input->MoveW.IsDown)
            {
                moveX -= 1f;
            }

            if (input->MoveN.IsDown)
            {
                moveY += 1f;
            }

            if (input->MoveS.IsDown)
            {
                moveY -= 1f;
            }

            if (CellSurfaceMotion.TryGetPrimaryDirection(moveX, moveY, out var faceDx, out var faceDy))
            {
                pawn->FacingX = faceDx;
                pawn->FacingY = faceDy;
            }
        }

        static bool TryJumpGridRollAtTrigger(
            Frame frame,
            Board board,
            ref Filter filter,
            int dirX,
            int dirY)
        {
            if (!TryResolveJumpCapability(frame, ref filter, out var capability)
                || !capability.AllowDiceGridMove)
            {
                return false;
            }

            return CoupledWalkRollAdapter.TryBeginJumpGridRoll(
                frame,
                board,
                ref filter,
                dirX,
                dirY,
                capability.MaxDistance,
                capability.AllowTierChange);
        }

        static bool TryResolveJumpCapability(
            Frame frame,
            ref Filter filter,
            out JumpCoupledMoveCapability capability)
        {
            capability = default;
            var pawn = *filter.Pawn;
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
                    filter.Pose->X,
                    filter.Pose->Y,
                    pawn.StandingTier,
                    out var diceEntity,
                    out var dice))
            {
                var effective = EffectiveDiceQuery.ResolveAt(
                    frame,
                    diceEntity,
                    in dice,
                    filter.Pose->X,
                    filter.Pose->Y);
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

        static float ResolveJumpTimelinePermille(int permille, int fallback)
        {
            var value = permille > 0 ? permille : fallback;
            return value / 1000f;
        }

        static void WritePose(Frame frame, ref Filter filter, float worldX, float worldZ)
        {
            filter.Pawn->WorldX = FP.FromFloat_UNSAFE(worldX);
            filter.Pawn->WorldZ = FP.FromFloat_UNSAFE(worldZ);
            filter.Pawn->HasWorldPose = true;
            SyncPawnTransform(frame, filter.Entity, filter.Pawn);
        }

        static void SyncPawnTransform(Frame frame, EntityRef entity, PlayerPawn* pawn)
        {
            var position = new FPVector2(pawn->WorldX, pawn->WorldZ);
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

        static void TryBeginJump(Frame frame, ref Filter filter)
        {
            // Production CharacterController.TryBeginJump gates.
            if (!JumpBeginRules.CanBegin(
                    filter.Pawn->IsJumping,
                    filter.Pawn->HasCarriedDice,
                    CoupledWalkRollAdapter.IsBusy(*filter.Pawn),
                    filter.Pawn->HasPushFollow)
                || filter.Pawn->LiftPhase != LiftPhaseMachine.None)
            {
                return;
            }

            var height = ResolveJumpHeight(frame);
            var gravity = ResolveJumpGravity(frame);
            // Production: GravityMotion.CreateLaunch(GetDiceJumpHeight(), gravity)
            var launch = GravityMotion.CreateLaunch(height, gravity);
            filter.Pawn->IsJumping = true;
            filter.Pawn->JumpDiceGridMoved = false;
            filter.Pawn->IsJumpArc = false;
            filter.Pawn->JumpOffsetY = FP.FromFloat_UNSAFE(launch.Offset);
            filter.Pawn->JumpVelocityY = FP.FromFloat_UNSAFE(launch.VelocityY);
            filter.Pawn->JumpHeight = FP.FromFloat_UNSAFE(height);
            filter.Pawn->JumpLaunchVy = FP.FromFloat_UNSAFE(launch.VelocityY);
            filter.Pawn->MoveSpeed = FP._0;
        }

        static float ResolveJumpHeight(Frame frame)
        {
            var cellSize = ResolveCellSize(frame).AsFloat;
            var multPermille = frame.RuntimeConfig.JumpHeightDiceMultiplierPermille;
            if (multPermille <= 0)
            {
                multPermille = 1000;
            }

            var fallbackMilli = frame.RuntimeConfig.JumpHeightMilli;
            if (fallbackMilli <= 0)
            {
                fallbackMilli = MatchSimDefaults.JumpHeightMilli;
            }

            return JumpHeightRules.ResolveDiceJumpHeight(
                cellSize,
                multPermille / 1000f,
                fallbackMilli / 1000f);
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

        static void ApplyPawnCellSnap(Frame frame, ref Filter filter, int nextX, int nextY)
        {
            filter.Pose->X = nextX;
            filter.Pose->Y = nextY;
            CellOccupancy.ResolveStanding(frame, nextX, nextY, out var onFloor, out var standingTier);
            filter.Pawn->IsOnFloor = onFloor;
            filter.Pawn->StandingTier = standingTier;

            var cellSize = ResolveCellSize(frame);
            CellSurfaceMotion.CellCenter(nextX, nextY, cellSize.AsFloat, out var cx, out var cz);
            filter.Pawn->WorldX = FP.FromFloat_UNSAFE(cx);
            filter.Pawn->WorldZ = FP.FromFloat_UNSAFE(cz);
            filter.Pawn->MoveSpeed = FP._0;
            filter.Pawn->HasWorldPose = true;
            SyncPawnTransform(frame, filter.Entity, filter.Pawn);

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
            if (CoupledWalkRollAdapter.IsBusy(*filter.Pawn)
                || filter.Pawn->IsJumping
                || LiftPhaseMachine.IsBusy(filter.Pawn->LiftPhase))
            {
                return;
            }

            // Production: while Carrying, lift input places (direction = facing).
            if (filter.Pawn->HasCarriedDice
                && LiftPhaseMachine.CanBeginPlace(filter.Pawn->LiftPhase))
            {
                TryPlaceCarriedDice(frame, board, ref filter);
                return;
            }

            if (filter.Pawn->HasCarriedDice)
            {
                return;
            }

            if (!LiftPhaseMachine.CanBeginLift(
                    filter.Pawn->LiftPhase,
                    filter.Pawn->IsJumping,
                    filter.Pawn->HasPushFollow))
            {
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

            if (dicePtr->IsMotionBusy || dicePtr->IsSpawning)
            {
                return;
            }

            // Production TryBeginCarry: isCarried immediate; logical busy = LiftDuration.
            dicePtr->IsCarried = true;
            dicePtr->Owner = filter.Pawn->Player;
            filter.Pawn->CarriedDice = dice;
            filter.Pawn->HasCarriedDice = true;
            filter.Pawn->LiftPhase = LiftPhaseMachine.Lifting;
            filter.Pawn->LiftBusyTicksRemaining = ResolveLiftBusyTicks(frame, placing: false);
            filter.Pawn->MoveSpeed = FP._0;

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

        static void TryPlaceCarriedDice(Frame frame, Board board, ref Filter filter)
        {
            var dice = filter.Pawn->CarriedDice;
            if (!dice.IsValid)
            {
                filter.Pawn->HasCarriedDice = false;
                filter.Pawn->CarriedDice = EntityRef.None;
                filter.Pawn->LiftPhase = LiftPhaseMachine.None;
                return;
            }

            if (!LiftEligibility.HasFacing(filter.Pawn->FacingX, filter.Pawn->FacingY))
            {
                return;
            }

            var x = filter.Pose->X + filter.Pawn->FacingX;
            var y = filter.Pose->Y + filter.Pawn->FacingY;
            if (!CellOccupancy.TryResolveDropTier(frame, board, x, y, out var tier))
            {
                return;
            }

            if (frame.Unsafe.TryGetPointer<Dice>(dice, out var dicePtr) == false)
            {
                return;
            }

            // Production TryPlaceAt: enter Placing, then FreeMove duration; commit on complete.
            filter.Pawn->LiftPhase = LiftPhaseMachine.Placing;
            filter.Pawn->LiftBusyTicksRemaining = ResolveLiftBusyTicks(frame, placing: true);

            dicePtr->IsCarried = false;
            dicePtr->Tier = tier;
            dicePtr->Owner = filter.Pawn->Player;

            if (frame.Unsafe.TryGetPointer<GridPose>(dice, out var pose))
            {
                pose->X = x;
                pose->Y = y;
                BoardBootstrapSystem.SyncTransform(frame, dice, x, y, tier);
            }

            filter.Pawn->MoveSpeed = FP._0;
            MatchSettleRequest.Request(frame, dice, filter.Pawn->Player);
        }

        static int ResolveLiftBusyTicks(Frame frame, bool placing)
        {
            var ticks = placing
                ? frame.RuntimeConfig.PlaceDurationTicks
                : frame.RuntimeConfig.LiftDurationTicks;
            var fallback = placing
                ? MatchSimDefaults.PlaceDurationTicks
                : MatchSimDefaults.LiftDurationTicks;
            return ticks > 0 ? ticks : fallback;
        }

        static FP ResolveCellSize(Frame frame)
        {
            var cellSize = frame.RuntimeConfig.CellSize;
            return cellSize > FP._0 ? cellSize : FP._1;
        }

        static FP ResolveMaxMoveSpeed(Frame frame)
        {
            var milli = frame.RuntimeConfig.MaxMoveSpeedMilli;
            if (milli <= 0)
            {
                milli = MatchSimDefaults.MaxMoveSpeedMilli;
            }

            return FP.FromFloat_UNSAFE(milli / 1000f);
        }

        static FP ResolveMoveAcceleration(Frame frame)
        {
            var milli = frame.RuntimeConfig.MoveAccelerationMilli;
            if (milli <= 0)
            {
                milli = MatchSimDefaults.MoveAccelerationMilli;
            }

            return FP.FromFloat_UNSAFE(milli / 1000f);
        }

        static float ResolveRollTriggerRatio(Frame frame)
        {
            var permille = frame.RuntimeConfig.RollTriggerExtentPermille;
            if (permille <= 0)
            {
                permille = MatchSimDefaults.RollTriggerExtentPermille;
            }

            return permille / 1000f;
        }

        static int ResolvePushMotionTicks(Frame frame)
        {
            var ticks = frame.RuntimeConfig.PushMotionTicks;
            return ticks > 0 ? ticks : MatchSimDefaults.PushMotionTicks;
        }

        static FP ResolvePushContactRadius(Frame frame)
        {
            var milli = frame.RuntimeConfig.PushContactRadiusMilli;
            if (milli < 0)
            {
                milli = MatchSimDefaults.PushContactRadiusMilli;
            }

            return FP.FromFloat_UNSAFE(milli / 1000f);
        }

        public struct Filter
        {
            public EntityRef Entity;
            public PlayerPawn* Pawn;
            public GridPose* Pose;
        }
    }
}
