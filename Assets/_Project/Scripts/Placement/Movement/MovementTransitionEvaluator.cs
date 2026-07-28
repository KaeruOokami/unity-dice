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
        readonly DiceCoupledMoveEngine coupledMoveEngine;
        readonly MoveFactResolver factResolver;
        readonly MoveTransitionBuilder transitionBuilder;
        readonly HeightTransferEvaluator heightTransferEvaluator;
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
            coupledMoveEngine = new DiceCoupledMoveEngine(registry, gridPlanBuilder);
            factResolver = new MoveFactResolver(board, registry, surfaceQuery, coupledMoveEngine);
            heightTransferEvaluator = new HeightTransferEvaluator(registry, LogHeightTransfer);
            transitionBuilder = new MoveTransitionBuilder(registry, heightTransferEvaluator);
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
            var reach = CreateReachEvaluation(context.IsJumping, fromLevel, standingDice);
            var facts = factResolver.Resolve(
                fromCell,
                toCell,
                fromLevel,
                standingDice,
                direction,
                context,
                reach);
            var action = MoveActionSelector.Select(facts);
            return transitionBuilder.Build(action, facts);
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
            return coupledMoveEngine.TryEvaluateGridRoll(
                fromCell,
                toCell,
                fromSurface,
                standingDice,
                direction,
                distance,
                allowMultiCell,
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
