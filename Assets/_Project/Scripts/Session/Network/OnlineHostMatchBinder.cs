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
    /// Phase A host authority: Reliable dice motion/spawn-appear events, attack queue,
    /// one-shot full board snapshot, then character-only pose updates (no periodic full board).
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
        bool sentInitialBoardSnapshot;
        uint nextEntityId = 1;
        readonly Dictionary<int, uint> diceIds = new();
        readonly Dictionary<PlayerSlot, uint> characterIds = new();

        public void BeginMotionRelay(
            OnlineNetMessenger netMessenger,
            DiceRegistry diceRegistry,
            DiceMatchOwnershipContext matchOwnership) {
            messenger = netMessenger;
            registry = diceRegistry;
            ownershipContext = matchOwnership;

            // Subscribe before initial spawn so SpawnEmerge/Fall events are not dropped.
            DiceVisualMotionHub.MotionStarted -= OnDiceVisualMotionStarted;
            DiceVisualMotionHub.MotionStarted += OnDiceVisualMotionStarted;
        }

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
            sentInitialBoardSnapshot = false;
            characterSnapshotTimer = 0f;
            attackQueueResyncTimer = 0f;
            TrySendInitialBoardSnapshot();
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

            if (!sentInitialBoardSnapshot) {
                TrySendInitialBoardSnapshot();
            }

            // Character poses only after the board is seeded on the client (small payload).
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

        void TrySendInitialBoardSnapshot() {
            if (messenger == null || sentInitialBoardSnapshot) {
                return;
            }

            if (!messenger.HasRemoteClients()) {
                return;
            }

            messenger.SendSnapshotToClients(BuildFullBoardSnapshot());
            sentInitialBoardSnapshot = true;
            characterSnapshotTimer = 0f;
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

        OnlineMatchSnapshot BuildFullBoardSnapshot() {
            var entities = BuildCharacterEntities();
            AppendDiceEntities(entities);
            return new OnlineMatchSnapshot {
                Entities = entities.ToArray()
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

        void AppendDiceEntities(List<OnlineTransformSnapshot> entities) {
            if (registry == null) {
                return;
            }

            foreach (var dice in registry.AllDice) {
                if (dice == null) {
                    continue;
                }

                var id = EnsureDiceId(dice);
                var state = dice.CurrentState;
                var catalogSide = ResolveCatalogSide(dice);

                entities.Add(new OnlineTransformSnapshot {
                    Id = id,
                    Kind = (byte)state.Kind,
                    Flags = (byte)(OnlineTransformSnapshot.FlagDice | OnlineTransformSnapshot.FlagActive),
                    CatalogSide = (byte)catalogSide,
                    GridX = (short)state.GridPos.x,
                    GridY = (short)state.GridPos.y,
                    Tier = (byte)state.Tier,
                    TopFace = (byte)Mathf.Clamp(state.Orientation.Top, 1, 6),
                    NorthFace = (byte)Mathf.Clamp(state.Orientation.North, 1, 6),
                    EastFace = (byte)Mathf.Clamp(state.Orientation.East, 1, 6)
                });
            }
        }
    }
}
