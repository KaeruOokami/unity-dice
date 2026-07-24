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
    /// Phase C host: dice motion events, attack queue, host→client input for P1,
    /// and sparse character pose corrections (not continuous snapshots).
    /// </summary>
    public sealed class OnlineHostMatchBinder : MonoBehaviour
    {
        OnlineNetMessenger messenger;
        RemoteNetworkInputSource remoteInput;
        CharacterInputReader localInputReader;
        DiceRegistry registry;
        DiceMatchOwnershipContext ownershipContext;
        VersusAttackController attackController;
        readonly List<GameCharacterController> characters = new();
        readonly OnlineEntityIdMap entityIds = new();
        readonly Dictionary<PlayerSlot, Vector3> lastSentCharacterPositions = new();
        float characterCorrectTimer;
        float attackQueueResyncTimer;
        float inputTimer;
        Vector2 lastSentMove;
        Direction? pendingDirection;

        public OnlineEntityIdMap EntityIds => entityIds;

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
            BindLocalInputReader();
            entityIds.RebuildFromSeed(characters, registry, ownershipContext);
            lastSentCharacterPositions.Clear();
            characterCorrectTimer = 0f;
            attackQueueResyncTimer = 0f;
            inputTimer = 0f;
            lastSentMove = Vector2.zero;
            pendingDirection = null;

            // Seed the "last sent" poses so the first correction waits for real drift.
            for (var i = 0; i < characters.Count; i++) {
                var character = characters[i];
                if (character == null) {
                    continue;
                }

                lastSentCharacterPositions[character.PlayerSlot] = character.transform.position;
            }
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
            if (!enabled || messenger == null) {
                return;
            }

            TickHostInputSend();
            TickSparseCharacterCorrection();

            if (attackController != null) {
                attackQueueResyncTimer += Time.unscaledDeltaTime;
                if (attackQueueResyncTimer >= OnlineSessionConstants.AttackQueueResyncIntervalSeconds) {
                    attackQueueResyncTimer = 0f;
                    OnAttackQueueChanged();
                }
            }
        }

        void BindLocalInputReader() {
            localInputReader = null;
            foreach (var character in characters) {
                if (character == null || character.PlayerSlot != PlayerSlot.Player1) {
                    continue;
                }

                localInputReader = character.GetComponent<CharacterInputReader>();
                break;
            }

            if (localInputReader == null) {
                Debug.LogError("OnlineHostMatchBinder: Player1 CharacterInputReader not found.");
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

        void TickHostInputSend() {
            if (localInputReader == null || !messenger.HasRemoteClients()) {
                return;
            }

            inputTimer += Time.unscaledDeltaTime;
            var move = localInputReader.ReadMove();
            var lift = localInputReader.WasLiftPressedThisFrame();
            var jump = localInputReader.WasJumpPressedThisFrame();
            var hasDirection = localInputReader.TryGetDirectionPressedThisFrame(out var direction);
            if (hasDirection) {
                pendingDirection = direction;
            }

            if (inputTimer < OnlineSessionConstants.InputSendIntervalSeconds
                && !lift
                && !jump
                && !hasDirection
                && (move - lastSentMove).sqrMagnitude < 0.0001f) {
                return;
            }

            inputTimer = 0f;
            lastSentMove = move;
            var payload = OnlineInputPayload.FromSource(
                move,
                lift,
                jump,
                pendingDirection.HasValue,
                pendingDirection ?? Direction.North);
            pendingDirection = null;
            messenger.SendInputToClients(payload);
        }

        void TickSparseCharacterCorrection() {
            if (registry == null || !messenger.HasRemoteClients()) {
                return;
            }

            characterCorrectTimer += Time.unscaledDeltaTime;
            if (characterCorrectTimer < OnlineSessionConstants.CharacterCorrectCheckIntervalSeconds) {
                return;
            }

            characterCorrectTimer = 0f;

            var minSend = OnlineSessionConstants.CharacterCorrectMinSendDistance;
            var minSendSqr = minSend * minSend;
            var needsSend = false;
            for (var i = 0; i < characters.Count; i++) {
                var character = characters[i];
                if (character == null) {
                    continue;
                }

                var position = character.transform.position;
                if (!lastSentCharacterPositions.TryGetValue(character.PlayerSlot, out var last)
                    || (position - last).sqrMagnitude >= minSendSqr) {
                    needsSend = true;
                    break;
                }
            }

            if (!needsSend) {
                return;
            }

            messenger.SendSnapshotToClients(BuildCharacterSnapshot());
            for (var i = 0; i < characters.Count; i++) {
                var character = characters[i];
                if (character == null) {
                    continue;
                }

                lastSentCharacterPositions[character.PlayerSlot] = character.transform.position;
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
                return System.Array.Empty<OnlineAttackVolleyPayload>();
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

            var entityId = entityIds.EnsureDice(controller);
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

                if (!entityIds.TryGetCharacterId(character.PlayerSlot, out var id)) {
                    continue;
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
