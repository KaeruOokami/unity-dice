using System;
using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Gameplay.Input;
using DiceGame.Placement;
using DiceGame.Versus;
using DiceGame.Versus.Core;
using DiceGame.View;
using UnityEngine;
using GameCharacterController = DiceGame.Gameplay.CharacterController;

namespace DiceGame.Session.Network
{
    /// <summary>
    /// Host authority after seed-based initial spawn: Reliable dice motion events,
    /// attack queue sync, and character pose updates (no full-board snapshot).
    /// </summary>
    public sealed class OnlineHostMatchBinder : MonoBehaviour
    {
        OnlineNetMessenger messenger;
        RemoteNetworkInputSource remoteInput;
        DiceRegistry registry;
        DiceMatchOwnershipContext ownershipContext;
        VersusAttackController attackController;
        readonly List<GameCharacterController> characters = new();
        float characterSnapshotTimer;
        float attackQueueResyncTimer;
        uint nextEntityId = 1;
        readonly Dictionary<int, uint> diceIds = new();
        readonly Dictionary<PlayerSlot, uint> characterIds = new();

        public void Configure(
            OnlineNetMessenger netMessenger,
            DiceRegistry diceRegistry,
            DiceMatchOwnershipContext matchOwnership,
            IReadOnlyList<GameCharacterController> spawnedCharacters,
            VersusAttackController versusAttackController = null) {
            messenger = netMessenger;
            registry = diceRegistry;
            ownershipContext = matchOwnership;
            characters.Clear();
            if (spawnedCharacters != null) {
                characters.AddRange(spawnedCharacters);
            }

            if (messenger != null) {
                messenger.InputReceived -= OnInputReceived;
                messenger.InputReceived += OnInputReceived;
            }

            DiceVisualMotionHub.MotionStarted -= OnDiceVisualMotionStarted;
            DiceVisualMotionHub.MotionStarted += OnDiceVisualMotionStarted;

            if (attackController != null) {
                attackController.QueuePresentationChanged -= OnAttackQueueChanged;
            }

            attackController = versusAttackController;
            if (attackController != null) {
                attackController.QueuePresentationChanged -= OnAttackQueueChanged;
                attackController.QueuePresentationChanged += OnAttackQueueChanged;
                OnAttackQueueChanged();
            }

            BindRemotePlayerInput();
            AssignIds();
            characterSnapshotTimer = 0f;
            attackQueueResyncTimer = 0f;
        }

        void OnDestroy() {
            if (messenger != null) {
                messenger.InputReceived -= OnInputReceived;
            }

            DiceVisualMotionHub.MotionStarted -= OnDiceVisualMotionStarted;
            if (attackController != null) {
                attackController.QueuePresentationChanged -= OnAttackQueueChanged;
            }
        }

        void Update() {
            if (!enabled) {
                return;
            }

            if (messenger == null || registry == null) {
                return;
            }

            characterSnapshotTimer += Time.unscaledDeltaTime;
            if (characterSnapshotTimer >= OnlineSessionConstants.SnapshotSendIntervalSeconds) {
                characterSnapshotTimer = 0f;
                messenger.SendSnapshotToClients(BuildCharacterSnapshot());
            }

            if (attackController != null) {
                attackQueueResyncTimer += Time.unscaledDeltaTime;
                if (attackQueueResyncTimer >= OnlineSessionConstants.AttackQueueResyncIntervalSeconds) {
                    attackQueueResyncTimer = 0f;
                    OnAttackQueueChanged();
                }
            }
        }

        void BindRemotePlayerInput() {
            GameCharacterController remoteCharacter = null;
            foreach (var character in characters) {
                if (character != null && character.PlayerSlot == PlayerSlot.Player2) {
                    remoteCharacter = character;
                    break;
                }
            }

            if (remoteCharacter == null) {
                Debug.LogError("OnlineHostMatchBinder: Player2 character not found.");
                return;
            }

            var humanReader = remoteCharacter.GetComponent<CharacterInputReader>();
            if (humanReader != null) {
                humanReader.enabled = false;
            }

            remoteInput = remoteCharacter.GetComponent<RemoteNetworkInputSource>();
            if (remoteInput == null) {
                remoteInput = remoteCharacter.gameObject.AddComponent<RemoteNetworkInputSource>();
            }

            remoteCharacter.SetInputSource(remoteInput);
        }

