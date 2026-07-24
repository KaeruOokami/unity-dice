using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Placement;
using UnityEngine;
using GameCharacterController = DiceGame.Gameplay.CharacterController;

namespace DiceGame.Session.Network
{
    /// <summary>
    /// Logical board fingerprint for lockstep desync detection.
    /// Excludes visual/busy timing and fine world poses (those are not authoritative in lockstep).
    /// </summary>
    public static class OnlineSimStateHasher
    {
        const uint FnvOffset = 2166136261u;
        const uint FnvPrime = 16777619u;

        public static uint Compute(
            uint tick,
            IReadOnlyList<GameCharacterController> characters,
            DiceRegistry registry,
            DiceMatchOwnershipContext ownershipContext) {
            var hash = FnvOffset;
            hash = Mix(hash, tick);

            if (characters != null) {
                MixCharacter(ref hash, FindCharacter(characters, PlayerSlot.Player1));
                MixCharacter(ref hash, FindCharacter(characters, PlayerSlot.Player2));
            }

            if (registry == null) {
                return hash;
            }

            var diceList = new List<DiceController>(registry.AllDice.Count);
            for (var i = 0; i < registry.AllDice.Count; i++) {
                var dice = registry.AllDice[i];
                if (dice == null || dice.IsSpawning) {
                    continue;
                }

                diceList.Add(dice);
            }

            diceList.Sort((a, b) => CompareDice(a, b, ownershipContext));
            hash = Mix(hash, (uint)diceList.Count);
            for (var i = 0; i < diceList.Count; i++) {
                MixDice(ref hash, diceList[i], ownershipContext);
            }

            return hash;
        }

        static void MixCharacter(ref uint hash, GameCharacterController character) {
            if (character == null) {
                hash = Mix(hash, 0u);
                return;
            }

            var cell = character.StandingGridCell;
            hash = Mix(hash, (uint)character.PlayerSlot);
            hash = Mix(hash, (uint)(ushort)cell.x);
            hash = Mix(hash, (uint)(ushort)cell.y);
            hash = Mix(hash, (uint)character.PlayerLevel);
            hash = Mix(hash, character.IsCarrying ? 1u : 0u);
            hash = Mix(hash, character.IsJumping ? 1u : 0u);
        }

        static void MixDice(
            ref uint hash,
            DiceController dice,
            DiceMatchOwnershipContext ownershipContext) {
            var state = dice.CurrentState;
            hash = Mix(hash, (uint)(ushort)state.GridPos.x);
            hash = Mix(hash, (uint)(ushort)state.GridPos.y);
            hash = Mix(hash, (byte)state.Tier);
            hash = Mix(hash, (byte)state.Kind);
            hash = Mix(hash, (byte)state.Orientation.Top);
            hash = Mix(hash, (byte)state.Orientation.North);
            hash = Mix(hash, (byte)state.Orientation.East);
            hash = Mix(hash, ResolveOwner(dice, ownershipContext));
            hash = Mix(hash, dice.IsCarried ? 1u : 0u);
            hash = Mix(hash, dice.IsErasing ? 1u : 0u);
            hash = Mix(hash, dice.IsVanishing ? 1u : 0u);
        }

        static GameCharacterController FindCharacter(
            IReadOnlyList<GameCharacterController> characters,
            PlayerSlot slot) {
            for (var i = 0; i < characters.Count; i++) {
                var character = characters[i];
                if (character != null && character.PlayerSlot == slot) {
                    return character;
                }
            }

            return null;
        }

        static int CompareDice(
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

            cmp = ResolveOwner(a, ownershipContext).CompareTo(ResolveOwner(b, ownershipContext));
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

        static byte ResolveOwner(DiceController dice, DiceMatchOwnershipContext ownershipContext) {
            if (ownershipContext != null && ownershipContext.TryGetOwner(dice, out var owner)) {
                return (byte)owner;
            }

            return 0;
        }

        static uint Mix(uint hash, uint value) {
            unchecked {
                hash ^= value;
                hash *= FnvPrime;
                return hash;
            }
        }
    }

    public static class OnlineSimBoardSnapshotBuilder
    {
        public static OnlineTransformSnapshot[] Build(
            IReadOnlyList<GameCharacterController> characters,
            DiceRegistry registry,
            DiceMatchOwnershipContext ownershipContext,
            OnlineEntityIdMap entityIds) {
            var entities = new List<OnlineTransformSnapshot>(32);
            if (characters != null) {
                for (var i = 0; i < characters.Count; i++) {
                    var character = characters[i];
                    if (character == null) {
                        continue;
                    }

                    uint id;
                    if (entityIds != null) {
                        if (!entityIds.TryGetCharacterId(character.PlayerSlot, out id)) {
                            continue;
                        }
                    } else {
                        id = (uint)character.PlayerSlot + 1;
                    }

                    entities.Add(new OnlineTransformSnapshot {
                        Id = id,
                        Position = character.transform.position,
                        Rotation = character.transform.rotation,
                        Kind = (byte)character.PlayerSlot,
                        Flags = (byte)(OnlineTransformSnapshot.FlagCharacter
                            | OnlineTransformSnapshot.FlagActive),
                        CatalogSide = (byte)character.PlayerSlot
                    });
                }
            }

            if (registry == null) {
                return entities.ToArray();
            }

            var diceList = new List<DiceController>(registry.AllDice.Count);
            for (var i = 0; i < registry.AllDice.Count; i++) {
                var dice = registry.AllDice[i];
                if (dice != null) {
                    diceList.Add(dice);
                }
            }

            diceList.Sort((a, b) => OnlineSimStateHasherCompare(a, b, ownershipContext));
            for (var i = 0; i < diceList.Count; i++) {
                var dice = diceList[i];
                var state = dice.CurrentState;
                var id = entityIds != null ? entityIds.EnsureDice(dice) : (uint)(i + 10);
                var owner = PlayerSlot.Player1;
                if (ownershipContext != null) {
                    ownershipContext.TryGetOwner(dice, out owner);
                }

                entities.Add(new OnlineTransformSnapshot {
                    Id = id,
                    Kind = (byte)state.Kind,
                    CatalogSide = (byte)owner,
                    GridX = (short)state.GridPos.x,
                    GridY = (short)state.GridPos.y,
                    Tier = (byte)state.Tier,
                    TopFace = (byte)state.Orientation.Top,
                    NorthFace = (byte)state.Orientation.North,
                    EastFace = (byte)state.Orientation.East,
                    Flags = (byte)(OnlineTransformSnapshot.FlagDice | OnlineTransformSnapshot.FlagActive)
                });
            }

            return entities.ToArray();
        }

        // Local compare to avoid exposing hasher internals as public API beyond Compute.
        static int OnlineSimStateHasherCompare(
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

            byte oa = 0;
            byte ob = 0;
            if (ownershipContext != null) {
                if (ownershipContext.TryGetOwner(a, out var ownerA)) {
                    oa = (byte)ownerA;
                }

                if (ownershipContext.TryGetOwner(b, out var ownerB)) {
                    ob = (byte)ownerB;
                }
            }

            cmp = oa.CompareTo(ob);
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
    }
}
