using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Grid;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    public static class AiRegionFilter
    {
        public static List<DiceSnapshot> FilterPlanningDice(
            IReadOnlyList<DiceSnapshot> allDice,
            VersusArenaLayout versusLayout,
            PlayerSlot playerSlot) {
            if (allDice == null || allDice.Count == 0) {
                return new List<DiceSnapshot>();
            }

            if (versusLayout == null) {
                return new List<DiceSnapshot>(allDice);
            }

            var filtered = new List<DiceSnapshot>(allDice.Count);
            for (var i = 0; i < allDice.Count; i++) {
                var snapshot = allDice[i];
                if (IsInPlayerRegion(versusLayout, playerSlot, snapshot.GridPos)) {
                    filtered.Add(snapshot);
                }
            }

            return filtered;
        }

        public static bool IsInPlayerRegion(
            VersusArenaLayout versusLayout,
            PlayerSlot playerSlot,
            Vector2Int cell) {
            return versusLayout == null || versusLayout.IsInsidePlayerRegion(playerSlot, cell);
        }
    }
}
