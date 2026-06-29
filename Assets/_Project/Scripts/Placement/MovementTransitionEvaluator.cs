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
        readonly float maxStepHeight;
        Action<string> jumpParallelRollDebugLog;

        public MovementTransitionEvaluator(
            Board board,
            DiceRegistry registry,
            SurfaceQuery surfaceQuery,
            float maxStepHeight) {
            this.board = board;
            this.registry = registry;
            this.surfaceQuery = surfaceQuery;
            occupancyQuery = new CellOccupancyQuery(board, registry);
            gridPlanBuilder = new GridMovePlanBuilder(registry, occupancyQuery);
            this.maxStepHeight = maxStepHeight;
        }

        public void SetJumpParallelRollDebugLog(Action<string> log) {
            jumpParallelRollDebugLog = log;
        }

        void LogJumpParallelRoll(string message) {
            jumpParallelRollDebugLog?.Invoke(message);
        }

        public MovementTransition Evaluate(
            Vector2Int fromCell,
            SurfaceLayer fromLayer,
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
                fromLayer,
                standingDice,
                standingTier,
                direction,
                context);
        }

        public float GetStackTopStandingSurfaceY(DiceController bottomDice) {
            return surfaceQuery.GetStackTopStandingSurfaceY(bottomDice);
        }

        public bool IsDescentBlockedOnlyByStepHeight(
            Vector2Int fromCell,
            SurfaceLayer fromLayer,
            Direction direction,
            float effectiveReachY,
            DiceController standingDice,
            DiceStackTier standingTier) {
            var transition = Evaluate(
                fromCell,
                fromLayer,
                direction,
                standingDice,
                standingTier,
                PassabilityContext.ForGround(effectiveReachY));
            return transition.IsDissolveDescentToFloor;
        }

        public bool IsWalkable(
            Vector2Int fromCell,
            SurfaceLayer fromLayer,
            Direction direction,
            float effectiveReachY,
            DiceController standingDice,
            DiceStackTier standingTier) {
            return Evaluate(
                fromCell,
                fromLayer,
                direction,
                standingDice,
                standingTier,
                PassabilityContext.ForGround(effectiveReachY)).Kind
                == MovementTransitionKind.Walkable;
        }

        public bool IsWalkableBetween(
            Vector2Int fromCell,
            Vector2Int toCell,
            SurfaceLayer fromLayer,
            float effectiveReachY,
            DiceController standingDice,
            DiceStackTier standingTier) {
            return TryEvaluateBetween(
                fromCell,
                toCell,
                fromLayer,
                effectiveReachY,
                standingDice,
                standingTier,
                out var transition)
                && transition.Kind == MovementTransitionKind.Walkable;
        }

        public bool TryEvaluateBetween(
            Vector2Int fromCell,
            Vector2Int toCell,
            SurfaceLayer fromLayer,
            float effectiveReachY,
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
                fromLayer,
                standingDice,
                standingTier,
                direction,
                PassabilityContext.ForGround(effectiveReachY));
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

            var fromLayer = standingTier == DiceStackTier.Top ? SurfaceLayer.Top : SurfaceLayer.Bottom;
            var fromSurface = surfaceQuery.GetStandingSurface(
                fromCell,
                fromLayer,
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
            SurfaceLayer fromLayer,
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
                fromLayer,
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
            SurfaceLayer fromLayer,
            DiceController standingDice,
            DiceStackTier standingTier,
            Direction direction,
            PassabilityContext context) {
            var isJumping = context.IsJumping;
            var reachY = context.EffectiveReachY;
            var fromSurface = surfaceQuery.GetStandingSurface(
                fromCell,
                fromLayer,
                standingDice,
                standingTier);

            if (registry.CanPlaceBottomDiceAt(toCell)) {
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
                    fromLayer,
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

                if (isJumping && fromLayer != SurfaceLayer.Floor && standingDice != null) {
                    return MovementTransition.Blocked();
                }

                return WalkTransferPolicy.EvaluateFloor(reachY, board.FloorSurfaceWorldY, maxStepHeight, fromSurface);
            }

            DiceController target;
            if (fromLayer == SurfaceLayer.Floor) {
                if (registry.TryGetBottomAt(toCell, out target)
                    && WalkTransferPolicy.TryEvaluateFloorToBottom(
                        reachY,
                        target,
                        maxStepHeight,
                        out var floorToBottomTransition)) {
                    return floorToBottomTransition;
                }

                return MovementTransition.Blocked();
            }

            if (TryEvaluateHeightTransfer(
                toCell,
                fromLayer,
                fromSurface,
                standingDice,
                standingTier,
                direction,
                isJumping,
                reachY,
                out var heightTransferTransition)) {
                return heightTransferTransition;
            }

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
                fromLayer,
                fromSurface,
                standingDice,
                standingTier,
                context,
                registry,
                maxStepHeight,
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

            return MovementTransition.Blocked();
        }

        bool TryEvaluateHeightTransfer(
            Vector2Int toCell,
            SurfaceLayer fromLayer,
            BoardSurface fromSurface,
            DiceController standingDice,
            DiceStackTier standingTier,
            Direction direction,
            bool isJumping,
            float reachY,
            out MovementTransition transition) {
            transition = default;
            if (fromLayer == SurfaceLayer.Floor || standingDice == null) {
                return false;
            }

            var target = registry.GetTransferTargetAt(standingDice, direction, standingTier);
            if (target == null || target.CurrentState.GridPos != toCell) {
                return false;
            }

            var targetSurface = BoardSurface.FromDice(
                toCell,
                target.CurrentState.Tier == DiceStackTier.Top ? SurfaceLayer.Top : SurfaceLayer.Bottom,
                target);
            return WalkTransferPolicy.TryEvaluateDiceToDice(
                reachY,
                target,
                standingTier,
                registry,
                fromSurface,
                targetSurface,
                isJumping,
                maxStepHeight,
                out transition);
        }

        static MovementTransition CreateCoupledGridMoveTransition(
            DiceController standingDice,
            DiceGridMovePlan plan) {
            var layer = plan.To.Tier == DiceStackTier.Top ? SurfaceLayer.Top : SurfaceLayer.Bottom;
            return MovementTransition.WalkableWithGridPlan(
                standingDice,
                layer,
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
