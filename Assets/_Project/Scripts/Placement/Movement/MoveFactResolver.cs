using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Placement
{
    /// <summary>
    /// Resolves <see cref="MoveFacts"/> once per cell evaluation.
    /// Capability probes fill boolean/plan fields; they do not choose the action.
    /// </summary>
    public sealed class MoveFactResolver
    {
        readonly Board board;
        readonly DiceRegistry registry;
        readonly SurfaceQuery surfaceQuery;
        readonly DiceCoupledMoveEngine coupledMoveEngine;

        public MoveFactResolver(
            Board board,
            DiceRegistry registry,
            SurfaceQuery surfaceQuery,
            DiceCoupledMoveEngine coupledMoveEngine) {
            this.board = board;
            this.registry = registry;
            this.surfaceQuery = surfaceQuery;
            this.coupledMoveEngine = coupledMoveEngine;
        }

        public MoveFacts Resolve(
            Vector2Int fromCell,
            Vector2Int toCell,
            int fromLevel,
            DiceController standingDice,
            Direction direction,
            PassabilityContext context,
            HeightReachEvaluation reach) {
            var isJumping = context.IsJumping;
            var mode = JumpPlayerTransferPolicy.ResolveStandingMoveMode(isJumping, standingDice);
            var fromSurface = surfaceQuery.GetStandingSurface(fromCell, fromLevel, standingDice);

            MovementTransition expandedTransition = default;
            var hasExpanded = fromLevel != SurfaceHeightLevel.Floor
                && standingDice != null
                && ExpandedFootprintWalkPolicy.TryEvaluateParallelWalk(
                    fromCell,
                    toCell,
                    fromLevel,
                    standingDice,
                    registry,
                    out expandedTransition);

            PlayerSupportQuery.ResolveAt(
                toCell,
                registry,
                board.FloorSurfaceWorldY,
                out var targetDice,
                out var targetLevel,
                out var targetSurfaceWorldY,
                includePendingBottom: true);

            var relation = ResolveRelation(fromLevel, targetLevel);
            var withinReachFull = HeightReachPolicy.CanTransfer(
                fromSurface,
                targetSurfaceWorldY,
                standingDice,
                registry,
                reach,
                allowDescentOnly: false,
                targetDice);
            var withinReachDescentOnly = HeightReachPolicy.CanTransfer(
                fromSurface,
                targetSurfaceWorldY,
                standingDice,
                registry,
                reach,
                allowDescentOnly: true,
                targetDice);

            var blocksCoupled = GhostPlacementRules.BlocksDiceCoupledStackEntry(registry, toCell);
            var floorPassable = GhostPlacementRules.IsPlayerFloorPassable(registry, toCell);
            var canPlaceBottom = registry.CanPlaceBottomDiceAt(toCell);

            DiceController floorMountBottom = null;
            if (fromLevel == SurfaceHeightLevel.Floor && !floorPassable) {
                registry.TryGetBottomIncludingPending(toCell, out floorMountBottom);
            }

            var hasIceSlide = false;
            DiceSlidePlan icePlan = default;
            DiceController iceElastic = null;
            var canJumpGrid = false;
            MovementTransition jumpGrid = default;
            var canTopFall = false;
            MovementTransition topFall = default;
            var canTierLand = false;
            MovementTransition tierLand = default;
            var canGridRoll = false;
            DiceGridMovePlan gridPlan = default;

            var needsCoupledProbes = !hasExpanded
                && (mode == DiceStandingMoveMode.Slide || mode == DiceStandingMoveMode.Roll)
                && !blocksCoupled;
            var needsPlayerOnlyTier = !hasExpanded
                && mode == DiceStandingMoveMode.PlayerOnly
                && fromLevel != SurfaceHeightLevel.Floor
                && standingDice != null
                && relation == MoveLevelRelation.BottomToTop;

            if (needsCoupledProbes || needsPlayerOnlyTier) {
                if (needsCoupledProbes && mode == DiceStandingMoveMode.Slide && !isJumping) {
                    hasIceSlide = coupledMoveEngine.TryProbeIceSlide(
                        standingDice,
                        fromLevel,
                        direction,
                        out icePlan,
                        out iceElastic);
                }

                if (needsCoupledProbes && isJumping) {
                    canJumpGrid = JumpGridRollPolicy.TryCreateCoupledTransition(
                        fromCell,
                        toCell,
                        fromSurface,
                        standingDice,
                        direction,
                        context,
                        coupledMoveEngine.PlanBuilder,
                        out jumpGrid);
                }

                if (needsCoupledProbes && mode == DiceStandingMoveMode.Roll && canPlaceBottom) {
                    canTopFall = TopFallPolicy.TryEvaluate(
                        fromLevel,
                        fromSurface,
                        standingDice,
                        direction,
                        context,
                        coupledMoveEngine.PlanBuilder,
                        out topFall);
                }

                // Tier landing: occupied coupled path, or player-only Bottom→Top.
                if ((needsCoupledProbes && !canPlaceBottom) || needsPlayerOnlyTier) {
                    canTierLand = TierLandingPolicy.TryEvaluate(
                        fromCell,
                        toCell,
                        fromLevel,
                        fromSurface,
                        standingDice,
                        context,
                        registry,
                        reach,
                        out tierLand);
                }

                if (needsCoupledProbes && mode == DiceStandingMoveMode.Roll) {
                    canGridRoll = coupledMoveEngine.TryEvaluateGridRoll(
                        fromCell,
                        toCell,
                        fromSurface,
                        standingDice,
                        direction,
                        MovementTransitionEvaluator.GetOrthogonalDistance(fromCell, toCell),
                        allowMultiCell: false,
                        context,
                        out gridPlan,
                        out _);
                }
            }

            return new MoveFacts(
                fromCell,
                toCell,
                fromLevel,
                standingDice,
                fromSurface,
                direction,
                context,
                reach,
                isJumping,
                mode,
                targetDice,
                targetLevel,
                targetSurfaceWorldY,
                relation,
                withinReachFull,
                withinReachDescentOnly,
                hasExpanded,
                expandedTransition,
                blocksCoupled,
                floorPassable,
                canPlaceBottom,
                floorMountBottom,
                hasIceSlide,
                icePlan,
                iceElastic,
                canJumpGrid,
                jumpGrid,
                canTopFall,
                topFall,
                canTierLand,
                tierLand,
                canGridRoll,
                gridPlan);
        }

        static MoveLevelRelation ResolveRelation(int fromLevel, int targetLevel) {
            if (fromLevel == SurfaceHeightLevel.Bottom && targetLevel == SurfaceHeightLevel.Top) {
                return MoveLevelRelation.BottomToTop;
            }

            if (targetLevel < fromLevel) {
                return MoveLevelRelation.Descent;
            }

            if (targetLevel > fromLevel) {
                return MoveLevelRelation.Ascent;
            }

            return MoveLevelRelation.Same;
        }
    }
}
