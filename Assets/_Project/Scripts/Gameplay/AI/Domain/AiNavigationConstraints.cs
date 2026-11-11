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

            // Sink-specific preference: skip remount onto ghost sinks in the protected cluster.
            // Solid sinks and other occupied dice use the normal HeightTransfer / JumpRoll paths.
            if (transition.Kind == MovementTransitionKind.Walkable) {
                return !IsProtectedClusterGhostRemount(ProtectedCluster, transition.TargetDice);
            }

            return !WouldMoveProtectedClusterDice(ProtectedCluster, transition, fromState);
        }

        static bool IsProtectedClusterGhostRemount(
            IReadOnlyList<DiceSnapshot> protectedCluster,
            DiceController landingDice) {
            if (landingDice == null || !landingDice.IsErasureGhost) {
                return false;
            }

            return ClusterSelectionEvaluator.ClusterContainsController(protectedCluster, landingDice);
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
