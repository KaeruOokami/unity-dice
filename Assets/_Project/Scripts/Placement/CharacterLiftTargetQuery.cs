using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Placement
{
    /// <summary>
    /// Resolves which adjacent die can be lifted from the current standing pose.
    /// </summary>
    public static class CharacterLiftTargetQuery
    {
        public static bool TryResolve(
            CharacterPlacement standing,
            bool isOnFloor,
            DiceController standingDice,
            Vector2Int neighborGrid,
            Board board,
            DiceRegistry registry,
            out DiceController targetDice) {
            targetDice = null;

            if (registry == null || board == null || !board.IsInside(neighborGrid)) {
                return false;
            }

            var candidate = ResolveCandidateAt(standing, neighborGrid, registry);
            if (candidate == null) {
                return false;
            }

            if (candidate == standingDice
                || candidate.IsErasing
                || candidate.IsVanishing
                || candidate.IsBusy
                || !LiftPassability.CanLift(standing, isOnFloor, standingDice, candidate, registry)) {
                return false;
            }

            targetDice = candidate;
            return true;
        }

        static DiceController ResolveCandidateAt(
            CharacterPlacement standing,
            Vector2Int neighborGrid,
            DiceRegistry registry) {
            registry.TryGetTopAt(neighborGrid, out var top);
            registry.TryGetBottomAt(neighborGrid, out var bottom);

            if (top != null && LiftPassability.IsReachable(standing, top)) {
                return top;
            }

            if (bottom != null && LiftPassability.IsReachable(standing, bottom)) {
                return bottom;
            }

            return null;
        }
    }
}
