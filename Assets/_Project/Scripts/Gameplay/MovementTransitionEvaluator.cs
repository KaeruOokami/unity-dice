using DiceGame.Core;
using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Gameplay
{
    public enum SurfaceLayer {
        Floor,
        Bottom,
        Top
    }

    public enum MovementTransitionKind {
        Walkable,
        Blocked,
        CanRoll
    }

    public readonly struct MovementTransition {
        public MovementTransitionKind Kind { get; }
        public DiceController TargetDice { get; }
        public SurfaceLayer TargetLayer { get; }

        MovementTransition(
            MovementTransitionKind kind,
            DiceController targetDice,
            SurfaceLayer targetLayer) {
            Kind = kind;
            TargetDice = targetDice;
            TargetLayer = targetLayer;
        }

        public static MovementTransition Walkable(DiceController dice, SurfaceLayer layer) {
            return new MovementTransition(MovementTransitionKind.Walkable, dice, layer);
        }

        public static MovementTransition Blocked() {
            return new MovementTransition(MovementTransitionKind.Blocked, null, SurfaceLayer.Floor);
        }

        public static MovementTransition Roll() {
            return new MovementTransition(MovementTransitionKind.CanRoll, null, SurfaceLayer.Floor);
        }
    }

    public class MovementTransitionEvaluator {
        readonly Board board;
        readonly DiceRegistry registry;
        readonly float maxStepHeight;

        public MovementTransitionEvaluator(Board board, DiceRegistry registry, float maxStepHeight) {
            this.board = board;
            this.registry = registry;
            this.maxStepHeight = maxStepHeight;
        }

        public MovementTransition Evaluate(
            Vector2Int fromCell,
            SurfaceLayer fromLayer,
            Direction direction,
            float fromSurfaceY,
            DiceController standingDice,
            DiceStackTier standingTier,
            bool ignoreStepHeight = false,
            bool isJumping = false) {
            var toCell = fromCell + direction.ToGridDelta();
            if (!board.IsInside(toCell) || board.GetCell(toCell) == CellType.Wall) {
                return MovementTransition.Blocked();
            }

            return EvaluateToCell(
                fromCell,
                toCell,
                fromLayer,
                fromSurfaceY,
                standingDice,
                standingTier,
                direction,
                ignoreStepHeight,
                isJumping);
        }

        public float GetStackTopStandingSurfaceY(DiceController bottomDice) {
            if (bottomDice == null) {
                return board.FloorSurfaceWorldY;
            }

            if (registry.TryGetTopAt(bottomDice.CurrentState.GridPos, out var top) && top != null) {
                return top.GetTopSurfaceWorldY();
            }

            return bottomDice.GetTopSurfaceWorldY() + board.CellSize;
        }

        public bool IsDescentBlockedOnlyByStepHeight(
            Vector2Int fromCell,
            SurfaceLayer fromLayer,
            Direction direction,
            float fromSurfaceY,
            DiceController standingDice,
            DiceStackTier standingTier) {
            var blocked = Evaluate(
                fromCell,
                fromLayer,
                direction,
                fromSurfaceY,
                standingDice,
                standingTier);
            if (blocked.Kind != MovementTransitionKind.Blocked) {
                return false;
            }

            var bypass = Evaluate(
                fromCell,
                fromLayer,
                direction,
                fromSurfaceY,
                standingDice,
                standingTier,
                ignoreStepHeight: true);
            if (bypass.Kind != MovementTransitionKind.Walkable) {
                return false;
            }

            return GetTargetSurfaceWorldY(bypass) < fromSurfaceY;
        }

        public bool IsWalkable(
            Vector2Int fromCell,
            SurfaceLayer fromLayer,
            Direction direction,
            float fromSurfaceY,
            DiceController standingDice,
            DiceStackTier standingTier) {
            return Evaluate(fromCell, fromLayer, direction, fromSurfaceY, standingDice, standingTier).Kind
                == MovementTransitionKind.Walkable;
        }

        public bool IsWalkableBetween(
            Vector2Int fromCell,
            Vector2Int toCell,
            SurfaceLayer fromLayer,
            float fromSurfaceY,
            DiceController standingDice,
            DiceStackTier standingTier) {
            return TryEvaluateBetween(
                fromCell,
                toCell,
                fromLayer,
                fromSurfaceY,
                standingDice,
                standingTier,
                out var transition)
                && transition.Kind == MovementTransitionKind.Walkable;
        }

        public bool TryEvaluateBetween(
            Vector2Int fromCell,
            Vector2Int toCell,
            SurfaceLayer fromLayer,
            float fromSurfaceY,
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
                fromSurfaceY,
                standingDice,
                standingTier,
                direction,
                ignoreStepHeight: false,
                isJumping: false);
            return true;
        }

        public static bool IsOrthogonalAdjacent(Vector2Int fromCell, Vector2Int toCell) {
            var delta = toCell - fromCell;
            return (Mathf.Abs(delta.x) + Mathf.Abs(delta.y)) == 1;
        }

        public static bool TryGetDirectionBetween(
            Vector2Int fromCell,
            Vector2Int toCell,
            out Direction direction) {
            direction = default;
            var delta = toCell - fromCell;
            if (delta == Vector2Int.right) {
                direction = Direction.East;
                return true;
            }

            if (delta == Vector2Int.left) {
                direction = Direction.West;
                return true;
            }

            if (delta == new Vector2Int(0, 1)) {
                direction = Direction.North;
                return true;
            }

            if (delta == new Vector2Int(0, -1)) {
                direction = Direction.South;
                return true;
            }

            return false;
        }

        MovementTransition EvaluateToCell(
            Vector2Int fromCell,
            Vector2Int toCell,
            SurfaceLayer fromLayer,
            float fromSurfaceY,
            DiceController standingDice,
            DiceStackTier standingTier,
            Direction direction,
            bool ignoreStepHeight,
            bool isJumping) {
            if (registry.CanPlaceBottomDiceAt(toCell)) {
                if (TryEvaluateTopFallToBottom(
                    fromLayer,
                    fromSurfaceY,
                    standingDice,
                    standingTier,
                    direction,
                    ignoreStepHeight,
                    out var topFallTransition)) {
                    return topFallTransition;
                }

                if (TryEvaluateGridRoll(fromCell, toCell, standingDice, standingTier, direction)) {
                    if (TryCreateJumpSameTierRollTransition(
                        isJumping,
                        standingDice,
                        out var jumpRollTransition)) {
                        return jumpRollTransition;
                    }

                    return MovementTransition.Roll();
                }

                return EvaluateFloorTransition(fromSurfaceY, ignoreStepHeight);
            }

            DiceController target;
            if (fromLayer == SurfaceLayer.Floor) {
                if (registry.TryGetBottomAt(toCell, out target)) {
                    if (!ignoreStepHeight
                        && !CanStepBetween(fromSurfaceY, target.GetTopSurfaceWorldY())) {
                        return MovementTransition.Blocked();
                    }

                    return MovementTransition.Walkable(target, SurfaceLayer.Bottom);
                }

                return MovementTransition.Blocked();
            }

            if (TryEvaluateJumpTopLanding(
                toCell,
                fromLayer,
                fromSurfaceY,
                standingDice,
                standingTier,
                ignoreStepHeight,
                isJumping,
                out var jumpTopTransition)) {
                return jumpTopTransition;
            }

            if (TryEvaluateGridRoll(fromCell, toCell, standingDice, standingTier, direction)) {
                if (TryCreateJumpSameTierRollTransition(
                    isJumping,
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

            if (target.IsDissolving && !standingDice.IsDissolving && !isJumping) {
                return MovementTransition.Blocked();
            }

            if (standingTier == DiceStackTier.Bottom
                && target.CurrentState.Tier == DiceStackTier.Bottom
                && registry.HasTopAt(toCell)) {
                return MovementTransition.Blocked();
            }

            if (!ignoreStepHeight && !CanStepBetween(fromSurfaceY, target.GetTopSurfaceWorldY())) {
                return MovementTransition.Blocked();
            }

            var targetLayer = target.CurrentState.Tier == DiceStackTier.Top
                ? SurfaceLayer.Top
                : SurfaceLayer.Bottom;
            return MovementTransition.Walkable(target, targetLayer);
        }

        MovementTransition EvaluateFloorTransition(float fromSurfaceY, bool ignoreStepHeight) {
            if (ignoreStepHeight || CanStepBetween(fromSurfaceY, board.FloorSurfaceWorldY)) {
                return MovementTransition.Walkable(null, SurfaceLayer.Floor);
            }

            return MovementTransition.Blocked();
        }

        bool TryEvaluateTopFallToBottom(
            SurfaceLayer fromLayer,
            float fromSurfaceY,
            DiceController standingDice,
            DiceStackTier standingTier,
            Direction direction,
            bool ignoreStepHeight,
            out MovementTransition transition) {
            transition = default;

            if (fromLayer != SurfaceLayer.Top
                || standingTier != DiceStackTier.Top
                || standingDice == null
                || standingDice.IsDissolving
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

            transition = MovementTransition.Walkable(standingDice, SurfaceLayer.Bottom);
            return true;
        }

        bool TryEvaluateJumpTopLanding(
            Vector2Int toCell,
            SurfaceLayer fromLayer,
            float fromSurfaceY,
            DiceController standingDice,
            DiceStackTier standingTier,
            bool ignoreStepHeight,
            bool isJumping,
            out MovementTransition transition) {
            transition = default;

            if (!isJumping
                || fromLayer != SurfaceLayer.Bottom
                || standingTier != DiceStackTier.Bottom
                || standingDice == null
                || standingDice.IsDissolving) {
                return false;
            }

            if (registry.TryGetTopAt(toCell, out var topDice)
                && topDice != null
                && (ignoreStepHeight || CanStepBetween(fromSurfaceY, topDice.GetTopSurfaceWorldY()))) {
                transition = MovementTransition.Walkable(topDice, SurfaceLayer.Top);
                return true;
            }

            if (!registry.TryGetBottomAt(toCell, out var bottomDice)
                || bottomDice == null
                || !registry.CanPlaceTopDiceAt(toCell)) {
                return false;
            }

            var topSurfaceY = GetStackTopStandingSurfaceY(bottomDice);
            if (!ignoreStepHeight && !CanStepBetween(fromSurfaceY, topSurfaceY)) {
                return false;
            }

            transition = MovementTransition.Walkable(standingDice, SurfaceLayer.Top);
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

            return transition.TargetDice.GetTopSurfaceWorldY();
        }

        static bool TryCreateJumpSameTierRollTransition(
            bool isJumping,
            DiceController standingDice,
            out MovementTransition transition) {
            transition = default;

            if (!isJumping || standingDice == null || standingDice.IsDissolving) {
                return false;
            }

            var tier = standingDice.CurrentState.Tier;
            var layer = tier == DiceStackTier.Top ? SurfaceLayer.Top : SurfaceLayer.Bottom;
            transition = MovementTransition.Walkable(standingDice, layer);
            return true;
        }

        bool TryEvaluateGridRoll(
            Vector2Int fromCell,
            Vector2Int toCell,
            DiceController standingDice,
            DiceStackTier standingTier,
            Direction direction) {
            if (standingDice == null || standingDice.IsDissolving) {
                return false;
            }

            if (standingTier != standingDice.CurrentState.Tier) {
                return false;
            }

            if (fromCell + direction.ToGridDelta() != toCell) {
                return false;
            }

            var hasTopOnSameCell = registry.HasTopAt(fromCell);
            return RollResolver.TryRoll(
                standingDice.CurrentState,
                direction,
                registry,
                hasTopOnSameCell,
                out _);
        }

        bool CanStepBetween(float fromSurfaceY, float toSurfaceY) {
            return Mathf.Abs(fromSurfaceY - toSurfaceY) <= maxStepHeight;
        }
    }
}
