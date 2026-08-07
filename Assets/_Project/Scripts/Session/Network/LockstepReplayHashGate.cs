using System.Collections.Generic;
using DiceGame.Gameplay;
using DiceGame.Placement;
using UnityEngine;
using GameCharacterController = DiceGame.Gameplay.CharacterController;

namespace DiceGame.Session.Network
{
    /// <summary>
    /// Phase A gate: compare two hashes produced by <see cref="OnlineSimStateHasher"/> for the same
    /// tick after an identical input stream. Expand coverage as more state moves into sim ownership.
    /// </summary>
    public static class LockstepReplayHashGate
    {
        public static bool TryCompare(
            uint tick,
            IReadOnlyList<GameCharacterController> charactersA,
            DiceRegistry registryA,
            DiceMatchOwnershipContext ownershipA,
            IReadOnlyList<GameCharacterController> charactersB,
            DiceRegistry registryB,
            DiceMatchOwnershipContext ownershipB,
            out uint hashA,
            out uint hashB) {
            hashA = OnlineSimStateHasher.Compute(tick, charactersA, registryA, ownershipA);
            hashB = OnlineSimStateHasher.Compute(tick, charactersB, registryB, ownershipB);
            return hashA == hashB;
        }

        public static void LogMismatch(uint tick, uint hashA, uint hashB) {
            Debug.LogError(
                $"[LockstepReplayHashGate] desync at tick={tick} hashA=0x{hashA:X8} hashB=0x{hashB:X8}");
        }
    }
}
