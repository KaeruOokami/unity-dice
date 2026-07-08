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

        HeightReachEvaluation CreateReachEvaluation(bool isJumping) {
            return new HeightReachEvaluation(
                board.FloorSurfaceWorldY,
                board.CellSize,
                stepLimits,
                isJumping);
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
            DiceStackTier standingTier,
            PassabilityContext context) {
            var toCell = fromCell + direction.ToGridDelta();
            if (!board.IsInside(toCell) || board.GetCell(toCell) == CellType.Wall) {
                return MovementTransition.Blocked();
            }

            return EvaluateToCell(
                fromCell,
                toCell,
                fromLevel,
                standingDice,
                standingTier,
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
            DiceStackTier standingTier,
            PassabilityContext context,
            out MovementTransition transition) {
            transition = default;
            if (!context.IsJumping || standingDice == null) {
                return false;
            }

            var fromSurface = surfaceQuery.GetStandingSurface(
                fromCell,
                fromLevel,
                standingDice,
                standingTier);
            return PlayerOnlyTierDemotePolicy.TryEvaluate(
                fromCell,
                fromLevel,
                fromSurface,
                standingDice,
                standingTier,
                context.IsJumping,
                registry,
                CreateReachEvaluation(context.IsJumping),
                out transition);
        }

        public bool IsDescentBlockedOnlyByStepHeight(
            Vector2Int fromCell,
            int fromLevel,
            Direction direction,
            float footingWorldY,
            DiceController standingDice,
            DiceStackTier standingTier) {
            var transition = Evaluate(
                fromCell,
                fromLevel,
                direction,
                standingDice,
                standingTier,
                PassabilityContext.ForGround(footingWorldY));
            return transition.IsDissolveDescentHold;
        }

        public bool IsWalkable(
            Vector2Int fromCell,
            int fromLevel,
            Direction direction,
            float footingWorldY,
            DiceController standingDice,
            DiceStackTier standingTier) {
            return Evaluate(
                fromCell,
                fromLevel,
                direction,
                standingDice,
                standingTier,
                PassabilityContext.ForGround(footingWorldY)).Kind
                == MovementTransitionKind.Walkable;
        }

        public bool IsWalkableBetween(
            Vector2Int fromCell,
            Vector2Int toCell,
            int fromLevel,
            float footingWorldY,
            DiceController standingDice,
            DiceStackTier standingTier) {
            return TryEvaluateBetween(
                fromCell,
                toCell,
                fromLevel,
                footingWorldY,
                standingDice,
                standingTier,
                out var transition)
                && transition.Kind == MovementTransitionKind.Walkable;
        }

        public bool TryEvaluateBetween(
            Vector2Int fromCell,
            Vector2Int toCell,
            int fromLevel,
            float footingWorldY,
            DiceController standingDice,
            DiceStackTier standingTier,
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
                standingTier,
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
            DiceStackTier standingTier,
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

            var fromLevel = standingTier == DiceStackTier.Top ? SurfaceHeightLevel.Top : SurfaceHeightLevel.Bottom;
            var fromSurface = surfaceQuery.GetStandingSurface(
                fromCell,
                fromLevel,
                standingDice,
                standingTier);

            if (!TryEvaluateGridRoll(
                fromCell,
                candidate,
                fromSurface,
                standingDice,
                standingTier,
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
            DiceStackTier standingTier,
            PassabilityContext context) {
            if (!board.IsInside(toCell) || board.GetCell(toCell) == CellType.Wall) {
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
                standingTier,
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
            DiceStackTier standingTier,
            Direction direction,
            PassabilityContext context) {
            var isJumping = context.IsJumping;
            var reach = CreateReachEvaluation(isJumping);
            var fromSurface = surfaceQuery.GetStandingSurface(
                fromCell,
                fromLevel,
                standingDice,
                standingTier);
            var playerOnlyTransfer = JumpPlayerTransferPolicy.UsesPlayerOnlyReach(isJumping, standingDice);

            // Player-only jump transfer (Iron/Stone/iron-adjacent Magnet):
            // Resolve target surface at toCell first (top -> bottom -> floor),
            // and evaluate transfer uniformly regardless of whether toCell is empty or occupied.
            if (playerOnlyTransfer
                && fromLevel != SurfaceHeightLevel.Floor
                && standingDice != null
                && TryResolveTargetSurfaceAtForPlayerOnlyJump(toCell, out var targetDice, out var targetLevel, out var targetSurfaceWorldY)) {
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

                var route = targetLevel == SurfaceHeightLevel.Floor
                    ? MovementTransitionRoute.FloorTransfer
                    : MovementTransitionRoute.HeightTransfer;
                return MovementTransition.Walkable(targetDice, targetLevel, route);
            }

            if (registry.CanPlaceBottomDiceAt(toCell)) {
                if (!playerOnlyTransfer) {
                if (isJumping
                    && JumpGridRollPolicy.TryCreateCoupledTransition(
                        fromCell,
                        toCell,
                        fromSurface,
                        standingDice,
                        standingTier,
                        direction,
                        context,
                        gridPlanBuilder,
                        out var jumpDiceTransition)) {
                    return jumpDiceTransition;
                }

                if (TopFallPolicy.TryEvaluate(
                    fromLevel,
                    fromSurface,
                    standingDice,
                    standingTier,
                    direction,
                    context,
                    gridPlanBuilder,
                    out var topFallTransition)) {
                    return topFallTransition;
                }

                if (TryEvaluateGridRoll(
                    fromCell,
                    toCell,
                    fromSurface,
                    standingDice,
                    standingTier,
                    direction,
                    GetOrthogonalDistance(fromCell, toCell),
                    allowMultiCell: false,
                    context,
                    out var gridPlan,
                    out _)) {
                    if (isJumping) {
                        if (!context.AllowJumpGridMove) {
                            return MovementTransition.Blocked();
                        }

                        return CreateCoupledGridMoveTransition(standingDice, gridPlan);
                    }

                    return MovementTransition.GridRoll(gridPlan);
                }

                if (!isJumping
                    && TryEvaluateIceSlide(
                        standingDice,
                        standingTier,
                        direction,
                        out var iceSlidePlan,
                        out _)) {
                    return MovementTransition.IceSlide(iceSlidePlan);
                }

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
                standingTier,
                direction,
                isJumping,
                context.AllowJumpGridMove,
                reach,
                out var heightTransferTransition)) {
                return heightTransferTransition;
            }

            if (!playerOnlyTransfer) {
            if (isJumping
                && JumpGridRollPolicy.TryCreateCoupledTransition(
                    fromCell,
                    toCell,
                    fromSurface,
                    standingDice,
                    standingTier,
                    direction,
                    context,
                    gridPlanBuilder,
                    out var occupiedJumpDiceTransition)) {
                return occupiedJumpDiceTransition;
            }

            if (TierLandingPolicy.TryEvaluate(
                fromCell,
                toCell,
                fromLevel,
                fromSurface,
                standingDice,
                standingTier,
                context,
                registry,
                reach,
                out var jumpTopTransition)) {
                return jumpTopTransition;
            }

            if (TryEvaluateGridRoll(
                fromCell,
                toCell,
                fromSurface,
                standingDice,
                standingTier,
                direction,
                GetOrthogonalDistance(fromCell, toCell),
                allowMultiCell: false,
                context,
                out var occupiedGridPlan,
                out _)) {
                if (isJumping) {
                    if (!context.AllowJumpGridMove) {
                        return MovementTransition.Blocked();
                    }

                    return CreateCoupledGridMoveTransition(standingDice, occupiedGridPlan);
                }

                return MovementTransition.GridRoll(occupiedGridPlan);
            }

            }

            if (!isJumping
                && TryEvaluateIceSlide(
                    standingDice,
                    standingTier,
                    direction,
                    out var occupiedIceSlidePlan,
                    out _)) {
                return MovementTransition.IceSlide(occupiedIceSlidePlan);
            }

            return MovementTransition.Blocked();
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

        bool TryEvaluateIceSlide(
            DiceController standingDice,
            DiceStackTier standingTier,
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

            if (standingTier != standingDice.CurrentState.Tier) {
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
            DiceStackTier standingTier,
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
            var sameTierTarget = registry.GetTransferTargetAt(standingDice, direction, standingTier);

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
                standingTier,
                direction,
                isJumping,
                reach,
                sameTierTarget,
                out transition,
                out sameTierRejectReason)) {
                return true;
            }

            if (fromSurface.IsDissolving
                && standingTier == DiceStackTier.Top
                && !registry.HasTopAt(toCell)
                && registry.TryGetBottomAt(toCell, out var lowerTierTarget)
                && lowerTierTarget != null
                && lowerTierTarget != sameTierTarget
                && (sameTierTarget == null || IsStepHeightRejectReason(sameTierRejectReason))
                && TryEvaluateHeightTransferToTarget(
                    fromCell,
                    toCell,
                    fromLevel,
                    fromSurface,
                    standingDice,
                    standingTier,
                    direction,
                    isJumping: false,
                    reach,
                    lowerTierTarget,
                    dissolveDescentHoldOnly: true,
                    out transition,
                    out _)) {
                return true;
            }

            if (JumpPlayerTransferPolicy.UsesPlayerOnlyReach(isJumping, standingDice)
                && WalkTransferPolicy.IsLandingTierAtOrBelowStandingTier(standingTier, DiceStackTier.Bottom)
                && (standingTier == DiceStackTier.Top || fromLevel == SurfaceHeightLevel.Top)
                && registry.TryGetBottomAt(toCell, out var playerOnlyLowerTarget)
                && playerOnlyLowerTarget != null
                && playerOnlyLowerTarget != sameTierTarget
                && (sameTierTarget == null || IsStepHeightRejectReason(sameTierRejectReason))
                && TryEvaluateHeightTransferToTarget(
                    fromCell,
                    toCell,
                    fromLevel,
                    fromSurface,
                    standingDice,
                    standingTier,
                    direction,
                    isJumping,
                    reach,
                    playerOnlyLowerTarget,
                    out transition,
                    out _)) {
                return true;
            }

            LogHeightTransfer(
                $"reject {sameTierRejectReason ?? "no-transfer-target"} from={FormatGrid(fromCell)} to={FormatGrid(toCell)} " +
                $"dir={direction} layer={fromLevel} tier={standingTier} " +
                $"standing={FormatDice(standingDice)} stack={FormatStack(toCell)} " +
                $"standingDissolving={standingDice.IsDissolving}");
            return false;
        }

        bool TryEvaluateHeightTransferToTarget(
            Vector2Int fromCell,
            Vector2Int toCell,
            int fromLevel,
            BoardSurface fromSurface,
            DiceController standingDice,
            DiceStackTier standingTier,
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
                standingTier,
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
            DiceStackTier standingTier,
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

            var targetSurface = BoardSurface.FromDice(
                toCell,
                target.CurrentState.Tier == DiceStackTier.Top ? SurfaceHeightLevel.Top : SurfaceHeightLevel.Bottom,
                target);

            var allowDescentOnly = isJumping
                && targetSurface.SurfaceWorldY < fromSurface.SurfaceWorldY - 0.001f;

            var evaluated = dissolveDescentHoldOnly
                ? WalkTransferPolicy.TryEvaluateDissolveDescentHold(
                    target,
                    standingTier,
                    registry,
                    fromSurface,
                    targetSurface,
                    standingDice,
                    reach,
                    out transition,
                    out rejectReason)
                : WalkTransferPolicy.TryEvaluateDiceToDice(
                    target,
                    standingTier,
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
                $"layer={fromLevel} tier={standingTier} standing={FormatDice(standingDice)} " +
                $"target={FormatDice(target)} footingY={footingWorldY:F3} targetY={targetSurface.SurfaceWorldY:F3} " +
                $"standingDissolving={standingDice.IsDissolving} targetDissolving={target.IsDissolving}");
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
            DiceStackTier standingTier,
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
                standingTier,
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
            DiceStackTier standingTier,
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
