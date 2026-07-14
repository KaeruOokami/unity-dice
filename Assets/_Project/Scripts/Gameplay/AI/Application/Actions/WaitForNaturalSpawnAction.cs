using DiceGame.Gameplay;
using DiceGame.Gameplay.AI.Domain;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Application.Actions
{
    public sealed class WaitForNaturalSpawnAction : AiDiscreteAction
    {
        readonly int maxFrames;
        readonly int excludedTrappedFace;
        int frameCount;

        public WaitForNaturalSpawnAction(int maxFrames, int excludedTrappedFace = 0) {
            this.maxFrames = maxFrames;
            this.excludedTrappedFace = excludedTrappedFace;
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

            if (HasRecoveryTarget(context)) {
                AiDebugLog.Log($"WaitForSpawnComplete reason=target-detected frames={frameCount}");
                return true;
            }

            return false;
        }

        bool HasRecoveryTarget(AiExecutionContext context) {
            if (context?.Registry == null || context.Character == null) {
                return false;
            }

            var snapshot = GameStateSnapshot.Capture(context.Character, context.Registry);
            if (AiFloorRecoveryPlanner.TryFindNaturalSpawnTarget(snapshot, context.Registry, out _)) {
                return true;
            }

            if (context.Settings == null) {
                return false;
            }

            return AiFloorRecoveryPlanner.TrySelectAlternateSinkingTarget(
                snapshot,
                excludedTrappedFace,
                context.Settings,
                out _);
        }
    }
}
