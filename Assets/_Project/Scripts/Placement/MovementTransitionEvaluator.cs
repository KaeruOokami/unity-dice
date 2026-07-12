using System;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Placement
{
    public class MovementTransitionEvaluator {
        readonly Board board;
        readonly DiceRegistry registry;
        readonly SurfaceQuery surfaceQuery;
        readonly CellOccupancyQuery occupancyQuery;
        readonly GridMovePlanBuilder gridPlanBuilder;
        readonly HeightStepLimits stepLimits;
        Action<string> jumpParallelRollDebugLog;
        Action<string> heightTransferDebugLog;

        public MovementTransitionEvaluator(
            Board board,
            DiceRegistry registry,
            SurfaceQuery surfaceQuery,
            HeightStepLimits stepLimits) {
            this.board = board;
            this.registry = registry;
            this.surfaceQuery = surfaceQuery;
            occupancyQuery = new CellOccupancyQuery(board, registry);
            gridPlanBuilder = new GridMovePlanBuilder(registry, occupancyQuery);
            this.stepLimits = stepLimits;
        }

        HeightReachEvaluation CreateReachEvaluation(
            bool isJumping,
            int fromLevel,
            DiceController standingDice) {
            return new HeightReachEvaluation(
                board.FloorSurfaceWorldY,
                board.CellSize,
                stepLimits,
                isJumping,
                JumpPlayerTransferPolicy.UsesPlayerOnlyJumpStep(isJumping, fromLevel, standingDice));
        }

        public void SetJumpParallelRollDebugLog(Action<string> log) {
            jumpParallelRollDebugLog = log;
        }

        public void SetHeightTransferDebugLog(Action<string> log) {
            heightTransferDebugLog = log;
        }

        void LogJumpParallelRoll(string message) {
            jumpParallelRollDebugLog?.Invoke(message);
        }

        void LogHeightTransfer(string message) {
            heightTransferDebugLog?.Invoke(message);
        }

        public MovementTransition Evaluate(
            Vector2Int fromCell,
            int fromLevel,
            Direction direction,
            DiceController standingDice,
            PassabilityContext context) {
            var toCell = fromCell + direction.ToGridDelta();
            if (!board.IsInside(toCell) || board.GetCell(toCell) == CellType.Wall) {
                return MovementTransition.Blocked();
            }

            if (board.BlocksMovement(fromCell, toCell, context.MovementOwner)) {
                return MovementTransition.Blocked();
            }

            return EvaluateToCell(
                fromCell,
                toCell,
                fromLevel,
                standingDice,
                direction,
                context);
        }

        public float GetStackTopStandingSurfaceY(DiceController bottomDice) {
            return surfaceQuery.GetStackTopStandingSurfaceY(bottomDice);
        }

        public bool TryEvaluatePlayerOnlyTierDemote(
            Vector2Int fromCell,
            int fromLevel,
            DiceController standingDice,
            PassabilityContext context,
            out MovementTransition transition) {
            transition = default;
            if (!context.IsJumping || standingDice == null) {
                return false;
            }

            var fromSurface = surfaceQuery.GetStandingSurface(
                fromCell,
                fromLevel,
                standingDice);
            return PlayerOnlyTierDemotePolicy.TryEvaluate(
                fromCell,
                fromLevel,
                fromSurface,
                standingDice,
                context.IsJumping,
                registry,
                CreateReachEvaluation(context.IsJumping, fromLevel, standingDice),
                out transition);
        }

        public bool IsDescentBlockedOnlyByStepHeight(
            Vector2Int fromCell,
            int fromLevel,
            Direction direction,
            float footingWorldY,
            DiceController standingDice) {
            var transition = Evaluate(
                fromCell,
                fromLevel,
                direction,
                standingDice,
                PassabilityContext.ForGround(footingWorldY));
            return transition.IsDissolveDescentHold;
        }

        public bool IsWalkable(
            Vector2Int fromCell,
            int fromLevel,
            Direction direction,
            float footingWorldY,
            DiceController standingDice) {
            return Evaluate(
                fromCell,
                fromLevel,
                direction,
                standingDice,
                PassabilityContext.ForGround(footingWorldY)).Kind
                == MovementTransitionKind.Walkable;
        }

        public bool IsWalkableBetween(
            Vector2Int fromCell,
            Vector2Int toCell,
            int fromLevel,
            float footingWorldY,
            DiceController standingDice) {
            return TryEvaluateBetween(
                fromCell,
                toCell,
                fromLevel,
                footingWorldY,
                standingDice,
                out var transition)
                && transition.Kind == MovementTransitionKind.Walkable;
        }

        public bool TryEvaluateBetween(
            Vector2Int fromCell,
            Vector2Int toCell,
            int fromLevel,
            float footingWorldY,
            DiceController standingDice,
            out MovementTransition transition) {
            if (fromCell == toCell) {
                transition = default;
                return false;
            }

            if (!TryGetDirectionBetween(fromCell, toCell, out var direction)) {
                transition = MovementTransition.Blocked();
                return true;
            }

            transition = EvaluateToCell(
                fromCell,
                toCell,
                fromLevel,
                standingDice,
                direction,
                PassabilityContext.ForGround(footingWorldY));
            return true;
        }

        public static bool IsOrthogonalAdjacent(Vector2Int fromCell, Vector2Int toCell) {
            return GetOrthogonalDistance(fromCell, toCell) == 1;
        }

        public static int GetOrthogonalDistance(Vector2Int fromCell, Vector2Int toCell) {
            var delta = toCell - fromCell;
            if (delta.x != 0 && delta.y != 0) {
                return -1;
            }

            return Mathf.Abs(delta.x) + Mathf.Abs(delta.y);
        }

        public static bool IsOrthogonalWithinDistance(
            Vector2Int fromCell,
            Vector2Int toCell,
            int maxDistance) {
            var distance = GetOrthogonalDistance(fromCell, toCell);
            return distance >= 1 && distance <= maxDistance;
        }

        public bool TryBuildGridMovePlan(
            DiceState fromState,
            Direction direction,
            int distance,
            PassabilityContext context,
            out DiceGridMovePlan plan,
            out string rejectReason) {
            return gridPlanBuilder.TryBuild(
                fromState,
                direction,
                distance,
                context,
                out plan,
                out rejectReason);
        }

        public bool TryBuildJumpGridMovePlan(
            DiceState fromState,
            Direction direction,
            int distance,
            PassabilityContext context,
            out DiceGridMovePlan plan,
            out string rejectReason) {
            return TryBuildGridMovePlan(fromState, direction, distance, context, out plan, out rejectReason);
        }

        public bool TryGetJumpParallelRollTarget(
            Vector2Int fromCell,
            Direction direction,
            DiceController standingDice,
            int fromLevel,
            int requiredDistance,
            PassabilityContext context,
            out Vector2Int toCell,
            out int distance) {
            toCell = default;
            distance = 0;

            if (requiredDistance < 1 || requiredDistance > DiceGridRollLimits.MaxParallelRollDistance) {
                LogJumpParallelRoll(
                    $"TryGetJumpParallelRollTarget reject distance-out-of-range required={requiredDistance}");
                return false;
            }

            var candidate = fromCell + direction.ToGridDelta() * requiredDistance;
            if (!board.IsInside(candidate) || board.GetCell(candidate) == CellType.Wall) {
                LogJumpParallelRoll(
                    $"TryGetJumpParallelRollTarget reject invalid-candidate from={FormatGrid(fromCell)} " +
                    $"candidate={FormatGrid(candidate)} dir={direction}");
                return false;
            }

            var fromSurface = surfaceQuery.GetStandingSurface(
                fromCell,
                fromLevel,
                standingDice);

            if (!TryEvaluateGridRoll(
                fromCell,
                candidate,
                fromSurface,
                standingDice,
                direction,
                requiredDistance,
                allowMultiCell: requiredDistance > 1,
                context,
                out var rejectReason)) {
                LogJumpParallelRoll(
                    $"TryGetJumpParallelRollTarget reject from={FormatGrid(fromCell)} candidate={FormatGrid(candidate)} " +
                    $"dir={direction} requiredDistance={requiredDistance} stack={FormatStack(candidate)} {rejectReason}");
                return false;
            }

            toCell = candidate;
            distance = requiredDistance;
            LogJumpParallelRoll(
                $"TryGetJumpParallelRollTarget ok from={FormatGrid(fromCell)} to={FormatGrid(candidate)} " +
                $"dir={direction} distance={requiredDistance} stack={FormatStack(candidate)}");
            return true;
        }

        public MovementTransition EvaluateToTargetCell(
            Vector2Int fromCell,
            Vector2Int toCell,
            int fromLevel,
            DiceController standingDice,
            PassabilityContext context) {
            if (!board.IsInside(toCell) || board.GetCell(toCell) == CellType.Wall) {
                return MovementTransition.Blocked();
            }

            if (board.BlocksMovement(fromCell, toCell, context.MovementOwner)) {
                return MovementTransition.Blocked();
            }

            if (!TryGetDirectionBetween(fromCell, toCell, out var direction)) {
                return MovementTransition.Blocked();
            }

            return EvaluateToCell(
                fromCell,
                toCell,
                fromLevel,
                standingDice,
                direction,
                context);
        }

        public static bool TryGetDirectionBetween(
            Vector2Int fromCell,
            Vector2Int toCell,
            out Direction direction) {
            direction = default;
            var delta = toCell - fromCell;
            if (delta.x != 0 && delta.y != 0) {
                return false;
            }

            if (delta.x > 0) {
                direction = Direction.East;
                return true;
            }

            if (delta.x < 0) {
                direction = Direction.West;
                return true;
            }

            if (delta.y > 0) {
                direction = Direction.North;
                return true;
            }

            if (delta.y < 0) {
                direction = Direction.South;
                return true;
            }

            return false;
        }

        MovementTransition EvaluateToCell(
            Vector2Int fromCell,
            Vector2Int toCell,
            int fromLevel,
            DiceController standingDice,
            Direction direction,
            PassabilityContext context) {
            var isJumping = context.IsJumping;
            var reach = CreateReachEvaluation(isJumping, fromLevel, standingDice);
            var fromSurface = surfaceQuery.GetStandingSurface(
                fromCell,
                fromLevel,
                standingDice);
            var playerOnlyMovement = JumpPlayerTransferPolicy.UsesPlayerOnlyMovement(isJumping, standingDice);
            var evaluateDiceCoupledMovement = JumpPlayerTransferPolicy.ShouldEvaluateDiceCoupledMovement(
                isJumping,
                standingDice);

            // L1 player-only transfer (Iron / Stone / iron-adjacent Magnet / sink-erasing dice / immovable on ground):
            // Resolve target surface at toCell first (top -> bottom -> floor).
            if (playerOnlyMovement
                && fromLevel != SurfaceHeightLevel.Floor
                && standingDice != null
                && TryResolveTargetSurfaceAtForPlayerOnlyJump(toCell, out var targetDice, out var targetLevel, out var targetSurfaceWorldY)) {
                if (JumpPlayerTransferPolicy.ShouldUseTierLandingPolicy(fromLevel, targetLevel)) {
                    return TierLandingPolicy.TryEvaluate(
                        fromCell,
                        toCell,
                        fromLevel,
                        fromSurface,
                        standingDice,
                        context,
                        registry,
                        reach,
                        out var tierLandingTransition)
                        ? tierLandingTransition
                        : MovementTransition.Blocked();
                }

                if (JumpPlayerTransferPolicy.BlocksGroundLowerLevelTransfer(
                    isJumping,
                    fromLevel,
                    targetLevel,
                    standingDice)) {
                    return MovementTransition.Blocked();
                }

                if (JumpPlayerTransferPolicy.IsLowerLevelTransfer(fromLevel, targetLevel)) {
                    // Player-only jump descent (roll-incapable): no step-height check.
                    if (JumpPlayerTransferPolicy.CanUsePlayerOnlyLowerLevelJump(isJumping, standingDice)) {
                        var descentRoute = targetLevel == SurfaceHeightLevel.Floor
                            ? MovementTransitionRoute.FloorTransfer
                            : MovementTransitionRoute.HeightTransfer;
                        return MovementTransition.Walkable(targetDice, targetLevel, descentRoute);
                    }

                    // Roll-capable player-only (Stone): jump descent is not allowed.
                    if (JumpPlayerTransferPolicy.BlocksPlayerOnlyJumpLowerLevelTransfer(
                        isJumping,
                        fromLevel,
                        targetLevel,
                        standingDice)) {
                        return MovementTransition.Blocked();
                    }

                    // Ground player-only descent (MaxWalkStep).
                    if (targetLevel == SurfaceHeightLevel.Floor) {
                        return WalkTransferPolicy.EvaluateFloor(
                            fromSurface,
                            standingDice,
                            registry,
                            reach,
                            allowDescentOnly: true);
                    }

                    if (!HeightReachPolicy.CanTransfer(
                        fromSurface,
                        targetSurfaceWorldY,
                        standingDice,
                        registry,
                        reach,
                        allowDescentOnly: true)) {
                        return MovementTransition.Blocked();
                    }

                    var lowerRoute = MovementTransitionRoute.HeightTransfer;
                    return MovementTransition.Walkable(targetDice, targetLevel, lowerRoute);
                }

                // Ascent / same-level player-only transfer (MaxJumpStepPlayerOnly when jumping).
                if (!HeightReachPolicy.CanTransfer(
                    fromSurface,
                    targetSurfaceWorldY,
                    standingDice,
                    registry,
                    reach,
                    allowDescentOnly: false)) {
                    return MovementTransition.Blocked();
                }

                return MovementTransition.Walkable(
                    targetDice,
                    targetLevel,
                    MovementTransitionRoute.HeightTransfer);
            }

            if (registry.CanPlaceBottomDiceAt(toCell)) {
                if (evaluateDiceCoupledMovement
                    && TryEvaluateDiceCoupledMovementOnEmptyCell(
                        fromCell,
                        toCell,
                        fromLevel,
                        fromSurface,
                        standingDice,
                        direction,
                        context,
                        out var emptyCellDiceTransition)) {
                    return emptyCellDiceTransition;
                }

                if (isJumping
                    && fromLevel != SurfaceHeightLevel.Floor
                    && standingDice != null
                    && standingDice.CanJumpCoupleWithPlayer) {
                    return MovementTransition.Blocked();
                }

                return WalkTransferPolicy.EvaluateFloor(
                    fromSurface,
                    standingDice,
                    registry,
                    reach,
                    allowDescentOnly: isJumping);
            }

            DiceController target;
            if (fromLevel == SurfaceHeightLevel.Floor) {
                if (registry.TryGetBottomAt(toCell, out target)
                    && WalkTransferPolicy.TryEvaluateFloorToBottom(
                        fromSurface,
                        target,
                        registry,
                        reach,
                        out var floorToBottomTransition)) {
                    return floorToBottomTransition;
                }

                return MovementTransition.Blocked();
            }

            if (TryEvaluateHeightTransfer(
                toCell,
                fromLevel,
                fromSurface,
                standingDice,
                direction,
                isJumping,
                context.AllowJumpGridMove,
                reach,
                out var heightTransferTransition)) {
                return heightTransferTransition;
            }

            if (evaluateDiceCoupledMovement
                && TryEvaluateDiceCoupledMovementOnOccupiedCell(
                    fromCell,
                    toCell,
                    fromLevel,
                    fromSurface,
                    standingDice,
                    direction,
                    context,
                    reach,
                    out var occupiedCellDiceTransition)) {
                return occupiedCellDiceTransition;
            }

            return MovementTransition.Blocked();
        }

        bool TryEvaluateDiceCoupledMovementOnEmptyCell(
            Vector2Int fromCell,
            Vector2Int toCell,
            int fromLevel,
            BoardSurface fromSurface,
            DiceController standingDice,
            Direction direction,
            PassabilityContext context,
            out MovementTransition transition) {
            transition = default;
            var isJumping = context.IsJumping;

            if (!isJumping
                && TryEvaluateIceSlide(
                    standingDice,
                    fromLevel,
                    direction,
                    out var iceSlidePlan,
                    out _)) {
                transition = MovementTransition.IceSlide(iceSlidePlan);
                return true;
            }

            if (isJumping
                && JumpGridRollPolicy.TryCreateCoupledTransition(
                    fromCell,
                    toCell,
                    fromSurface,
                    standingDice,
                    direction,
                    context,
                    gridPlanBuilder,
                    out var jumpDiceTransition)) {
                transition = jumpDiceTransition;
                return true;
            }

            if (standingDice.Capabilities.CanGridRoll
                && TopFallPolicy.TryEvaluate(
                    fromLevel,
                    fromSurface,
                    standingDice,
                    direction,
                    context,
                    gridPlanBuilder,
                    out var topFallTransition)) {
                transition = topFallTransition;
                return true;
            }

            if (!TryEvaluateGridRoll(
                fromCell,
                toCell,
                fromSurface,
                standingDice,
                direction,
                GetOrthogonalDistance(fromCell, toCell),
                allowMultiCell: false,
                context,
                out var gridPlan,
                out _)) {
                return false;
            }

            if (isJumping) {
                if (!context.AllowJumpGridMove) {
                    transition = MovementTransition.Blocked();
                    return true;
                }

                transition = CreateCoupledGridMoveTransition(standingDice, gridPlan);
                return true;
            }

            transition = MovementTransition.GridRoll(gridPlan);
            return true;
        }

        bool TryEvaluateDiceCoupledMovementOnOccupiedCell(
            Vector2Int fromCell,
            Vector2Int toCell,
            int fromLevel,
            BoardSurface fromSurface,
            DiceController standingDice,
            Direction direction,
            PassabilityContext context,
            HeightReachEvaluation reach,
            out MovementTransition transition) {
            transition = default;
            var isJumping = context.IsJumping;

            if (!isJumping
                && TryEvaluateIceSlide(
                    standingDice,
                    fromLevel,
                    direction,
                    out var iceSlidePlan,
                    out _)) {
                transition = MovementTransition.IceSlide(iceSlidePlan);
                return true;
            }

            if (isJumping
                && JumpGridRollPolicy.TryCreateCoupledTransition(
                    fromCell,
                    toCell,
                    fromSurface,
                    standingDice,
                    direction,
                    context,
                    gridPlanBuilder,
                    out var occupiedJumpDiceTransition)) {
                transition = occupiedJumpDiceTransition;
                return true;
            }

            if (TierLandingPolicy.TryEvaluate(
                fromCell,
                toCell,
                fromLevel,
                fromSurface,
                standingDice,
                context,
                registry,
                reach,
                out var jumpTopTransition)) {
                transition = jumpTopTransition;
                return true;
            }

            if (!TryEvaluateGridRoll(
                fromCell,
                toCell,
                fromSurface,
                standingDice,
                direction,
                GetOrthogonalDistance(fromCell, toCell),
                allowMultiCell: false,
                context,
                out var occupiedGridPlan,
                out _)) {
                return false;
            }

            if (isJumping) {
                if (!context.AllowJumpGridMove) {
                    transition = MovementTransition.Blocked();
                    return true;
                }

                transition = CreateCoupledGridMoveTransition(standingDice, occupiedGridPlan);
                return true;
            }

            transition = MovementTransition.GridRoll(occupiedGridPlan);
            return true;
        }

        bool TryResolveTargetSurfaceAtForPlayerOnlyJump(
            Vector2Int toCell,
            out DiceController targetDice,
            out int targetLevel,
            out float targetSurfaceWorldY) {
            targetDice = null;
            targetLevel = SurfaceHeightLevel.Floor;
            targetSurfaceWorldY = board.FloorSurfaceWorldY;

            if (registry.TryGetTopAt(toCell, out var top) && top != null) {
                targetDice = top;
                targetLevel = SurfaceHeightLevel.Top;
                targetSurfaceWorldY = top.GetLogicalTopSurfaceWorldY();
                return true;
            }

            if (registry.TryGetBottomAt(toCell, out var bottom) && bottom != null) {
                targetDice = bottom;
                targetLevel = SurfaceHeightLevel.Bottom;
                targetSurfaceWorldY = bottom.GetLogicalTopSurfaceWorldY();
                return true;
            }

            // Empty cell: floor
            return true;
        }

        bool TryResolveLowerLevelTargetAt(
            int fromLevel,
            Vector2Int toCell,
            out DiceController targetDice,
            out int targetLevel) {
            targetDice = null;
            targetLevel = SurfaceHeightLevel.Floor;

            if (JumpPlayerTransferPolicy.IsLowerLevelTransfer(fromLevel, SurfaceHeightLevel.Bottom)
                && registry.TryGetBottomAt(toCell, out var bottom)
                && bottom != null) {
                targetDice = bottom;
                targetLevel = SurfaceHeightLevel.Bottom;
                return true;
            }

            return false;
        }

        bool TryEvaluateIceSlide(
            DiceController standingDice,
            int fromLevel,
            Direction direction,
            out DiceSlidePlan plan,
            out string rejectReason) {
            plan = default;
            rejectReason = null;

            if (standingDice == null) {
                rejectReason = "no-standing-dice";
                return false;
            }

            if (!standingDice.Capabilities.SlideUntilBlocked) {
                rejectReason = "not-ice-dice";
                return false;
            }

            if (SurfaceHeightLevel.ToDiceStackTier(fromLevel) != standingDice.CurrentState.Tier) {
                rejectReason = "standing-tier-mismatch";
                return false;
            }

            if (!IronAdjacencyBlock.IsPlayerMovable(standingDice, registry)) {
                rejectReason = "dice-not-player-movable";
                return false;
            }

            return IceSlidePassability.TryBuildUntilBlocked(
                standingDice.CurrentState,
                direction,
                registry,
                out plan,
                out rejectReason);
        }

        bool TryEvaluateHeightTransfer(
            Vector2Int toCell,
            int fromLevel,
            BoardSurface fromSurface,
            DiceController standingDice,
            Direction direction,
            bool isJumping,
            bool allowJumpGridMove,
            HeightReachEvaluation reach,
            out MovementTransition transition) {
            transition = default;
            if (fromLevel == SurfaceHeightLevel.Floor || standingDice == null) {
                LogHeightTransfer(
                    $"reject skip-height-transfer to={FormatGrid(toCell)} " +
                    $"fromLevel={fromLevel} standingDice={(standingDice != null ? standingDice.name : "(none)")}");
                return false;
            }

            var fromCell = standingDice.CurrentState.GridPos;
            var sameTierTarget = registry.GetTransferTargetAt(standingDice, direction, fromLevel);

            // During coupled jump, prefer per-dice grid roll over same-tier height transfer.
            // Player-only dice skip this path because AllowJumpGridMove is false.
            var preferCoupledGridRoll = isJumping
                && allowJumpGridMove
                && standingDice.Capabilities.CanGridRoll
                && standingDice.CanJumpCoupleWithPlayer;

            string sameTierRejectReason = null;
            if (preferCoupledGridRoll) {
                sameTierRejectReason = "skipped-for-coupled-grid-roll";
                LogHeightTransfer(
                    $"skip same-tier-transfer for-coupled-grid-roll from={FormatGrid(fromCell)} " +
                    $"to={FormatGrid(toCell)} dir={direction} standing={FormatDice(standingDice)}");
            } else if (TryEvaluateHeightTransferToTarget(
                fromCell,
                toCell,
                fromLevel,
                fromSurface,
                standingDice,
                direction,
                isJumping,
                reach,
                sameTierTarget,
                out transition,
                out sameTierRejectReason)) {
                return true;
            }

            if (TryResolveLowerLevelTargetAt(fromLevel, toCell, out var lowerLevelTarget, out var lowerLevelTargetLevel)
                && lowerLevelTarget != sameTierTarget
                && (sameTierTarget == null || IsStepHeightRejectReason(sameTierRejectReason))) {
                if (fromSurface.IsSinkErasing
                    && !isJumping
                    && TryEvaluateHeightTransferToTarget(
                        fromCell,
                        toCell,
                        fromLevel,
                        fromSurface,
                        standingDice,
                        direction,
                        isJumping: false,
                        reach,
                        lowerLevelTarget,
                        dissolveDescentHoldOnly: true,
                        out transition,
                        out _)) {
                    return true;
                }

                // Player-only jump descent: no step-height check. The drop distance to the
                // lower level is irrelevant when jumping off a roll-incapable dice.
                if (JumpPlayerTransferPolicy.CanUsePlayerOnlyLowerLevelJump(isJumping, standingDice)) {
                    transition = MovementTransition.Walkable(
                        lowerLevelTarget,
                        lowerLevelTargetLevel,
                        MovementTransitionRoute.HeightTransfer);
                    return true;
                }
            }

            LogHeightTransfer(
                $"reject {sameTierRejectReason ?? "no-transfer-target"} from={FormatGrid(fromCell)} to={FormatGrid(toCell)} " +
                $"dir={direction} layer={fromLevel} " +
                $"standing={FormatDice(standingDice)} stack={FormatStack(toCell)} " +
                $"standingErasing={standingDice.IsErasing}");
            return false;
        }

        bool TryEvaluateHeightTransferToTarget(
            Vector2Int fromCell,
            Vector2Int toCell,
            int fromLevel,
            BoardSurface fromSurface,
            DiceController standingDice,
            Direction direction,
            bool isJumping,
            HeightReachEvaluation reach,
            DiceController target,
            out MovementTransition transition,
            out string rejectReason) {
            return TryEvaluateHeightTransferToTarget(
                fromCell,
                toCell,
                fromLevel,
                fromSurface,
                standingDice,
                direction,
                isJumping,
                reach,
                target,
                dissolveDescentHoldOnly: false,
                out transition,
                out rejectReason);
        }

        bool TryEvaluateHeightTransferToTarget(
            Vector2Int fromCell,
            Vector2Int toCell,
            int fromLevel,
            BoardSurface fromSurface,
            DiceController standingDice,
            Direction direction,
            bool isJumping,
            HeightReachEvaluation reach,
            DiceController target,
            bool dissolveDescentHoldOnly,
            out MovementTransition transition,
            out string rejectReason) {
            transition = default;
            rejectReason = null;
            if (target == null) {
                rejectReason = "no-transfer-target";
                return false;
            }

            if (target.CurrentState.GridPos != toCell) {
                rejectReason =
                    $"target-cell-mismatch target={FormatDice(target)} targetCell={FormatGrid(target.CurrentState.GridPos)}";
                return false;
            }

            if (JumpDiceTransferPolicy.ShouldBlockDiceToDiceTransfer(isJumping, standingDice, target)) {
                rejectReason = "jump-ice-dice-to-dice-transfer-blocked";
                return false;
            }

            var targetSurface = BoardSurface.FromDice(
                toCell,
                target.CurrentState.Tier == DiceStackTier.Top ? SurfaceHeightLevel.Top : SurfaceHeightLevel.Bottom,
                target);

            var allowDescentOnly = isJumping
                && targetSurface.SurfaceWorldY < fromSurface.SurfaceWorldY - 0.001f;

            var evaluated = dissolveDescentHoldOnly
                ? WalkTransferPolicy.TryEvaluateDissolveDescentHold(
                    target,
                    fromLevel,
                    registry,
                    fromSurface,
                    targetSurface,
                    standingDice,
                    reach,
                    out transition,
                    out rejectReason)
                : WalkTransferPolicy.TryEvaluateDiceToDice(
                    target,
                    fromLevel,
                    registry,
                    fromSurface,
                    targetSurface,
                    standingDice,
                    isJumping,
                    reach,
                    allowDescentOnly,
                    out transition,
                    out rejectReason);
            if (!evaluated) {
                return false;
            }

            var footingWorldY = TransferFootingPolicy.GetFootingWorldY(
                fromSurface,
                targetSurface.SurfaceWorldY,
                standingDice,
                registry);
            var resultKind = transition.Kind == MovementTransitionKind.BlockedStepOnly
                ? "dissolve-hold"
                : "ok";
            LogHeightTransfer(
                $"{resultKind} from={FormatGrid(fromCell)} to={FormatGrid(toCell)} dir={direction} " +
                $"layer={fromLevel} standing={FormatDice(standingDice)} " +
                $"target={FormatDice(target)} footingY={footingWorldY:F3} targetY={targetSurface.SurfaceWorldY:F3} " +
                $"standingErasing={standingDice.IsErasing} targetErasing={target.IsErasing}");
            return true;
        }

        static bool IsStepHeightRejectReason(string rejectReason) {
            return rejectReason != null && rejectReason.StartsWith("step-height");
        }

        static MovementTransition CreateCoupledGridMoveTransition(
            DiceController standingDice,
            DiceGridMovePlan plan) {
            var targetLevel = SurfaceHeightLevel.FromDiceStackTier(plan.To.Tier);
            return MovementTransition.WalkableWithGridPlan(
                standingDice,
                targetLevel,
                MovementTransitionRoute.CoupledGridMove,
                plan);
        }

        bool TryEvaluateGridRoll(
            Vector2Int fromCell,
            Vector2Int toCell,
            BoardSurface fromSurface,
            DiceController standingDice,
            Direction direction,
            int distance,
            bool allowMultiCell,
            PassabilityContext context,
            out string rejectReason) {
            return TryEvaluateGridRoll(
                fromCell,
                toCell,
                fromSurface,
                standingDice,
                direction,
                distance,
                allowMultiCell,
                context,
                out _,
                out rejectReason);
        }

        bool TryEvaluateGridRoll(
            Vector2Int fromCell,
            Vector2Int toCell,
            BoardSurface fromSurface,
            DiceController standingDice,
            Direction direction,
            int distance,
            bool allowMultiCell,
            PassabilityContext context,
            out DiceGridMovePlan plan,
            out string rejectReason) {
            plan = default;
            rejectReason = null;

            if (standingDice == null || !fromSurface.AllowsRoll) {
                rejectReason = "no-standing-dice-or-surface-cannot-roll";
                return false;
            }

            var standingTier = SurfaceHeightLevel.ToDiceStackTier(fromSurface.Level);
            if (standingTier != standingDice.CurrentState.Tier) {
                rejectReason =
                    $"standing-tier-mismatch standingTier={standingTier} diceTier={standingDice.CurrentState.Tier}";
                return false;
            }

            if (!standingDice.Capabilities.CanGridRoll) {
                rejectReason = "dice-cannot-grid-roll";
                return false;
            }

            if (!IronAdjacencyBlock.IsPlayerMovable(standingDice, registry)) {
                rejectReason = "dice-not-player-movable";
                return false;
            }

            if (distance < 1 || distance > DiceGridRollLimits.MaxParallelRollDistance) {
                rejectReason = $"distance-out-of-range distance={distance}";
                return false;
            }

            if (distance > 1 && !allowMultiCell) {
                rejectReason = "multi-cell-not-allowed";
                return false;
            }

            if (fromCell + direction.ToGridDelta() * distance != toCell) {
                rejectReason =
                    $"cell-mismatch from={FormatGrid(fromCell)} to={FormatGrid(toCell)} dir={direction} distance={distance}";
                return false;
            }

            return TryBuildGridMovePlan(
                standingDice.CurrentState,
                direction,
                distance,
                context,
                out plan,
                out rejectReason);
        }

        static string FormatGrid(Vector2Int grid) {
            return $"({grid.x},{grid.y})";
        }

        string FormatStack(Vector2Int gridPos) {
            registry.TryGetTopAt(gridPos, out var top);
            registry.TryGetBottomAt(gridPos, out var bottom);
            return $"Top={FormatDice(top)} Bottom={FormatDice(bottom)}";
        }

        static string FormatDice(DiceController dice) {
            return dice != null ? dice.name : "(none)";
        }
    }
}
