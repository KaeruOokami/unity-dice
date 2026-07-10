using System.Collections.Generic;
using DiceGame.Config;

namespace DiceGame.Gameplay
{
    public sealed class MatchActionSnapshot
    {
        readonly List<DiceController> allDice;
        readonly Dictionary<PlayerSlot, List<DiceController>> diceByPlayer;

        public MatchActionSnapshot(
            List<DiceController> all,
            Dictionary<PlayerSlot, List<DiceController>> byPlayer) {
            allDice = all;
            diceByPlayer = byPlayer;
        }

        public IReadOnlyList<DiceController> AllDice => allDice;

        public IReadOnlyList<DiceController> GetDiceFor(PlayerSlot slot) {
            return diceByPlayer.TryGetValue(slot, out var list)
                ? list
                : System.Array.Empty<DiceController>();
        }

        public IEnumerable<PlayerSlot> GetParticipatingPlayers() {
            foreach (var pair in diceByPlayer) {
                if (pair.Value.Count > 0) {
                    yield return pair.Key;
                }
            }
        }
    }
}
