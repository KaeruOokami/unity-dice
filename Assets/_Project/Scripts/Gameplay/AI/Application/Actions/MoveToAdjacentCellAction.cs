using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Gameplay.AI.Application;
using DiceGame.Gameplay.AI.Domain;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Application.Actions
{
    /// <summary>
    /// Executes one orthogonal cell step: hold direction until nextCell center is reached.
    /// Planning uses cell pathfinding; FaceSlide and other in-cell motion are handled by gameplay.
    /// </summary>
    public sealed class MoveToAdjacentCellAction : AiDiscreteAction
    {
        readonly Vector2Int nextCell;
        readonly Vector2Int goalCell;
        readonly int maxFrames;
        readonly MoveActionPurpose purpose;
        readonly DiceController standOnDie;
        readonly MovementTransitionKind edgeKind;
        int frameCount;
        Vector2Int startCell;
        Direction direction;
        bool sawBusy;

        public MoveToAdjacentCellAction(
            Vector2Int nextCell,
            Vector2Int goalCell,
            int maxFrames,
            MoveActionPurpose purpose = MoveActionPurpose.NavigateToCell,
            DiceController standOnDie = null,
            MovementTransitionKind edgeKind = MovementTransitionKind.Walkable) {
            this.nextCell = nextCell;
            this.goalCell = goalCell;
            this.maxFrames = maxFrames;
            this.purpose = purpose;
            this.standOnDie = standOnDie;
            this.edgeKind = edgeKind;
        }

        public override void Begin(AiExecutionContext context) {
            ClearFailure();
            frameCount = 0;
            sawBusy = false;
            startCell = context.Character.StandingGridCell;

            if (!AiCellMoveEvaluator.TryGetDirectionBetweenCells(startCell, nextCell, out direction)) {
                direction = DiceBoardAnalyzer.GetPrimaryDirectionToward(startCell, nextCell) ?? Direction.East;
            }

            AiDebugLog.Log(
                $"CellStepStart from={startCell} step={nextCell} goal={goalCell} direction={direction} " +
                $"edge={edgeKind} purpose={purpose} standOn={(standOnDie != null ? standOnDie.name : "none")} maxFrames={maxFrames} " +
                $"currentDice={(context.Character.CurrentDice != null ? context.Character.CurrentDice.name : "none")}");
        }

        public override void Tick(AiExecutionContext context) {
            if (context.Character.IsBusy) {
                sawBusy = true;
            } else {
                frameCount++;
            }

            var shouldHoldDirection = !IsGroundRollEdge() || !sawBusy;
            context.InputSource.SetMove(
                shouldHoldDirection
                    ? CharacterController.DirectionToMoveVector(direction)
                    : Vector2.zero);
        }

        bool IsGroundRollEdge() {
            return edgeKind == MovementTransitionKind.CanRoll
                || (purpose == MoveActionPurpose.RollWorkDie
                    && edgeKind == MovementTransitionKind.Walkable);
        }

        public override bool IsComplete(AiExecutionContext context) {
            if (TryCompleteSuccess(context, out var reason)) {
                context.InputSource.SetMove(Vector2.zero);
                LogComplete(context, reason);
                return true;
            }

            var frameLimit = GetFrameLimit(context);
            if (frameCount >= frameLimit) {
                context.InputSource.SetMove(Vector2.zero);
                MarkFailed();
                LogComplete(context, "Timeout");
                return true;
            }

            return false;
        }

        int GetFrameLimit(AiExecutionContext context) {
            if (sawBusy && context.Settings != null) {
                return Mathf.Max(maxFrames, context.Settings.RollStepMaxFrames);
            }

            return maxFrames;
        }

        float GetCenterTolerance(AiExecutionContext context) {
            return context.Settings != null ? context.Settings.CellCenterTolerance : 0.08f;
        }

        bool TryCompleteSuccess(AiExecutionContext context, out string reason) {
            reason = null;
            var tolerance = GetCenterTolerance(context);

            if (!context.IsWorldIdle()) {
                return false;
            }

            var atStepCell = context.Character.StandingGridCell == nextCell;
            var atStepCenter = atStepCell && context.Character.IsNearCellCenter(nextCell, tolerance);
            var rollStepSettled = IsGroundRollEdge() && atStepCell;

            if (purpose == MoveActionPurpose.StandOnDie) {
                // Mount only: cell arrival while on another die is not success.
                // Center snap is not required once mounted on the target die.
                if (standOnDie != null
                    && context.Character.CurrentDice == standOnDie
                    && atStepCell) {
                    reason = "StandOnDie";
                    return true;
                }

                return false;
            }

            if (purpose == MoveActionPurpose.RollWorkDie
                && standOnDie != null
                && context.Character.CurrentDice == standOnDie
                && rollStepSettled) {
                reason = "RollWorkDie";
                return true;
            }

            if (rollStepSettled || atStepCenter) {
                reason = "ReachedStepCell";
                return true;
            }

            if (edgeKind == MovementTransitionKind.BlockedStepOnly
                && purpose == MoveActionPurpose.NavigateToCell
                && context.Character.IsOnFloor
                && context.IsWorldIdle()) {
                reason = "DissolveDescentReachedFloor";
                return true;
            }

            return false;
        }

        void LogComplete(AiExecutionContext context, string reason) {
            var centerDistance = context.Character.GetDistanceToCellCenter(nextCell);
            AiDebugLog.Log(
                $"CellStepComplete reason={reason} direction={direction} edge={edgeKind} from={startCell} step={nextCell} " +
                $"goal={goalCell} frames={frameCount} limit={GetFrameLimit(context)} sawBusy={sawBusy} " +
                $"playerCell={context.Character.StandingGridCell} centerDist={centerDistance:F3} " +
                $"currentDice={(context.Character.CurrentDice != null ? context.Character.CurrentDice.name : "none")} " +
                $"standOn={(standOnDie != null ? standOnDie.name : "none")} " +
                $"onTargetDie={standOnDie != null && context.Character.CurrentDice == standOnDie}");
        }
    }
}
