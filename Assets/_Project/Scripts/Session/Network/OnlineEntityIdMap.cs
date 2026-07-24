using System;
using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Gameplay;
using DiceGame.Placement;
using GameCharacterController = DiceGame.Gameplay.CharacterController;

namespace DiceGame.Session.Network
{
    /// <summary>
    /// Deterministic entity IDs shared by host and client after seed spawn.
    /// Characters: Player1 then Player2. Dice: stable sort of logical state.
    /// New dice after seed take the next sequential id on both sides via events.
    /// </summary>
    public sealed class OnlineEntityIdMap
    {
        readonly Dictionary<int, uint> diceInstanceToId = new();
        readonly Dictionary<uint, DiceController> idToDice = new();
        readonly Dictionary<PlayerSlot, uint> characterIds = new();
        uint nextEntityId = 1;

        public uint NextEntityId => nextEntityId;
        public IReadOnlyDictionary<PlayerSlot, uint> CharacterIds => characterIds;

        public void RebuildFromSeed(
            IReadOnlyList<GameCharacterController> characters,
            DiceRegistry registry,
            DiceMatchOwnershipContext ownershipContext) {
            Clear();

            AssignCharacterSlot(PlayerSlot.Player1, characters);
            AssignCharacterSlot(PlayerSlot.Player2, characters);

            if (registry == null) {
                return;
            }

            var diceList = new List<DiceController>(registry.AllDice.Count);
            for (var i = 0; i < registry.AllDice.Count; i++) {
                var dice = registry.AllDice[i];
                if (dice != null) {
                    diceList.Add(dice);
                }
            }

            diceList.Sort((a, b) => CompareSeedDice(a, b, ownershipContext));
            for (var i = 0; i < diceList.Count; i++) {
                RegisterDice(diceList[i], nextEntityId++);
            }
        }

        public uint EnsureDice(DiceController dice) {
            if (dice == null) {
                return 0;
            }

            var key = dice.GetInstanceID();
            if (diceInstanceToId.TryGetValue(key, out var existing)) {
                return existing;
            }

            var id = nextEntityId++;
            RegisterDice(dice, id);
            return id;
        }

        public void RegisterDice(DiceController dice, uint entityId) {
            if (dice == null || entityId == 0) {
                return;
            }

            var key = dice.GetInstanceID();
            if (diceInstanceToId.TryGetValue(key, out var previousId) && previousId != entityId) {
                idToDice.Remove(previousId);
            }

            if (idToDice.TryGetValue(entityId, out var other) && other != null && other != dice) {
                diceInstanceToId.Remove(other.GetInstanceID());
            }

            diceInstanceToId[key] = entityId;
            idToDice[entityId] = dice;
            if (entityId >= nextEntityId) {
                nextEntityId = entityId + 1;
            }
        }

        public bool TryGetDice(uint entityId, out DiceController dice) {
            return idToDice.TryGetValue(entityId, out dice) && dice != null;
        }

        public bool TryGetCharacterId(PlayerSlot slot, out uint entityId) {
            return characterIds.TryGetValue(slot, out entityId);
        }

        public void UnregisterDice(DiceController dice) {
            if (dice == null) {
                return;
            }

            var key = dice.GetInstanceID();
            if (!diceInstanceToId.TryGetValue(key, out var id)) {
                return;
            }

            diceInstanceToId.Remove(key);
            if (idToDice.TryGetValue(id, out var mapped) && mapped == dice) {
                idToDice.Remove(id);
            }
        }

        void Clear() {
            diceInstanceToId.Clear();
            idToDice.Clear();
            characterIds.Clear();
            nextEntityId = 1;
        }

        void AssignCharacterSlot(PlayerSlot slot, IReadOnlyList<GameCharacterController> characters) {
            if (characters == null) {
                return;
            }

            for (var i = 0; i < characters.Count; i++) {
                var character = characters[i];
                if (character != null && character.PlayerSlot == slot) {
                    characterIds[slot] = nextEntityId++;
                    return;
                }
            }
        }

        static int CompareSeedDice(
            DiceController a,
            DiceController b,
            DiceMatchOwnershipContext ownershipContext) {
            var sa = a.CurrentState;
            var sb = b.CurrentState;
            var cmp = sa.GridPos.x.CompareTo(sb.GridPos.x);
            if (cmp != 0) {
                return cmp;
            }

            cmp = sa.GridPos.y.CompareTo(sb.GridPos.y);
            if (cmp != 0) {
                return cmp;
            }

            cmp = ((byte)sa.Tier).CompareTo((byte)sb.Tier);
            if (cmp != 0) {
                return cmp;
            }

            cmp = ((byte)sa.Kind).CompareTo((byte)sb.Kind);
            if (cmp != 0) {
                return cmp;
            }

            cmp = ResolveOwnerByte(a, ownershipContext).CompareTo(ResolveOwnerByte(b, ownershipContext));
            if (cmp != 0) {
                return cmp;
            }

            cmp = sa.Orientation.Top.CompareTo(sb.Orientation.Top);
            if (cmp != 0) {
                return cmp;
            }

            cmp = sa.Orientation.North.CompareTo(sb.Orientation.North);
            if (cmp != 0) {
                return cmp;
            }

            return sa.Orientation.East.CompareTo(sb.Orientation.East);
        }

        static byte ResolveOwnerByte(DiceController dice, DiceMatchOwnershipContext ownershipContext) {
            if (ownershipContext != null && ownershipContext.TryGetOwner(dice, out var owner)) {
                return (byte)owner;
            }

            return 0;
        }
    }
}
