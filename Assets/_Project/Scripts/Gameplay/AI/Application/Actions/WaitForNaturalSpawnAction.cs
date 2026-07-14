using DiceGame.Gameplay;
using DiceGame.Gameplay.AI.Domain;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Application.Actions
{
    public sealed class WaitForNaturalSpawnAction : AiDiscreteAction
    {
        readonly int maxFrames;
        int frameCount;

        public WaitForNaturalSpawnAction(int maxFrames) {
            this.maxFrames = maxFrames;
        }

        public override void Begin(AiExecutionContext context) {
            frameCount = 0;
            AiDebugLog.Log(
                $"WaitForSpawnStart maxFrames={maxFrames} playerCell={context.Character.StandingGridCell}");
        }

        public override void Tick(AiExecutionContext context) {
            frameCount++;
            context.InputSource.SetMove(Vector2.zero);
        }

        public override bool IsComplete(AiExecutionContext context) {
            if (frameCount >= maxFrames) {
                AiDebugLog.Log($"WaitForSpawnComplete reason=timeout frames={frameCount}");
                return true;
            }

            if (TryFindSpawnTarget(context, out _)) {
                AiDebugLog.Log($"WaitForSpawnComplete reason=spawn-detected frames={frameCount}");
                return true;
            }

            return false;
        }

        public static bool TryFindSpawnTarget(AiExecutionContext context, out DiceController spawnDie) {
            spawnDie = null;
            if (context?.Registry == null || context.Character == null) {
                return false;
            }

            var snapshot = GameStateSnapshot.Capture(context.Character, context.Registry);
            return AiFloorRecoveryPlanner.TryFindNaturalSpawnTarget(
                snapshot,
                context.Registry,
                out spawnDie);
        }
    }
}
