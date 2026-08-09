namespace Quantum
{
    using DiceGame.Core;
    using DiceGame.SimShared.Coupling;
    using DiceGame.SimShared.GridMove;
    using DiceGame.SimShared.Jump;
    using DiceGame.SimShared.Motion;
    using Photon.Deterministic;
    using SimDiceState = DiceGame.SimShared.Motion.DiceState;

    /// <summary>
    /// Thin Frame adapter: fills <see cref="CoupledWalkRollRequest"/>, applies
    /// <see cref="CoupledWalkRollCommit"/>, maps <see cref="CoupledWalkRollSession"/> onto pawn.
    /// Domain owns plan + ride lock; do not re-split into StandingCouple + PushFollow.
    /// </summary>
    public static unsafe class CoupledWalkRollAdapter
    {
        public static bool TryBegin(
            Frame frame,
            Board board,
            ref PlayerActionSystem.Filter filter,
            int dirX,
            int dirY)
        {
            var pawn = *filter.Pawn;
            var pose = *filter.Pose;
            if (pawn.IsOnFloor || pawn.HasCarriedDice || pawn.HasCoupledWalkRoll)
            {
                return false;
            }

            if (!CellOccupancy.TryGetAt(
                    frame,
                    pose.X,
                    pose.Y,
                    pawn.StandingTier,
                    out var diceEntity,
                    out var dice))
            {
                return false;
            }

            var effective = EffectiveDiceQuery.ResolveAt(frame, diceEntity, in dice, pose.X, pose.Y);
            if (!effective.IsPlayerMovable)
            {
                return false;
            }

            var caps = effective.Capabilities;
            var cellSize = frame.RuntimeConfig.CellSize;
            if (cellSize <= FP._0)
            {
                cellSize = FP._1;
            }

            var motionTicks = frame.RuntimeConfig.PushMotionTicks;
            if (motionTicks <= 0)
            {
                motionTicks = MatchSimDefaults.PushMotionTicks;
            }

            // Ice: Domain until-blocked with Ghost + partition (not place-only lite path).
            if (caps.SlideUntilBlocked)
            {
                return TryBeginIceSlide(
                    frame,
                    board,
                    ref filter,
                    diceEntity,
                    in dice,
                    dirX,
                    dirY,
                    cellSize.AsFloat,
                    motionTicks);
            }

            var ignorePawn = filter.Entity;
            var request = new CoupledWalkRollRequest
            {
                StandingCellX = pose.X,
                StandingCellY = pose.Y,
                StandingTier = pawn.StandingTier == DiceStackTier.Top ? 1 : 0,
                DirX = dirX,
                DirY = dirY,
                PawnWorldX = pawn.WorldX.AsFloat,
                PawnWorldZ = pawn.WorldZ.AsFloat,
                BoardWidth = board.Width,
                BoardHeight = board.Height,
                DiceTopFace = dice.TopFace,
                DiceNorthFace = dice.NorthFace,
                DiceEastFace = dice.EastFace,
                CanGridRoll = caps.CanGridRoll,
                SlideUntilBlocked = false,
                IsPlayerPassThrough = caps.IsPlayerPassThrough,
                DiceBusy = dice.IsMotionBusy || dice.IsSpawning,
                DiceCarried = dice.IsCarried,
                DiceErasing = dice.IsErasing,
                MotionTicks = motionTicks,
                CellSize = cellSize.AsFloat,
                CanPlaceBottomAt = (x, y) => CellOccupancy.CanPlaceBottomAt(frame, board, x, y),
                CanPlaceTopAt = (x, y) => CellOccupancy.CanPlaceTopAt(frame, board, x, y),
                HasSolidBottomAt = (x, y) => CellOccupancy.HasSolidBottomAt(frame, x, y),
                IsPawnOccupiedAt = (x, y) => BoardBootstrapSystem.IsPawnOccupied(frame, x, y, ignorePawn),
            };

            if (!CoupledWalkRoll.TryBegin(in request, out var commit, out var session))
            {
                return false;
            }

            ApplyCommit(frame, diceEntity, in commit, pawn.Player);
            WriteSession(filter.Pawn, diceEntity, in session);
            ApplyTickPose(frame, ref filter, in session, firstTick: true);
            return true;
        }

        static bool TryBeginIceSlide(
            Frame frame,
            Board board,
            ref PlayerActionSystem.Filter filter,
            EntityRef diceEntity,
            in Dice dice,
            int dirX,
            int dirY,
            float cellSize,
            int baseMotionTicks)
        {
            var pose = *filter.Pose;
            var tier = filter.Pawn->StandingTier == DiceStackTier.Top ? 1 : 0;
            if (dice.IsMotionBusy || dice.IsSpawning || dice.IsCarried || dice.IsErasing)
            {
                return false;
            }

            if (!IceSlideFrameQueries.TryPlan(
                    frame,
                    board,
                    pose.X,
                    pose.Y,
                    tier,
                    dirX,
                    dirY,
                    dice.Kind,
                    out var plan))
            {
                return false;
            }

            if (BoardBootstrapSystem.IsPawnOccupied(frame, plan.DestX, plan.DestY, filter.Entity))
            {
                return false;
            }

            var slideTicks = frame.RuntimeConfig.SlideDurationTicks > 0
                ? frame.RuntimeConfig.SlideDurationTicks
                : MatchSimDefaults.SlideDurationTicks;
            var motionTicks = DiceGame.SimShared.Slide.IceSlideUntilBlockedPlanner.ResolveMotionTicks(
                slideTicks,
                plan.StepCount);

            var pawnStanding = plan.HasPartitionDismount
                ? CoupledWalkRollStanding.Floor
                : plan.LandingTier;
            var commit = new CoupledWalkRollCommit
            {
                DiceFromX = pose.X,
                DiceFromY = pose.Y,
                DiceDestX = plan.DestX,
                DiceDestY = plan.DestY,
                LandingTier = plan.LandingTier,
                NextTopFace = dice.TopFace,
                NextNorthFace = dice.NorthFace,
                NextEastFace = dice.EastFace,
                DemoteUnsupportedTopAtFrom = plan.DemoteUnsupportedTopAtFrom,
                PawnCellX = plan.HasPartitionDismount ? plan.DismountX : plan.DestX,
                PawnCellY = plan.HasPartitionDismount ? plan.DismountY : plan.DestY,
                PawnStandingTier = pawnStanding,
                MotionTicks = motionTicks,
            };

            var session = new CoupledWalkRollSession
            {
                Active = true,
                DirX = dirX,
                DirY = dirY,
                FromX = pose.X,
                FromY = pose.Y,
                DestX = commit.PawnCellX,
                DestY = commit.PawnCellY,
                StandingTier = pawnStanding,
                TicksRemaining = motionTicks,
                TicksTotal = motionTicks,
                CellSize = cellSize,
            };

            ApplyCommit(frame, diceEntity, in commit, filter.Pawn->Player);
            IceSlideFrameQueries.ApplyGhostSwap(frame, in plan);
            WriteSession(filter.Pawn, diceEntity, in session);
            ApplyTickPose(frame, ref filter, in session, firstTick: true);
            return true;
        }

        /// <summary>
        /// Production JumpGridRoll: <see cref="JumpGridRollPolicy"/> + <see cref="JumpCoupleSession"/>.
        /// </summary>
        public static bool TryBeginJumpGridRoll(
            Frame frame,
            Board board,
            ref PlayerActionSystem.Filter filter,
            int dirX,
            int dirY,
            int maxDistance,
            bool allowTierChange)
        {
            var pawn = *filter.Pawn;
            var pose = *filter.Pose;
            var couple = new JumpCoupleSession.State
            {
                IsJumpArc = pawn.IsJumpArc,
                JumpDiceGridMoved = pawn.JumpDiceGridMoved,
            };
            if (!pawn.IsJumping
                || !JumpCoupleSession.CanBeginJumpGridMove(in couple)
                || pawn.IsOnFloor
                || pawn.HasCarriedDice
                || pawn.HasCoupledWalkRoll)
            {
                return false;
            }

            if (!CellOccupancy.TryGetAt(
                    frame,
                    pose.X,
                    pose.Y,
                    pawn.StandingTier,
                    out var diceEntity,
                    out var dice))
            {
                return false;
            }

            var effective = EffectiveDiceQuery.ResolveAt(frame, diceEntity, in dice, pose.X, pose.Y);
            if (!effective.IsPlayerMovable || !effective.Capabilities.CanGridRoll)
            {
                return false;
            }

            if (dice.IsMotionBusy || dice.IsSpawning || dice.IsCarried || dice.IsErasing)
            {
                return false;
            }

            if (!TryToDirection(dirX, dirY, out var direction))
            {
                return false;
            }

            var fromState = new SimDiceState(
                pose.X,
                pose.Y,
                CoreDiceBridge.ToCoreOrientation(dice.TopFace, dice.NorthFace, dice.EastFace),
                CoreDiceBridge.ToCoreTier(pawn.StandingTier),
                CoreDiceBridge.ToCoreKind(dice.Kind));

            var context = PassabilityContext.Jump(
                allowJumpGridMove: true,
                allowJumpTierChange: allowTierChange,
                footingWorldY: 0f);

            var occupancy = new FrameGridRollOccupancy(frame, board);
            var kindMax = effective.Capabilities.GetEffectiveMaxJumpGridMoveDistance();
            if (!JumpGridRollPolicy.TryBuildBestPlan(
                    occupancy,
                    fromState,
                    direction,
                    maxDistance,
                    kindMax,
                    allowsRoll: true,
                    context,
                    out var plan,
                    out _))
            {
                return false;
            }

            if (BoardBootstrapSystem.IsPawnOccupied(frame, plan.To.GridX, plan.To.GridY, filter.Entity))
            {
                return false;
            }

            var cellSize = frame.RuntimeConfig.CellSize;
            if (cellSize <= FP._0)
            {
                cellSize = FP._1;
            }

            var motionTicks = frame.RuntimeConfig.PushMotionTicks;
            if (motionTicks <= 0)
            {
                motionTicks = MatchSimDefaults.PushMotionTicks;
            }

            var demoteTop = plan.From.Tier == DiceGame.Core.DiceStackTier.Bottom;
            var commit = new CoupledWalkRollCommit
            {
                DiceFromX = plan.From.GridX,
                DiceFromY = plan.From.GridY,
                DiceDestX = plan.To.GridX,
                DiceDestY = plan.To.GridY,
                LandingTier = plan.To.Tier == DiceGame.Core.DiceStackTier.Top ? 1 : 0,
                NextTopFace = plan.To.Orientation.Top,
                NextNorthFace = plan.To.Orientation.North,
                NextEastFace = plan.To.Orientation.East,
                DemoteUnsupportedTopAtFrom = demoteTop,
                PawnCellX = plan.To.GridX,
                PawnCellY = plan.To.GridY,
                PawnStandingTier = plan.To.Tier == DiceGame.Core.DiceStackTier.Top ? 1 : 0,
                MotionTicks = motionTicks,
            };

            var session = new CoupledWalkRollSession
            {
                Active = true,
                DirX = dirX,
                DirY = dirY,
                FromX = plan.From.GridX,
                FromY = plan.From.GridY,
                DestX = plan.To.GridX,
                DestY = plan.To.GridY,
                StandingTier = commit.PawnStandingTier,
                TicksRemaining = motionTicks,
                TicksTotal = motionTicks,
                CellSize = cellSize.AsFloat,
            };

            ApplyCommit(frame, diceEntity, in commit, pawn.Player);
            if (plan.HasGhostSwap)
            {
                ApplyPlanGhostSwap(frame, in plan);
            }

            JumpCoupleSession.MarkJumpGridMoveStarted(ref couple, plan.Kind);
            filter.Pawn->JumpDiceGridMoved = couple.JumpDiceGridMoved;
            filter.Pawn->IsJumpArc = couple.IsJumpArc;
            WriteSession(filter.Pawn, diceEntity, in session);
            ApplyTickPose(frame, ref filter, in session, firstTick: true);
            return true;
        }

        static void ApplyPlanGhostSwap(Frame frame, in DiceGridMovePlan plan)
        {
            var fromTier = CoreDiceBridge.ToQuantumTier(plan.GhostFrom.Tier);
            if (!CellOccupancy.TryGetAt(
                    frame,
                    plan.GhostFrom.GridX,
                    plan.GhostFrom.GridY,
                    fromTier,
                    out var ghost,
                    out _))
            {
                return;
            }

            if (!frame.Unsafe.TryGetPointer<Dice>(ghost, out var ghostDice)
                || !frame.Unsafe.TryGetPointer<GridPose>(ghost, out var ghostPose))
            {
                return;
            }

            var toTier = CoreDiceBridge.ToQuantumTier(plan.GhostTo.Tier);
            ghostPose->X = plan.GhostTo.GridX;
            ghostPose->Y = plan.GhostTo.GridY;
            ghostDice->Tier = toTier;
            BoardBootstrapSystem.SyncTransform(
                frame,
                ghost,
                plan.GhostTo.GridX,
                plan.GhostTo.GridY,
                toTier);
        }

        static bool TryToDirection(int dirX, int dirY, out Direction direction)
        {
            direction = default;
            if (dirX == 1 && dirY == 0)
            {
                direction = Direction.East;
                return true;
            }

            if (dirX == -1 && dirY == 0)
            {
                direction = Direction.West;
                return true;
            }

            if (dirX == 0 && dirY == 1)
            {
                direction = Direction.North;
                return true;
            }

            if (dirX == 0 && dirY == -1)
            {
                direction = Direction.South;
                return true;
            }

            return false;
        }

        public static bool Tick(Frame frame, Board board, ref PlayerActionSystem.Filter filter)
        {
            if (!filter.Pawn->HasCoupledWalkRoll)
            {
                return false;
            }

            var session = ReadSession(filter.Pawn);
            if (!CoupledWalkRoll.IsBusy(in session) && !session.Active)
            {
                ClearSession(filter.Pawn);
                return false;
            }

            CoupledWalkRoll.Tick(ref session, out var tick);
            WriteSession(filter.Pawn, filter.Pawn->CoupledWalkRollDice, in session);

            filter.Pose->X = tick.PawnCellX;
            filter.Pose->Y = tick.PawnCellY;
            ApplyStandingFromSession(filter.Pawn, tick.PawnStandingTier);
            filter.Pawn->MoveSpeed = FP._0;
            filter.Pawn->WorldX = FP.FromFloat_UNSAFE(tick.PawnWorldX);
            filter.Pawn->WorldZ = FP.FromFloat_UNSAFE(tick.PawnWorldZ);
            filter.Pawn->HasWorldPose = true;
            SyncPawnTransform(frame, filter.Entity, filter.Pawn);

            if (tick.Completed)
            {
                ClearSession(filter.Pawn);
            }

            return true;
        }

        public static bool IsBusy(in PlayerPawn pawn) => pawn.HasCoupledWalkRoll;

        static void ApplyCommit(Frame frame, EntityRef diceEntity, in CoupledWalkRollCommit commit, PlayerRef player)
        {
            if (!frame.Unsafe.TryGetPointer<Dice>(diceEntity, out var dicePtr)
                || !frame.Unsafe.TryGetPointer<GridPose>(diceEntity, out var dicePose))
            {
                return;
            }

            EntityRef topEntity = EntityRef.None;
            var hadTop = commit.DemoteUnsupportedTopAtFrom
                && CellOccupancy.TryGetTopAt(frame, commit.DiceFromX, commit.DiceFromY, out topEntity, out _);

            dicePose->X = commit.DiceDestX;
            dicePose->Y = commit.DiceDestY;
            dicePtr->Tier = commit.LandingTier == 1 ? DiceStackTier.Top : DiceStackTier.Bottom;
            dicePtr->TopFace = commit.NextTopFace;
            dicePtr->NorthFace = commit.NextNorthFace;
            dicePtr->EastFace = commit.NextEastFace;
            dicePtr->IsMotionBusy = true;
            dicePtr->MotionTicksRemaining = commit.MotionTicks;
            BoardBootstrapSystem.SyncTransform(
                frame,
                diceEntity,
                commit.DiceDestX,
                commit.DiceDestY,
                dicePtr->Tier);

            if (hadTop
                && topEntity.IsValid
                && frame.Unsafe.TryGetPointer<Dice>(topEntity, out var topDice)
                && !topDice->IsErasing)
            {
                topDice->Tier = DiceStackTier.Bottom;
                BoardBootstrapSystem.SyncTransform(
                    frame,
                    topEntity,
                    commit.DiceFromX,
                    commit.DiceFromY,
                    DiceStackTier.Bottom);
            }

            MatchSettleRequest.Request(frame, diceEntity, player);
        }

        static void ApplyTickPose(
            Frame frame,
            ref PlayerActionSystem.Filter filter,
            in CoupledWalkRollSession session,
            bool firstTick)
        {
            var working = session;
            if (firstTick)
            {
                // Present t=0 ride pose without consuming a tick (Tick will advance next frames).
                CellSurfaceMotionCellCenter(session, 0f, out var x, out var z);
                filter.Pose->X = session.DestX;
                filter.Pose->Y = session.DestY;
                ApplyStandingFromSession(filter.Pawn, session.StandingTier);
                filter.Pawn->MoveSpeed = FP._0;
                filter.Pawn->WorldX = FP.FromFloat_UNSAFE(x);
                filter.Pawn->WorldZ = FP.FromFloat_UNSAFE(z);
                filter.Pawn->HasWorldPose = true;
                SyncPawnTransform(frame, filter.Entity, filter.Pawn);
                return;
            }

            CoupledWalkRoll.Tick(ref working, out _);
        }

        static void CellSurfaceMotionCellCenter(
            in CoupledWalkRollSession session,
            float t01,
            out float x,
            out float z)
        {
            DiceGame.SimShared.Move.CellSurfaceMotion.CellCenter(
                session.FromX,
                session.FromY,
                session.CellSize,
                out var fromX,
                out var fromZ);
            DiceGame.SimShared.Move.CellSurfaceMotion.CellCenter(
                session.DestX,
                session.DestY,
                session.CellSize,
                out var toX,
                out var toZ);
            DiceGame.SimShared.Push.PushFollowMotion.Lerp(fromX, fromZ, toX, toZ, t01, out x, out z);
        }

        static CoupledWalkRollSession ReadSession(PlayerPawn* pawn)
        {
            return new CoupledWalkRollSession
            {
                Active = pawn->HasCoupledWalkRoll,
                DirX = pawn->CoupledWalkRollDirX,
                DirY = pawn->CoupledWalkRollDirY,
                FromX = pawn->CoupledWalkRollFromX,
                FromY = pawn->CoupledWalkRollFromY,
                DestX = pawn->CoupledWalkRollDestX,
                DestY = pawn->CoupledWalkRollDestY,
                StandingTier = pawn->CoupledWalkRollStandingTier,
                TicksRemaining = pawn->CoupledWalkRollTicksRemaining,
                TicksTotal = pawn->CoupledWalkRollTicksTotal,
                CellSize = pawn->CoupledWalkRollCellSize.AsFloat,
            };
        }

        static void WriteSession(PlayerPawn* pawn, EntityRef dice, in CoupledWalkRollSession session)
        {
            pawn->HasCoupledWalkRoll = session.Active;
            pawn->CoupledWalkRollDice = dice;
            pawn->CoupledWalkRollDirX = session.DirX;
            pawn->CoupledWalkRollDirY = session.DirY;
            pawn->CoupledWalkRollFromX = session.FromX;
            pawn->CoupledWalkRollFromY = session.FromY;
            pawn->CoupledWalkRollDestX = session.DestX;
            pawn->CoupledWalkRollDestY = session.DestY;
            pawn->CoupledWalkRollTicksRemaining = session.TicksRemaining;
            pawn->CoupledWalkRollTicksTotal = session.TicksTotal;
            pawn->CoupledWalkRollStandingTier = session.StandingTier;
            pawn->CoupledWalkRollCellSize = FP.FromFloat_UNSAFE(session.CellSize);
        }

        static void ClearSession(PlayerPawn* pawn)
        {
            pawn->HasCoupledWalkRoll = false;
            pawn->CoupledWalkRollDice = EntityRef.None;
            pawn->CoupledWalkRollDirX = 0;
            pawn->CoupledWalkRollDirY = 0;
            pawn->CoupledWalkRollFromX = 0;
            pawn->CoupledWalkRollFromY = 0;
            pawn->CoupledWalkRollDestX = 0;
            pawn->CoupledWalkRollDestY = 0;
            pawn->CoupledWalkRollTicksRemaining = 0;
            pawn->CoupledWalkRollTicksTotal = 0;
            pawn->CoupledWalkRollStandingTier = 0;
            pawn->CoupledWalkRollCellSize = FP._0;
        }

        static void ApplyStandingFromSession(PlayerPawn* pawn, int standingTierNorm)
        {
            if (standingTierNorm == CoupledWalkRollStanding.Floor)
            {
                pawn->IsOnFloor = true;
                pawn->StandingTier = DiceStackTier.Bottom;
                return;
            }

            pawn->IsOnFloor = false;
            pawn->StandingTier = standingTierNorm == CoupledWalkRollStanding.Top
                ? DiceStackTier.Top
                : DiceStackTier.Bottom;
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
    }
}
