using System.Collections.Generic;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Placement;

namespace DiceGame.Gameplay.AI.Domain
{
    public readonly struct AiNavigationConstraints
    {
        public IReadOnlyList<DiceSnapshot> ProtectedCluster { get; }

        AiNavigationConstraints(IReadOnlyList<DiceSnapshot> protectedCluster) {
            ProtectedCluster = protectedCluster;
        }

        public static AiNavigationConstraints None => new AiNavigationConstraints(null);

        public static AiNavigationConstraints ForClusterProtection(IReadOnlyList<DiceSnapshot> cluster) {
            if (cluster == null || cluster.Count == 0) {
                return None;
            }

            return new AiNavigationConstraints(cluster);
        }

        public bool IsTransitionAllowed(MovementTransition transition, AiNavigationState fromState) {
            if (ProtectedCluster == null || ProtectedCluster.Count == 0) {
                return true;
            }

            if (transition.Kind == MovementTransitionKind.Walkable) {
                return true;
            }

            return !WouldMoveProtectedClusterDice(ProtectedCluster, transition, fromState);
        }

        static bool WouldMoveProtectedClusterDice(
            IReadOnlyList<DiceSnapshot> protectedCluster,
            MovementTransition transition,
            AiNavigationState fromState) {
            if (transition.Kind != MovementTransitionKind.CanRoll
                && transition.Kind != MovementTransitionKind.IceSlide) {
                return false;
            }

            var standingDice = fromState.StandingDice;
            if (standingDice == null) {
                return false;
            }

            return ClusterSelectionEvaluator.ClusterContainsController(
                protectedCluster,
                standingDice);
        }
    }
}
