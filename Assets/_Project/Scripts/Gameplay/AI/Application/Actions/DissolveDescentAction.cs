using DiceGame.Core;
using DiceGame.Gameplay.AI.Domain;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Application.Actions
{
    public sealed class DissolveDescentAction : AiDiscreteAction
    {
        readonly Direction direction;
        readonly Vector2Int stepCell;
        readonly int maxFrames;
        int frameCount;

        public DissolveDescentAction(Direction direction, Vector2Int stepCell, int maxFrames) {
            this.direction = direction;
            this.stepCell = stepCell;
            this.maxFrames = maxFrames;
        }

        public override void Begin(AiExecutionContext context) {
            frameCount = 0;
            AiDebugLog.Log(
                $"DissolveDescentStart direction={direction} step={stepCell} maxFrames={maxFrames} " +
                $"playerCell={context.Character.StandingGridCell} " +
                $"standing={(context.Character.CurrentDice != null ? context.Character.CurrentDice.name : "floor")}");
        }

        public override void Tick(AiExecutionContext context) {
            frameCount++;
            context.InputSource.SetMove(CharacterController.DirectionToMoveVector(direction));
        }

        public override bool IsComplete(AiExecutionContext context) {
            if (HasDescended(context)) {
                context.InputSource.SetMove(Vector2.zero);
                AiDebugLog.Log(
                    $"DissolveDescentComplete reason=descended frames={frameCount} " +
                    $"playerCell={context.Character.StandingGridCell} onFloor={context.Character.IsOnFloor}");
                return true;
            }

            if (frameCount >= maxFrames) {
                context.InputSource.SetMove(Vector2.zero);
                AiDebugLog.Log(
                    $"DissolveDescentComplete reason=timeout frames={frameCount} " +
                    $"playerCell={context.Character.StandingGridCell}");
                return true;
            }

            return false;
        }

        static bool HasDescended(AiExecutionContext context) {
            if (!context.IsWorldIdle()) {
                return false;
            }

            var standing = context.Character.CurrentDice;
            if (standing == null) {
                return context.Character.IsOnFloor;
            }

            return !standing.IsSinkErasing;
        }
    }
}
