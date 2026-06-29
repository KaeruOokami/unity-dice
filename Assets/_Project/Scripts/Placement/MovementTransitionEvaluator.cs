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

        public bool TryBuildJumpGridMovePlan(
            DiceState fromState,
            Direction direction,
            int distance,
            PassabilityContext context,
            out DiceGridMovePlan plan,
            out string rejectReason) {
            plan = default;
            rejectReason = null;

            var hasTopOnSameCell = registry.HasTopAt(fromState.GridPos);
            if (!JumpGridPassability.TryEvaluate(
                occupancyQuery,
                fromState,
                direction,
                distance,
                hasTopOnSameCell,
                context,
                out var landingTier,
                out var moveKind,
                out rejectReason)) {
                return false;
            }

            return DiceGridMovePlanner.TryBuildPlan(
                fromState,
                direction,
                distance,
                landingTier,
                moveKind,
                out plan,
                out rejectReason);
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

            if (requiredDistance < 1 || requiredDistance > RollResolver.MaxParallelRollDistance) {
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
                    && TryCreateJumpDiceMoveTransition(
                        fromCell,
                        toCell,
                        fromSurface,
                        standingDice,
                        standingTier,
                        direction,
                        context,
                        out var jumpDiceTransition)) {
                    return jumpDiceTransition;
                }

                if (TryEvaluateTopFallToBottom(
                    fromLayer,
                    fromSurface,
                    standingDice,
                    standingTier,
                    direction,
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
                    out _)) {
                    if (isJumping && !context.AllowJumpGridMove) {
                        return MovementTransition.Blocked();
                    }

                    if (TryCreateJumpSameTierRollTransition(
                        isJumping,
                        fromSurface,
                        standingDice,
                        out var jumpRollTransition)) {
                        return jumpRollTransition;
                    }

                    return MovementTransition.Roll();
                }

                if (isJumping && fromLayer != SurfaceLayer.Floor && standingDice != null) {
                    return MovementTransition.Blocked();
                }

                return EvaluateFloorTransition(reachY, fromSurface);
            }

            DiceController target;
            if (fromLayer == SurfaceLayer.Floor) {
                if (registry.TryGetBottomAt(toCell, out target)) {
                    if (!CanStepBetween(reachY, GetLogicalTopSurfaceY(target))) {
                        return MovementTransition.Blocked();
                    }

                    return MovementTransition.Walkable(
                        target,
                        SurfaceLayer.Bottom,
                        MovementTransitionRoute.HeightTransfer);
                }

                return MovementTransition.Blocked();
            }

            if (isJumping
                && TryCreateJumpDiceMoveTransition(
                    fromCell,
                    toCell,
                    fromSurface,
                    standingDice,
                    standingTier,
                    direction,
                    context,
                    out var occupiedJumpDiceTransition)) {
                return occupiedJumpDiceTransition;
            }

            if (TryEvaluateJumpTopLanding(
                fromCell,
                toCell,
                fromLayer,
                fromSurface,
                standingDice,
                standingTier,
                context,
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
                out _)) {
                if (isJumping && !context.AllowJumpGridMove) {
                    return MovementTransition.Blocked();
                }

                if (TryCreateJumpSameTierRollTransition(
                    isJumping,
                    fromSurface,
                    standingDice,
                    out var jumpRollTransition)) {
                    return jumpRollTransition;
                }

                return MovementTransition.Roll();
            }

            target = registry.GetTransferTargetAt(standingDice, direction, standingTier);
            if (target == null) {
                return MovementTransition.Blocked();
            }

            var targetSurface = BoardSurface.FromDice(
                toCell,
                target.CurrentState.Tier == DiceStackTier.Top ? SurfaceLayer.Top : SurfaceLayer.Bottom,
                target);
            if (!targetSurface.AllowsWalkFrom(fromSurface, isJumping)) {
                return MovementTransition.Blocked();
            }

            if (standingTier == DiceStackTier.Bottom
                && target.CurrentState.Tier == DiceStackTier.Bottom
                && registry.HasTopAt(toCell)) {
                return MovementTransition.Blocked();
            }

            if (!CanStepBetween(reachY, GetLogicalTopSurfaceY(target))) {
                return MovementTransition.Blocked();
            }

            var targetLayer = target.CurrentState.Tier == DiceStackTier.Top
                ? SurfaceLayer.Top
                : SurfaceLayer.Bottom;
            return MovementTransition.Walkable(
                target,
                targetLayer,
                MovementTransitionRoute.HeightTransfer);
        }

        static float GetLogicalTopSurfaceY(DiceController dice) {
            return dice != null ? dice.GetLogicalTopSurfaceWorldY() : 0f;
        }

        MovementTransition EvaluateFloorTransition(float effectiveReachY, BoardSurface fromSurface) {
            var floorY = board.FloorSurfaceWorldY;
            if (CanStepBetween(effectiveReachY, floorY)) {
                return MovementTransition.Walkable(
                    null,
                    SurfaceLayer.Floor,
                    MovementTransitionRoute.FloorTransfer);
            }

            if (fromSurface.IsDissolving && fromSurface.Layer == SurfaceLayer.Bottom) {
                return MovementTransition.BlockedStepOnly(null, SurfaceLayer.Floor);
            }

            return MovementTransition.Blocked();
        }

        bool TryEvaluateTopFallToBottom(
            SurfaceLayer fromLayer,
            BoardSurface fromSurface,
            DiceController standingDice,
            DiceStackTier standingTier,
            Direction direction,
            out MovementTransition transition) {
            transition = default;

            if (fromLayer != SurfaceLayer.Top
                || standingTier != DiceStackTier.Top
                || standingDice == null
                || !fromSurface.AllowsRoll
                || standingDice.CurrentState.Tier != DiceStackTier.Top) {
                return false;
            }

            if (!SlideResolver.TrySlideTop(
                standingDice.CurrentState,
                direction,
                registry,
                out _,
                out var result)
                || result != TopSlideResult.FallToBottom) {
                return false;
            }

            transition = MovementTransition.Walkable(
                standingDice,
                SurfaceLayer.Bottom,
                MovementTransitionRoute.TopFall);
            return true;
        }

        bool TryEvaluateJumpTopLanding(
            Vector2Int fromCell,
            Vector2Int toCell,
            SurfaceLayer fromLayer,
            BoardSurface fromSurface,
            DiceController standingDice,
            DiceStackTier standingTier,
            PassabilityContext context,
            out MovementTransition transition) {
            transition = default;

            if (GetOrthogonalDistance(fromCell, toCell) != 1) {
                return false;
            }

            if (!context.IsJumping
                || fromLayer != SurfaceLayer.Bottom
                || standingTier != DiceStackTier.Bottom
                || standingDice == null
                || !fromSurface.AllowsRoll) {
                return false;
            }

            if (!context.AllowJumpTierChange) {
                return false;
            }

            var reachY = context.EffectiveReachY;

            if (registry.TryGetTopAt(toCell, out var topDice)
                && topDice != null
                && CanStepBetween(reachY, GetLogicalTopSurfaceY(topDice))) {
                transition = MovementTransition.Walkable(
                    topDice,
                    SurfaceLayer.Top,
                    MovementTransitionRoute.TierLanding);
                return true;
            }

            if (!registry.TryGetBottomAt(toCell, out var bottomDice)
                || bottomDice == null
                || !registry.CanPlaceTopDiceAt(toCell)) {
                return false;
            }

            var topSurfaceY = GetStackTopStandingSurfaceY(bottomDice);
            if (!CanStepBetween(reachY, topSurfaceY)) {
                return false;
            }

            transition = MovementTransition.Walkable(
                standingDice,
                SurfaceLayer.Top,
                MovementTransitionRoute.TierLanding);
            return true;
        }

        float GetTargetSurfaceWorldY(MovementTransition transition) {
            if (transition.TargetLayer == SurfaceLayer.Floor) {
                return board.FloorSurfaceWorldY;
            }

            if (transition.TargetDice == null) {
                return board.FloorSurfaceWorldY;
            }

            if (transition.TargetLayer == SurfaceLayer.Top
                && transition.TargetDice.CurrentState.Tier == DiceStackTier.Bottom) {
                return GetStackTopStandingSurfaceY(transition.TargetDice);
            }

            if (transition.TargetLayer == SurfaceLayer.Bottom
                && transition.TargetDice.CurrentState.Tier == DiceStackTier.Top) {
                return board.FloorSurfaceWorldY;
            }

            return transition.TargetDice.GetLogicalTopSurfaceWorldY();
        }

        bool TryCreateJumpDiceMoveTransition(
            Vector2Int fromCell,
            Vector2Int toCell,
            BoardSurface fromSurface,
            DiceController standingDice,
            DiceStackTier standingTier,
            Direction direction,
            PassabilityContext context,
            out MovementTransition transition) {
            transition = default;

            if (standingDice == null || !fromSurface.AllowsRoll) {
                return false;
            }

            if (standingTier != standingDice.CurrentState.Tier) {
                return false;
            }

            var distance = GetOrthogonalDistance(fromCell, toCell);
            if (distance < 1 || distance > RollResolver.MaxParallelRollDistance) {
                return false;
            }

            if (fromCell + direction.ToGridDelta() * distance != toCell) {
                return false;
            }

            if (!TryBuildJumpGridMovePlan(
                standingDice.CurrentState,
                direction,
                distance,
                context,
                out var plan,
                out _)) {
                return false;
            }

            switch (plan.Kind) {
                case DiceGridMoveKind.Parallel:
                    return TryCreateJumpSameTierRollTransition(true, fromSurface, standingDice, out transition);
                case DiceGridMoveKind.Demote:
                    transition = MovementTransition.Walkable(
                        standingDice,
                        SurfaceLayer.Bottom,
                        MovementTransitionRoute.CoupledGridMove);
                    return true;
                case DiceGridMoveKind.Stack:
                    transition = MovementTransition.Walkable(
                        standingDice,
                        SurfaceLayer.Top,
                        MovementTransitionRoute.CoupledGridMove);
                    return true;
                default:
                    return false;
            }
        }

        static bool TryCreateJumpSameTierRollTransition(
            bool isJumping,
            BoardSurface fromSurface,
            DiceController standingDice,
            out MovementTransition transition) {
            transition = default;

            if (!isJumping || standingDice == null || !fromSurface.AllowsRoll) {
                return false;
            }

            var tier = standingDice.CurrentState.Tier;
            var layer = tier == DiceStackTier.Top ? SurfaceLayer.Top : SurfaceLayer.Bottom;
            transition = MovementTransition.Walkable(
                standingDice,
                layer,
                MovementTransitionRoute.CoupledGridMove);
            return true;
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
            PassabilityContext context) {
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
                out _);
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

            if (distance < 1 || distance > RollResolver.MaxParallelRollDistance) {
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

            var hasTopOnSameCell = registry.HasTopAt(fromCell);
            if (allowMultiCell) {
                return JumpGridPassability.TryEvaluate(
                    occupancyQuery,
                    standingDice.CurrentState,
                    direction,
                    distance,
                    hasTopOnSameCell,
                    context,
                    out _,
                    out _,
                    out rejectReason);
            }

            if (!RollResolver.TryRollDistance(
                standingDice.CurrentState,
                direction,
                registry,
                hasTopOnSameCell,
                distance,
                out _,
                out var rollReject)) {
                rejectReason = rollReject ?? "roll-resolver-failed";
                return false;
            }

            return true;
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

        bool CanStepBetween(float fromSurfaceY, float toSurfaceY) {
            return Mathf.Abs(fromSurfaceY - toSurfaceY) <= maxStepHeight;
        }
    }
}
