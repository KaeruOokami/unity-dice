using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Placement;

namespace DiceGame.Gameplay.AI.Domain
{
    /// <summary>
    /// Navigation inputs for filtering external work dice by stand-on reachability.
    /// </summary>
    public readonly struct AiWorkDieReachContext
    {
        public MovementTransitionEvaluator Passability { get; }
        public AiNavigationState Start { get; }
        public float FootingWorldY { get; }
        public PlayerSlot MovementOwner { get; }
        public int MaxSearchSteps { get; }
        public bool AllowJump { get; }
        public AiNavigationConstraints Constraints { get; }

        public bool IsValid => Passability != null;

        public AiWorkDieReachContext(
            MovementTransitionEvaluator passability,
            AiNavigationState start,
            float footingWorldY,
            PlayerSlot movementOwner,
            int maxSearchSteps,
            bool allowJump,
            AiNavigationConstraints constraints) {
            Passability = passability;
            Start = start;
            FootingWorldY = footingWorldY;
            MovementOwner = movementOwner;
            MaxSearchSteps = maxSearchSteps;
            AllowJump = allowJump;
            Constraints = constraints;
        }

        public static bool TryCreate(
            CharacterController character,
            AiPlayerSettings settings,
            AiNavigationConstraints constraints,
            out AiWorkDieReachContext context) {
            context = default;
            if (character == null
                || !character.TryGetAiNavigationQuery(out var passability, out var footingWorldY)) {
                return false;
            }

            var maxSteps = settings != null ? settings.PathSearchMaxSteps : 64;
            var allowJump = settings == null || settings.AllowJump;
            context = new AiWorkDieReachContext(
                passability,
                character.GetAiNavigationState(),
                footingWorldY,
                character.PlayerSlot,
                maxSteps,
                allowJump,
                constraints);
            return true;
        }
    }

    /// <summary>
    /// Shared ReachWorkDie navigation constraints for selection and execution.
    /// </summary>
    public static class AiReachExternalWorkDieNavigation
    {
        /// <summary>
        /// When leaving a protected cluster to mount an external work die, forbid rolling
        /// cluster dice unless the player is stranded on that cluster (including sink footing)
        /// with no adjacent external mount.
        /// </summary>
        public static AiNavigationConstraints ResolveConstraints(
            GameStateSnapshot snapshot,
            IReadOnlyList<DiceSnapshot> cluster,
            int face) {
            if (snapshot == null || cluster == null || cluster.Count == 0) {
                return AiNavigationConstraints.None;
            }

            var strandedOnCluster = ClusterSelectionEvaluator.IsStandingOnCluster(snapshot, cluster)
                && snapshot.StandingDice != null
                && !AiFloorRecoveryPlanner.HasAdjacentClusterExternalDie(
                    cluster,
                    face,
                    snapshot.PlanningDice);

            return strandedOnCluster
                ? AiNavigationConstraints.None
                : AiNavigationConstraints.ForClusterProtection(cluster);
        }
    }
}
