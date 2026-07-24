using System.Collections.Generic;
using DiceGame.Config;

namespace DiceGame.Session.Network
{
    /// <summary>
    /// Per-tick input storage for delayed lockstep (both player slots).
    /// </summary>
    public sealed class OnlineLockstepInputBuffer
    {
        readonly Dictionary<uint, OnlineInputPayload> player1 = new();
        readonly Dictionary<uint, OnlineInputPayload> player2 = new();
        readonly int capacityTicks;

        public OnlineLockstepInputBuffer(int capacityTicks) {
            this.capacityTicks = capacityTicks > 0 ? capacityTicks : OnlineSessionConstants.LockstepInputBufferTicks;
        }

        public bool TryGet(PlayerSlot slot, uint tick, out OnlineInputPayload payload) {
            var map = MapFor(slot);
            return map.TryGetValue(tick, out payload);
        }

        public bool Has(PlayerSlot slot, uint tick) {
            return MapFor(slot).ContainsKey(tick);
        }

        public bool HasBoth(uint tick) {
            return player1.ContainsKey(tick) && player2.ContainsKey(tick);
        }

        public void Set(PlayerSlot slot, uint tick, OnlineInputPayload payload) {
            payload.Tick = tick;
            var map = MapFor(slot);
            map[tick] = payload;
            Trim(map, tick);
        }

        public void DiscardBefore(uint tick) {
            DiscardBefore(player1, tick);
            DiscardBefore(player2, tick);
        }

        public void Clear() {
            player1.Clear();
            player2.Clear();
        }

        Dictionary<uint, OnlineInputPayload> MapFor(PlayerSlot slot) {
            return slot == PlayerSlot.Player1 ? player1 : player2;
        }

        void Trim(Dictionary<uint, OnlineInputPayload> map, uint latestTick) {
            if (map.Count <= capacityTicks) {
                return;
            }

            var minKeep = latestTick > (uint)capacityTicks ? latestTick - (uint)capacityTicks : 0u;
            DiscardBefore(map, minKeep);
        }

        static void DiscardBefore(Dictionary<uint, OnlineInputPayload> map, uint tick) {
            if (map.Count == 0) {
                return;
            }

            var remove = new List<uint>();
            foreach (var key in map.Keys) {
                if (key < tick) {
                    remove.Add(key);
                }
            }

            for (var i = 0; i < remove.Count; i++) {
                map.Remove(remove[i]);
            }
        }
    }
}
