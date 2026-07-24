using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Gameplay.Character;
using DiceGame.Placement;
using UnityEngine;
using GameCharacterController = DiceGame.Gameplay.CharacterController;

namespace DiceGame.Session.Network
{
    /// <summary>
    /// Applies a host Phase C resync dump onto the local dual-sim board.
    /// </summary>
    public static class OnlineSimResyncApplier
    {
        public static void Apply(
            OnlineSimResyncPayload payload,
            IReadOnlyList<GameCharacterController> characters,
            DiceRegistry registry,
            DiceMatchOwnershipContext ownershipContext,
            DiceSpawnSystem spawnSystem,
            OnlineEntityIdMap entityIds) {
            if (payload.Entities == null) {
                Debug.LogError("OnlineSimResyncApplier: Entities is null.");
                return;
            }

            var usedDice = new HashSet<DiceController>();
            for (var i = 0; i < payload.Entities.Length; i++) {
                var entity = payload.Entities[i];
                if (!entity.IsActive) {
                    continue;
                }

                if (entity.IsCharacter) {
                    ApplyCharacter(entity, characters);
                    continue;
                }

                if (!entity.IsDice) {
                    continue;
                }

                var dice = ResolveOrSpawnDice(
                    entity,
                    registry,
                    ownershipContext,
                    spawnSystem,
                    entityIds);
                if (dice == null) {
                    continue;
                }

                ApplyDiceLogicalState(dice, entity.ToDiceState(), registry);
                entityIds?.RegisterDice(dice, entity.Id);
                if (ownershipContext != null) {
                    ownershipContext.SetOwner(dice, entity.CatalogPlayerSlot);
                }

                usedDice.Add(dice);
            }

            if (registry == null) {
                return;
            }

            var toDestroy = new List<DiceController>();
            for (var i = 0; i < registry.AllDice.Count; i++) {
                var dice = registry.AllDice[i];
                if (dice != null && !usedDice.Contains(dice)) {
                    toDestroy.Add(dice);
                }
            }

            for (var i = 0; i < toDestroy.Count; i++) {
                entityIds?.UnregisterDice(toDestroy[i]);
                toDestroy[i].ForceDestroyForOverride();
            }
        }

        static void ApplyCharacter(
            OnlineTransformSnapshot entity,
            IReadOnlyList<GameCharacterController> characters) {
            if (characters == null) {
                return;
            }

            var slot = (PlayerSlot)entity.Kind;
            for (var i = 0; i < characters.Count; i++) {
                var character = characters[i];
                if (character == null || character.PlayerSlot != slot) {
                    continue;
                }

                character.ApplyRollbackState(new CharacterRollbackState {
                    Sequence = 0,
                    Position = entity.Position,
                    Rotation = entity.Rotation,
                    Speed = 0f,
                    IsBusy = false
                });
                return;
            }

            Debug.LogError($"OnlineSimResyncApplier: character missing for slot={slot}.");
        }

        static DiceController ResolveOrSpawnDice(
            OnlineTransformSnapshot entity,
            DiceRegistry registry,
            DiceMatchOwnershipContext ownershipContext,
            DiceSpawnSystem spawnSystem,
            OnlineEntityIdMap entityIds) {
            if (entityIds != null && entityIds.TryGetDice(entity.Id, out var mapped) && mapped != null) {
                return mapped;
            }

            if (registry != null) {
                for (var i = 0; i < registry.AllDice.Count; i++) {
                    var dice = registry.AllDice[i];
                    if (dice == null) {
                        continue;
                    }

                    var state = dice.CurrentState;
                    if (state.GridPos.x == entity.GridX
                        && state.GridPos.y == entity.GridY
                        && (byte)state.Tier == entity.Tier
                        && (byte)state.Kind == entity.Kind) {
                        return dice;
                    }
                }
            }

            if (spawnSystem == null) {
                Debug.LogError(
                    $"OnlineSimResyncApplier: cannot spawn missing dice id={entity.Id}.");
                return null;
            }

            if (!spawnSystem.TryGetSpawnSettings(entity.CatalogPlayerSlot, out var spawnSettings)
                || spawnSettings == null) {
                Debug.LogError(
                    $"OnlineSimResyncApplier: spawn settings missing for {entity.CatalogPlayerSlot}.");
                return null;
            }

            return spawnSystem.ApplyNetworkSpawn(
                new Vector2Int(entity.GridX, entity.GridY),
                (DiceStackTier)entity.Tier,
                (DiceKind)entity.Kind,
                new DiceOrientation(entity.TopFace, entity.NorthFace, entity.EastFace),
                entity.CatalogPlayerSlot,
                spawnSettings,
                useSpawnAppear: false,
                forceFallFromAbove: false);
        }

        static void ApplyDiceLogicalState(
            DiceController dice,
            DiceState toState,
            DiceRegistry registry) {
            if (dice == null) {
                return;
            }

            var fromState = dice.CurrentState;
            if (registry != null
                && (fromState.GridPos != toState.GridPos || fromState.Tier != toState.Tier)) {
                registry.MoveDice(
                    dice,
                    fromState.GridPos,
                    toState.GridPos,
                    fromState.Tier,
                    toState.Tier);
            }

            dice.ApplyExternalState(toState, snapVisual: true);
        }
    }
}
