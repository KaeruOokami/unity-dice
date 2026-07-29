using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Grid;
using DiceGame.Versus.Core;
using UnityEngine;
using GameCharacterController = DiceGame.Gameplay.CharacterController;

namespace DiceGame.Versus
{
    /// <summary>
    /// Versus-only jumbo sequence: face-1 vanish starts Jumbo2 on the opponent,
    /// then each fully erased jumbo advances 3..6 toward the opponent of the last owner.
    /// When a 2x2 spawn is unavailable, the sequence waits and retries until space opens.
    /// </summary>
    public sealed class JumboDiceSequenceController : MonoBehaviour
    {
        IVersusBoardSettings versusSettings;
        JumboDiceSettings jumboSettings;
        Board board;
        DiceSpawnSystem spawnSystem;
        DiceMatchOwnershipContext ownershipContext;
        DiceOneVanishSystem oneVanishSystem;
        readonly List<GameCharacterController> characters = new();
        readonly List<Vector2Int> blockedCellsBuffer = new();

        bool sequenceActive;
        int nextFace = 2;
        DiceController activeJumbo;
        PlayerSlot lastJumboOwner;
        bool hasPendingSpawn;
        PlayerSlot pendingSpawnTarget;

        public void Configure(
            IVersusBoardSettings settings,
            Board targetBoard,
            DiceSpawnSystem targetSpawnSystem,
            DiceMatchOwnershipContext targetOwnership,
            DiceOneVanishSystem targetOneVanish,
            IReadOnlyList<GameCharacterController> targetCharacters) {
            versusSettings = settings;
            jumboSettings = settings != null ? settings.JumboDiceSettings : null;
            board = targetBoard;
            spawnSystem = targetSpawnSystem;
            ownershipContext = targetOwnership;
            oneVanishSystem = targetOneVanish;
            characters.Clear();
            if (targetCharacters != null) {
                characters.AddRange(targetCharacters);
            }

            sequenceActive = false;
            nextFace = jumboSettings != null ? jumboSettings.SequenceStartFace : 2;
            activeJumbo = null;
            ClearPendingSpawn();

            if (oneVanishSystem != null) {
                oneVanishSystem.FaceOneVanished -= OnFaceOneVanished;
                oneVanishSystem.FaceOneVanished += OnFaceOneVanished;
            }
        }

        void Update() {
            if (GameplaySimClock.IsActive) {
                return;
            }

            TickPendingSpawn();
        }

        void LateUpdate() {
            // Keep last owner fresh through snatches until Erased handlers clear ownership.
            if (activeJumbo == null || ownershipContext == null) {
                return;
            }

            if (ownershipContext.TryGetOwner(activeJumbo, out var owner)) {
                lastJumboOwner = owner;
            }
        }

        /// <summary>
        /// Retry a pending jumbo spawn during lockstep (offline uses <see cref="Update"/>).
        /// </summary>
        public void SimulateLockstepTick() {
            TickPendingSpawn();
        }

        void OnDisable() {
            if (oneVanishSystem != null) {
                oneVanishSystem.FaceOneVanished -= OnFaceOneVanished;
            }

            UnsubscribeActiveJumbo();
        }

        void OnFaceOneVanished(PlayerSlot initiator) {
            if (!IsJumboEnabled() || sequenceActive || activeJumbo != null || hasPendingSpawn) {
                return;
            }

            sequenceActive = true;
            nextFace = jumboSettings.SequenceStartFace;
            var target = SinkingChainResolver.GetOpponent(initiator);
            TrySpawnJumbo(target, nextFace);
        }

        void OnJumboErased(DiceController dice) {
            if (dice == null || dice != activeJumbo) {
                return;
            }

            var owner = lastJumboOwner;
            if (ownershipContext != null && ownershipContext.TryGetOwner(dice, out var liveOwner)) {
                owner = liveOwner;
            }

            UnsubscribeActiveJumbo();
            activeJumbo = null;

            var endFace = jumboSettings != null ? jumboSettings.SequenceEndFace : 6;
            if (nextFace >= endFace) {
                EndSequence();
                return;
            }

            nextFace++;
            var target = SinkingChainResolver.GetOpponent(owner);
            TrySpawnJumbo(target, nextFace);
        }

        void TickPendingSpawn() {
            if (!hasPendingSpawn || activeJumbo != null || !sequenceActive || !IsJumboEnabled()) {
                return;
            }

            TrySpawnJumbo(pendingSpawnTarget, nextFace);
        }

        void TrySpawnJumbo(PlayerSlot target, int face) {
            if (spawnSystem == null || versusSettings == null) {
                BeginPendingSpawn(target);
                return;
            }

            var spawnSettings = versusSettings.GetSpawnSettings(target);
            CollectBlockedCells(target);
            var jumbo = spawnSystem.SpawnJumboDice(target, face, spawnSettings, blockedCellsBuffer);
            if (jumbo == null) {
                BeginPendingSpawn(target);
                return;
            }

            ClearPendingSpawn();
            activeJumbo = jumbo;
            lastJumboOwner = target;
            jumbo.Erased += OnJumboErased;
            jumbo.ErasureStarted += OnJumboErasureStarted;
        }

        void BeginPendingSpawn(PlayerSlot target) {
            hasPendingSpawn = true;
            pendingSpawnTarget = target;
        }

        void ClearPendingSpawn() {
            hasPendingSpawn = false;
        }

        void EndSequence() {
            sequenceActive = false;
            ClearPendingSpawn();
            nextFace = jumboSettings != null ? jumboSettings.SequenceStartFace : 2;
        }

        void OnJumboErasureStarted(DiceController dice) {
            if (dice == null || dice != activeJumbo || ownershipContext == null) {
                return;
            }

            if (ownershipContext.TryGetOwner(dice, out var owner)) {
                lastJumboOwner = owner;
            }
        }

        void CollectBlockedCells(PlayerSlot targetSlot) {
            blockedCellsBuffer.Clear();
            for (var i = 0; i < characters.Count; i++) {
                var character = characters[i];
                if (character == null || character.PlayerSlot != targetSlot) {
                    continue;
                }

                blockedCellsBuffer.Add(character.StandingGridCell);
            }
        }

        void UnsubscribeActiveJumbo() {
            if (activeJumbo == null) {
                return;
            }

            activeJumbo.Erased -= OnJumboErased;
            activeJumbo.ErasureStarted -= OnJumboErasureStarted;
        }

        bool IsJumboEnabled() {
            return jumboSettings != null
                && jumboSettings.Enabled
                && board != null
                && board.IsVersusArena
                && versusSettings != null;
        }
    }
}
