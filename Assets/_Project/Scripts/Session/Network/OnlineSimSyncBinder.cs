using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Gameplay.Character;
using DiceGame.Gameplay.Input;
using DiceGame.Versus;
using DiceGame.Versus.Core;
using UnityEngine;
using GameCharacterController = DiceGame.Gameplay.CharacterController;

namespace DiceGame.Session.Network
{
    /// <summary>
    /// Full-sim online sync with character-only rollback:
    /// shared seed initial board; host spawn/attack results; bidirectional input;
    /// host character pose corrections + client local resim (no board/dice rollback).
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class OnlineSimSyncBinder : MonoBehaviour
    {
        struct InputHistoryEntry
        {
            public uint Sequence;
            public OnlineInputPayload Payload;
            public float DeltaTime;
        }

        OnlineNetMessenger messenger;
        DiceSpawnSystem spawnSystem;
        VersusAttackController attackController;
        DiceMatchOwnershipContext ownershipContext;
        RemoteNetworkInputSource remoteInput;
        GameCharacterController localCharacter;
        GameCharacterController remoteCharacter;
        CharacterInputReader localInputReader;
        bool isHost;
        float inputTimer;
        float characterStateTimer;
        Vector2 lastMove;
        Direction? pendingDirection;
        uint localInputSequence;
        uint lastRemoteInputSequence;
        uint lastAppliedLocalCorrectionSequence;
        readonly List<GameCharacterController> characters = new();
        readonly InputHistoryEntry[] inputHistory =
            new InputHistoryEntry[OnlineSessionConstants.CharacterRollbackHistorySize];
        int inputHistoryCount;
        int inputHistoryStart;

        public void Configure(
            OnlineNetMessenger netMessenger,
            DiceSpawnSystem diceSpawnSystem,
            DiceMatchOwnershipContext matchOwnership,
            VersusAttackController versusAttackController,
            IReadOnlyList<GameCharacterController> spawnedCharacters,
            bool host) {
            messenger = netMessenger;
            spawnSystem = diceSpawnSystem;
            ownershipContext = matchOwnership;
            attackController = versusAttackController;
            isHost = host;
            characters.Clear();
            if (spawnedCharacters != null) {
                characters.AddRange(spawnedCharacters);
            }

            UnsubscribeAll();
            BindRemoteAndLocalCharacters();

            if (messenger != null) {
                if (isHost) {
                    messenger.InputReceived += OnClientInputReceived;
                } else {
                    messenger.HostInputReceived += OnHostInputReceived;
                    messenger.DiceSpawnReceived += OnDiceSpawnReceived;
                    messenger.AttackQueueReceived += OnAttackQueueReceived;
                    messenger.CharacterStateReceived += OnCharacterStateReceived;
                }
            }

            if (isHost && spawnSystem != null) {
                spawnSystem.NetworkSpawnEmitted -= OnHostNetworkSpawnEmitted;
                spawnSystem.NetworkSpawnEmitted += OnHostNetworkSpawnEmitted;
                spawnSystem.EmitNetworkSpawns = true;
            }

            if (!isHost && attackController != null) {
                attackController.SetNetworkFollowerMode(true);
            }

            if (isHost && attackController != null) {
                attackController.QueuePresentationChanged -= OnHostAttackQueueChanged;
                attackController.QueuePresentationChanged += OnHostAttackQueueChanged;
                OnHostAttackQueueChanged();
            }
        }

        void OnDestroy() {
            UnsubscribeAll();
        }

        void UnsubscribeAll() {
            if (messenger != null) {
                messenger.InputReceived -= OnClientInputReceived;
                messenger.HostInputReceived -= OnHostInputReceived;
                messenger.DiceSpawnReceived -= OnDiceSpawnReceived;
                messenger.AttackQueueReceived -= OnAttackQueueReceived;
                messenger.CharacterStateReceived -= OnCharacterStateReceived;
            }

            if (spawnSystem != null) {
                spawnSystem.NetworkSpawnEmitted -= OnHostNetworkSpawnEmitted;
            }

            if (attackController != null) {
                attackController.QueuePresentationChanged -= OnHostAttackQueueChanged;
            }
        }

        void Update() {
            CaptureAndSendLocalInput();
            if (isHost) {
                TickHostCharacterStateSync();
            }
        }

        void BindRemoteAndLocalCharacters() {
            var localSlot = isHost ? PlayerSlot.Player1 : PlayerSlot.Player2;
            var remoteSlot = isHost ? PlayerSlot.Player2 : PlayerSlot.Player1;

            localCharacter = null;
            remoteCharacter = null;
            remoteInput = null;
            foreach (var character in characters) {
                if (character == null) {
                    continue;
                }

                if (character.PlayerSlot == localSlot) {
                    localCharacter = character;
                    localInputReader = character.GetComponent<CharacterInputReader>();
                    continue;
                }

                if (character.PlayerSlot != remoteSlot) {
                    continue;
                }

                remoteCharacter = character;
                var humanReader = character.GetComponent<CharacterInputReader>();
                if (humanReader != null) {
                    humanReader.enabled = false;
                }

                remoteInput = character.GetComponent<RemoteNetworkInputSource>();
                if (remoteInput == null) {
                    remoteInput = character.gameObject.AddComponent<RemoteNetworkInputSource>();
                }

                character.SetInputSource(remoteInput);
            }

            if (localCharacter == null) {
                Debug.LogError($"OnlineSimSyncBinder: local character missing for slot={localSlot}.");
            }

            if (remoteInput == null) {
                Debug.LogError($"OnlineSimSyncBinder: remote character missing for slot={remoteSlot}.");
            }
        }

        void CaptureAndSendLocalInput() {
            if (messenger == null || localInputReader == null || !localInputReader.enabled) {
                return;
            }

            var delta = Time.deltaTime;
            if (delta <= 0f) {
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

            localInputSequence++;
            var historyPayload = OnlineInputPayload.FromSource(
                move,
                lift,
                jump,
                pendingDirection.HasValue,
                pendingDirection ?? Direction.North,
                localInputSequence);
            PushInputHistory(historyPayload, delta);

            var shouldSend = inputTimer >= OnlineSessionConstants.InputSendIntervalSeconds
                || lift
                || jump
                || hasDirection
                || (move - lastMove).sqrMagnitude >= 0.0001f;
            if (!shouldSend) {
                return;
            }

            inputTimer = 0f;
            lastMove = move;
            var payload = OnlineInputPayload.FromSource(
                move,
                lift,
                jump,
                pendingDirection.HasValue,
                pendingDirection ?? Direction.North,
                localInputSequence);
            pendingDirection = null;

            if (isHost) {
                messenger.SendInputToClients(payload);
            } else {
                messenger.SendInputToServer(payload);
            }
        }

        void TickHostCharacterStateSync() {
            if (messenger == null) {
                return;
            }

            characterStateTimer += Time.unscaledDeltaTime;
            if (characterStateTimer < OnlineSessionConstants.CharacterStateSendIntervalSeconds) {
                return;
            }

            characterStateTimer = 0f;
            var states = new List<OnlineCharacterStatePayload>(characters.Count);
            for (var i = 0; i < characters.Count; i++) {
                var character = characters[i];
                if (character == null) {
                    continue;
                }

                var sequence = character.PlayerSlot == PlayerSlot.Player1
                    ? localInputSequence
                    : lastRemoteInputSequence;
                if (sequence == 0) {
                    sequence = 1;
                }

                states.Add(OnlineCharacterStatePayload.FromCharacter(sequence, character));
            }

            if (states.Count == 0) {
                return;
            }

            messenger.SendCharacterStateToClients(new OnlineCharacterStateBatch {
                States = states.ToArray()
            });
        }

        void OnClientInputReceived(ulong senderClientId, OnlineInputPayload payload) {
            if (payload.Sequence != 0 && payload.Sequence <= lastRemoteInputSequence) {
                return;
            }

            lastRemoteInputSequence = payload.Sequence != 0 ? payload.Sequence : lastRemoteInputSequence + 1;
            remoteInput?.ApplyPayload(payload);
        }

        void OnHostInputReceived(OnlineInputPayload payload) {
            if (payload.Sequence != 0 && payload.Sequence <= lastRemoteInputSequence) {
                return;
            }

            lastRemoteInputSequence = payload.Sequence != 0 ? payload.Sequence : lastRemoteInputSequence + 1;
            remoteInput?.ApplyPayload(payload);
        }

        void OnCharacterStateReceived(OnlineCharacterStateBatch batch) {
            if (isHost || batch.States == null) {
                return;
            }

            for (var i = 0; i < batch.States.Length; i++) {
                ApplyCharacterCorrection(batch.States[i]);
            }
        }

        void ApplyCharacterCorrection(OnlineCharacterStatePayload payload) {
            var slot = payload.PlayerSlot;
            if (localCharacter != null && localCharacter.PlayerSlot == slot) {
                RollbackLocalCharacter(payload);
                return;
            }

            if (remoteCharacter != null && remoteCharacter.PlayerSlot == slot) {
                // Remote seat: snap to host pose (no local input history to resim).
                remoteCharacter.ApplyRollbackState(new CharacterRollbackState {
                    Sequence = payload.Sequence,
                    Position = payload.Position,
                    Rotation = payload.Rotation,
                    Speed = payload.Speed,
                    IsBusy = payload.IsBusy
                });
            }
        }

        void RollbackLocalCharacter(OnlineCharacterStatePayload payload) {
            if (localCharacter == null) {
                return;
            }

            if (payload.Sequence != 0 && payload.Sequence <= lastAppliedLocalCorrectionSequence) {
                return;
            }

            lastAppliedLocalCorrectionSequence = payload.Sequence;

            // Busy phases are not character-surface-resim safe; snap only.
            localCharacter.ApplyRollbackState(new CharacterRollbackState {
                Sequence = payload.Sequence,
                Position = payload.Position,
                Rotation = payload.Rotation,
                Speed = payload.Speed,
                IsBusy = payload.IsBusy
            });

            if (payload.IsBusy || localCharacter.IsRollbackBusy) {
                return;
            }

            var stepDt = OnlineSessionConstants.InputSendIntervalSeconds;
            for (var i = 0; i < inputHistoryCount; i++) {
                var entry = inputHistory[(inputHistoryStart + i) % inputHistory.Length];
                if (entry.Sequence <= payload.Sequence) {
                    continue;
                }

                localCharacter.ResimulateSurfaceStep(
                    entry.Payload.Move,
                    entry.DeltaTime > 0f ? entry.DeltaTime : stepDt);
            }
        }

        void PushInputHistory(OnlineInputPayload payload, float deltaTime) {
            var index = (inputHistoryStart + inputHistoryCount) % inputHistory.Length;
            if (inputHistoryCount < inputHistory.Length) {
                inputHistoryCount++;
            } else {
                inputHistoryStart = (inputHistoryStart + 1) % inputHistory.Length;
                index = (inputHistoryStart + inputHistoryCount - 1) % inputHistory.Length;
            }

            inputHistory[index] = new InputHistoryEntry {
                Sequence = payload.Sequence,
                Payload = payload,
                DeltaTime = deltaTime
            };
        }

        void OnHostNetworkSpawnEmitted(
            DiceController dice,
            byte reason,
            bool useSpawnAppear,
            bool forceFallFromAbove) {
            if (messenger == null || dice == null) {
                return;
            }

            var command = OnlineDiceSpawnCommand.FromDice(dice, reason, useSpawnAppear, forceFallFromAbove);
            if (ownershipContext != null && ownershipContext.TryGetOwner(dice, out var owner)) {
                command.OwnerSlot = (byte)owner;
            }

            messenger.SendDiceSpawnToClients(command);
        }

        void OnDiceSpawnReceived(OnlineDiceSpawnCommand command) {
            if (spawnSystem == null) {
                return;
            }

            var owner = command.Owner;
            var spawnSettings = ResolveSpawnSettings(owner);
            if (spawnSettings == null) {
                Debug.LogError("OnlineSimSyncBinder: spawn settings missing for network spawn.");
                return;
            }

            spawnSystem.ApplyNetworkSpawn(
                new Vector2Int(command.GridX, command.GridY),
                (DiceStackTier)command.Tier,
                (DiceKind)command.Kind,
                new DiceOrientation(command.TopFace, command.NorthFace, command.EastFace),
                owner,
                spawnSettings,
                command.UseSpawnAppear != 0,
                command.ForceFallFromAbove != 0);
        }

        void OnHostAttackQueueChanged() {
            if (messenger == null || attackController == null) {
                return;
            }

            messenger.SendAttackQueueToClients(new OnlineAttackQueueSnapshot {
                Player1Volleys = ToNetworkVolleys(attackController.GetPendingVolleys(PlayerSlot.Player1)),
                Player2Volleys = ToNetworkVolleys(attackController.GetPendingVolleys(PlayerSlot.Player2))
            });
        }

        void OnAttackQueueReceived(OnlineAttackQueueSnapshot queueSnapshot) {
            if (attackController == null) {
                return;
            }

            attackController.ApplyNetworkQueuePresentation(
                ToVolleys(queueSnapshot.Player1Volleys),
                ToVolleys(queueSnapshot.Player2Volleys));
        }

        DiceSpawnSettings ResolveSpawnSettings(PlayerSlot owner) {
            if (spawnSystem != null && spawnSystem.TryGetSpawnSettings(owner, out var settings)) {
                return settings;
            }

            return null;
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

        static List<AttackVolley> ToVolleys(OnlineAttackVolleyPayload[] payloads) {
            var result = new List<AttackVolley>(payloads?.Length ?? 0);
            if (payloads == null) {
                return result;
            }

            for (var i = 0; i < payloads.Length; i++) {
                result.Add(payloads[i].ToVolley());
            }

            return result;
        }
    }
}