        void AssignIds() {
            diceIds.Clear();
            characterIds.Clear();
            nextEntityId = 1;

            foreach (var character in characters) {
                if (character == null) {
                    continue;
                }

                characterIds[character.PlayerSlot] = nextEntityId++;
            }
        }

        void OnInputReceived(ulong senderClientId, OnlineInputPayload payload) {
            remoteInput?.ApplyPayload(payload);
        }

        void OnAttackQueueChanged() {
            if (messenger == null || attackController == null) {
                return;
            }

            messenger.SendAttackQueueToClients(BuildAttackQueueSnapshot());
        }

        OnlineAttackQueueSnapshot BuildAttackQueueSnapshot() {
            return new OnlineAttackQueueSnapshot {
                Player1Volleys = ToNetworkVolleys(attackController.GetPendingVolleys(PlayerSlot.Player1)),
                Player2Volleys = ToNetworkVolleys(attackController.GetPendingVolleys(PlayerSlot.Player2))
            };
        }

        static OnlineAttackVolleyPayload[] ToNetworkVolleys(IReadOnlyList<AttackVolley> volleys) {
            if (volleys == null || volleys.Count == 0) {
                return Array.Empty<OnlineAttackVolleyPayload>();
            }

            var result = new OnlineAttackVolleyPayload[volleys.Count];
            for (var i = 0; i < volleys.Count; i++) {
                result[i] = OnlineAttackVolleyPayload.FromVolley(volleys[i]);
            }

            return result;
        }

        void OnDiceVisualMotionStarted(DiceView view, DiceVisualMotionRequest request) {
            if (!enabled || messenger == null || view == null) {
                return;
            }

            if (!TryResolveDiceController(view, out var controller)) {
                Debug.LogError("OnlineHostMatchBinder: failed to resolve dice for motion event.");
                return;
            }

            var entityId = EnsureDiceId(controller);
            var catalogSide = ResolveCatalogSide(controller);
            messenger.SendDiceMotionToClients(
                OnlineDiceMotionEvent.FromRequest(entityId, request, catalogSide));
        }

        static bool TryResolveDiceController(DiceView view, out DiceController controller) {
            controller = view.GetComponentInParent<DiceController>();
            if (controller == null) {
                controller = view.GetComponent<DiceController>();
            }

            return controller != null;
        }

        PlayerSlot ResolveCatalogSide(DiceController dice) {
            if (ownershipContext != null && ownershipContext.TryGetOwner(dice, out var owner)) {
                return owner;
            }

            return PlayerSlot.Player1;
        }

        uint EnsureDiceId(DiceController dice) {
            var key = dice.GetInstanceID();
            if (!diceIds.TryGetValue(key, out var id)) {
                id = nextEntityId++;
                diceIds[key] = id;
            }

            return id;
        }

        OnlineMatchSnapshot BuildCharacterSnapshot() {
            return new OnlineMatchSnapshot {
                Entities = BuildCharacterEntities().ToArray()
            };
        }

        List<OnlineTransformSnapshot> BuildCharacterEntities() {
            var entities = new List<OnlineTransformSnapshot>(characters.Count);
            foreach (var character in characters) {
                if (character == null) {
                    continue;
                }

                if (!characterIds.TryGetValue(character.PlayerSlot, out var id)) {
                    id = nextEntityId++;
                    characterIds[character.PlayerSlot] = id;
                }

                entities.Add(new OnlineTransformSnapshot {
                    Id = id,
                    Position = character.transform.position,
                    Rotation = character.transform.rotation,
                    Kind = (byte)character.PlayerSlot,
                    Flags = (byte)(OnlineTransformSnapshot.FlagCharacter | OnlineTransformSnapshot.FlagActive),
                    CatalogSide = (byte)character.PlayerSlot
                });
            }

            return entities;
        }
    }
}
