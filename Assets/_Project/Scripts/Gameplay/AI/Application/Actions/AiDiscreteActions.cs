using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Gameplay.AI.Application;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Application.Actions
{
    public enum MoveActionPurpose
    {
        NavigateToCell,
        StandOnDie,
        RollAdjacentDie
    }

    public sealed class MoveInDirectionAction : AiDiscreteAction
    {
        readonly Direction direction;
        readonly int maxFrames;
        readonly Vector2Int? targetCell;
        readonly MoveActionPurpose purpose;
        readonly DiceController standOnDie;
        int frameCount;
        bool startedMotion;

        public MoveInDirectionAction(
            Direction direction,
            int maxFrames,
            Vector2Int? targetCell = null,
            MoveActionPurpose purpose = MoveActionPurpose.NavigateToCell,
            DiceController standOnDie = null) {
            this.direction = direction;
            this.maxFrames = maxFrames;
            this.targetCell = targetCell;
            this.purpose = purpose;
            this.standOnDie = standOnDie;
        }

        public override void Begin(AiExecutionContext context) {
            frameCount = 0;
            startedMotion = false;

            AiDebugLog.Log(
                $"MoveStart direction={direction} purpose={purpose} target={FormatCell(targetCell)} " +
                $"standOn={(standOnDie != null ? standOnDie.name : "none")} maxFrames={maxFrames} " +
                $"playerCell={context.Character.StandingGridCell} " +
                $"currentDice={(context.Character.CurrentDice != null ? context.Character.CurrentDice.name : "none")}");
        }

        public override void Tick(AiExecutionContext context) {
            frameCount++;
            context.InputSource.SetMove(CharacterController.DirectionToMoveVector(direction));

            if (context.Character.IsBusy
                || (context.Registry != null && context.Registry.AnyRolling())) {
                startedMotion = true;
            }
        }

        public override bool IsComplete(AiExecutionContext context) {
            if (frameCount >= maxFrames) {
                context.InputSource.SetMove(Vector2.zero);
                LogComplete(context, "Timeout");
                return true;
            }

            if (purpose == MoveActionPurpose.StandOnDie) {
                if (standOnDie != null && context.Character.CurrentDice == standOnDie) {
                    if (IsSettled(context)) {
                        context.InputSource.SetMove(Vector2.zero);
                        LogComplete(context, "StandOnDie");
                        return true;
                    }

                    return false;
                }
            }

            if (purpose == MoveActionPurpose.NavigateToCell && targetCell.HasValue) {
                if (context.Character.StandingGridCell == targetCell.Value && IsSettled(context)) {
                    context.InputSource.SetMove(Vector2.zero);
                    LogComplete(context, "NavigateToCell");
                    return true;
                }
            }

            if (purpose == MoveActionPurpose.RollAdjacentDie) {
                if (startedMotion && IsSettled(context)) {
                    context.InputSource.SetMove(Vector2.zero);
                    LogComplete(context, "RollAdjacentDie");
                    return true;
                }
            }

            if (context.Character.IsBusy) {
                context.InputSource.SetMove(Vector2.zero);
                return false;
            }

            return false;
        }

        void LogComplete(AiExecutionContext context, string reason) {
            AiDebugLog.Log(
                $"MoveComplete reason={reason} direction={direction} purpose={purpose} frames={frameCount} " +
                $"playerCell={context.Character.StandingGridCell} target={FormatCell(targetCell)} " +
                $"currentDice={(context.Character.CurrentDice != null ? context.Character.CurrentDice.name : "none")} " +
                $"standOn={(standOnDie != null ? standOnDie.name : "none")} " +
                $"onTargetDie={standOnDie != null && context.Character.CurrentDice == standOnDie}");
        }

        static string FormatCell(Vector2Int? cell) {
            return cell.HasValue ? cell.Value.ToString() : "none";
        }

        static bool IsSettled(AiExecutionContext context) {
            return context.IsWorldIdle();
        }
    }

    public sealed class PulseLiftAction : AiDiscreteAction
    {
        readonly Direction faceDirection;
        int phase;
        int faceFrames;

        public PulseLiftAction(Direction faceDirection) {
            this.faceDirection = faceDirection;
        }

        public override void Begin(AiExecutionContext context) {
            phase = 0;
            faceFrames = context.Settings != null ? context.Settings.FaceBeforeLiftFrames : 4;
        }

        public override void Tick(AiExecutionContext context) {
            if (phase < faceFrames) {
                context.InputSource.SetMove(CharacterController.DirectionToMoveVector(faceDirection));
                phase++;
                return;
            }

            context.InputSource.SetMove(Vector2.zero);
            context.InputSource.PulseLift();
            phase++;
        }

        public override bool IsComplete(AiExecutionContext context) {
            if (phase <= faceFrames) {
                return false;
            }

            return context.Character.IsLiftCarrying || phase > faceFrames + 30;
        }
    }

    public sealed class PlaceCarriedDiceAction : AiDiscreteAction
    {
        readonly Direction placeDirection;
        bool pulsed;

        public PlaceCarriedDiceAction(Direction placeDirection) {
            this.placeDirection = placeDirection;
        }

        public override void Begin(AiExecutionContext context) {
            pulsed = false;
        }

        public override void Tick(AiExecutionContext context) {
            context.InputSource.SetMove(Vector2.zero);
            if (!pulsed) {
                context.InputSource.PulseDirection(placeDirection);
                pulsed = true;
            }
        }

        public override bool IsComplete(AiExecutionContext context) {
            return pulsed && !context.Character.IsCarrying && context.IsWorldIdle();
        }
    }

    public sealed class SameCellJumpAction : AiDiscreteAction
    {
        readonly DiceController targetDie;
        readonly int maxFrames;
        int frameCount;
        bool jumpPulsed;
        bool sawJumping;

        public SameCellJumpAction(DiceController targetDie, int maxFrames) {
            this.targetDie = targetDie;
            this.maxFrames = maxFrames;
        }

        public override void Begin(AiExecutionContext context) {
            frameCount = 0;
            jumpPulsed = false;
            sawJumping = false;

            AiDebugLog.Log(
                $"SameCellJumpStart die={(targetDie != null ? targetDie.name : "none")} " +
                $"cell={(targetDie != null ? targetDie.CurrentState.GridPos.ToString() : "none")} " +
                $"playerCell={context.Character.StandingGridCell} maxFrames={maxFrames}");
        }

        public override void Tick(AiExecutionContext context) {
            frameCount++;
            context.InputSource.SetMove(Vector2.zero);

            if (!jumpPulsed
                && context.IsWorldIdle()
                && !context.Character.IsJumping
                && !context.Character.IsBusy) {
                context.InputSource.PulseJump();
                jumpPulsed = true;
            }
        }

        public override bool IsComplete(AiExecutionContext context) {
            if (context.Character.IsJumping) {
                sawJumping = true;
            }

            if (IsMatchTriggered()) {
                context.InputSource.SetMove(Vector2.zero);
                LogComplete(context, "MatchErasing");
                return true;
            }

            if (jumpPulsed && !sawJumping && frameCount > 12) {
                context.InputSource.SetMove(Vector2.zero);
                LogComplete(context, "JumpNotStarted");
                return true;
            }

            if (jumpPulsed && sawJumping && !context.Character.IsJumping && context.IsWorldIdle()) {
                context.InputSource.SetMove(Vector2.zero);
                LogComplete(context, "JumpLanded");
                return true;
            }

            if (frameCount >= maxFrames) {
                context.InputSource.SetMove(Vector2.zero);
                LogComplete(context, "Timeout");
                return true;
            }

            return false;
        }

        bool IsMatchTriggered() {
            return targetDie != null
                && (targetDie.IsErasing || targetDie.IsVanishing || targetDie.IsSinkErasing);
        }

        void LogComplete(AiExecutionContext context, string reason) {
            AiDebugLog.Log(
                $"SameCellJumpComplete reason={reason} frames={frameCount} limit={maxFrames} " +
                $"sawJumping={sawJumping} playerCell={context.Character.StandingGridCell} " +
                $"die={(targetDie != null ? targetDie.name : "none")} " +
                $"erasing={targetDie != null && targetDie.IsErasing}");
        }
    }

    public sealed class JumpThenMoveAction : AiDiscreteAction
    {
        readonly Direction jumpDirection;
        readonly Vector2Int targetCell;
        readonly DiceController standOnDie;
        readonly int moveMaxFrames;
        int phase;
        int moveFrames;

        public JumpThenMoveAction(
            Direction jumpDirection,
            Vector2Int targetCell,
            int moveMaxFrames,
            DiceController standOnDie = null) {
            this.jumpDirection = jumpDirection;
            this.targetCell = targetCell;
            this.moveMaxFrames = moveMaxFrames;
            this.standOnDie = standOnDie;
        }

        public override void Begin(AiExecutionContext context) {
            phase = 0;
            moveFrames = 0;
        }

        public override void Tick(AiExecutionContext context) {
            if (phase == 0) {
                context.InputSource.SetMove(CharacterController.DirectionToMoveVector(jumpDirection));
                context.InputSource.PulseJump();
                phase = 1;
                return;
            }

            moveFrames++;
            context.InputSource.SetMove(CharacterController.DirectionToMoveVector(jumpDirection));
        }

        public override bool IsComplete(AiExecutionContext context) {
            if (phase == 0) {
                return false;
            }

            if (standOnDie != null && context.Character.CurrentDice == standOnDie && context.IsWorldIdle()) {
                context.InputSource.SetMove(Vector2.zero);
                return true;
            }

            if (context.Character.StandingGridCell == targetCell && context.IsWorldIdle()) {
                context.InputSource.SetMove(Vector2.zero);
                return true;
            }

            if (!context.Character.IsJumping && moveFrames > 3 && context.IsWorldIdle()) {
                context.InputSource.SetMove(Vector2.zero);
                return true;
            }

            if (moveFrames >= moveMaxFrames) {
                context.InputSource.SetMove(Vector2.zero);
                return true;
            }

            return false;
        }
    }

    public sealed class LiftSequenceAction : AiDiscreteAction
    {
        enum LiftSequencePhase
        {
            Face,
            Lift,
            WaitCarrying,
            Place
        }

        readonly Direction faceDirection;
        readonly Vector2Int placeCell;
        LiftSequencePhase phase;
        int frameCount;
        int faceFrames;
        bool placePulsed;

        public LiftSequenceAction(Direction faceDirection, Vector2Int placeCell) {
            this.faceDirection = faceDirection;
            this.placeCell = placeCell;
        }

        public override void Begin(AiExecutionContext context) {
            phase = LiftSequencePhase.Face;
            frameCount = 0;
            placePulsed = false;
            faceFrames = context.Settings != null ? context.Settings.FaceBeforeLiftFrames : 4;
        }

        public override void Tick(AiExecutionContext context) {
            switch (phase) {
                case LiftSequencePhase.Face:
                    context.InputSource.SetMove(CharacterController.DirectionToMoveVector(faceDirection));
                    frameCount++;
                    if (frameCount >= faceFrames) {
                        phase = LiftSequencePhase.Lift;
                        frameCount = 0;
                    }
                    break;
                case LiftSequencePhase.Lift:
                    context.InputSource.SetMove(Vector2.zero);
                    context.InputSource.PulseLift();
                    phase = LiftSequencePhase.WaitCarrying;
                    break;
                case LiftSequencePhase.WaitCarrying:
                    context.InputSource.SetMove(Vector2.zero);
                    if (context.Character.IsLiftCarrying) {
                        phase = LiftSequencePhase.Place;
                    }
                    break;
                case LiftSequencePhase.Place:
                    context.InputSource.SetMove(Vector2.zero);
                    if (!placePulsed) {
                        var delta = placeCell - context.Character.StandingGridCell;
                        Direction? placeDirection = ResolvePlaceDirection(delta);
                        if (placeDirection.HasValue) {
                            context.InputSource.PulseDirection(placeDirection.Value);
                        }

                        placePulsed = true;
                    }

                    frameCount++;
                    break;
            }
        }

        public override bool IsComplete(AiExecutionContext context) {
            if (phase == LiftSequencePhase.Place && !context.Character.IsCarrying && context.IsWorldIdle()) {
                return true;
            }

            return phase == LiftSequencePhase.Place && frameCount > 30;
        }

        static Direction? ResolvePlaceDirection(Vector2Int delta) {
            if (delta.x == 1 && delta.y == 0) {
                return Direction.East;
            }

            if (delta.x == -1 && delta.y == 0) {
                return Direction.West;
            }

            if (delta.x == 0 && delta.y == 1) {
                return Direction.North;
            }

            if (delta.x == 0 && delta.y == -1) {
                return Direction.South;
            }

            return null;
        }
    }

    public sealed class WaitUntilIdleAction : AiDiscreteAction
    {
        public override void Begin(AiExecutionContext context) {
            context.InputSource.SetMove(Vector2.zero);
        }

        public override void Tick(AiExecutionContext context) {
            context.InputSource.SetMove(Vector2.zero);
        }

        public override bool IsComplete(AiExecutionContext context) {
            return context.IsWorldIdle();
        }
    }
}
