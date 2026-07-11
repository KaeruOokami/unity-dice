using System.Collections.Generic;
using DiceGame.Config;

namespace DiceGame.Gameplay
{
    public sealed class DeferredMatchSnapshot
    {
        readonly List<DiceController> participants;
        readonly PlayerSlot attacker;

        public DeferredMatchSnapshot(DiceController participant, PlayerSlot attacker) {
            participants = new List<DiceController> { participant };
            this.attacker = attacker;
        }

        public IReadOnlyList<DiceController> Participants => participants;
        public PlayerSlot Attacker => attacker;
    }
}
