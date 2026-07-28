using DiceGame.Core;
using DiceGame.Gameplay;
using UnityEngine;

namespace DiceGame.Placement
{
    /// <summary>
    /// Sub-pipeline for <see cref="MoveAction.HeightTransfer"/>.
    /// </summary>
    public sealed class HeightTransferEvaluator
    {
        readonly HeightTransferFactResolver factResolver;
        readonly HeightTransferBuilder builder;

        public HeightTransferEvaluator(DiceRegistry registry, System.Action<string> log = null) {
            factResolver = new HeightTransferFactResolver(registry);
            builder = new HeightTransferBuilder(log);
        }

        public MovementTransition Evaluate(
            Vector2Int fromCell,
            Vector2Int toCell,
            int fromLevel,
            BoardSurface fromSurface,
            DiceController standingDice,
            Direction direction,
            bool isJumping,
            bool allowJumpGridMove,
            HeightReachEvaluation reach) {
            var facts = factResolver.Resolve(
                fromCell,
                toCell,
                fromLevel,
                fromSurface,
                standingDice,
                direction,
                isJumping,
                allowJumpGridMove,
                reach);
            var action = HeightTransferActionSelector.Select(facts);
            return builder.Build(action, facts);
        }
    }
}
