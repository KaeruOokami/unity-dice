using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Gameplay.Input;
using DiceGame.Placement;
using UnityEngine;
using GameCharacterController = DiceGame.Gameplay.CharacterController;

namespace DiceGame.Session.Network
{
    /// <summary>
    /// Delayed lockstep dual-sim: shared inputs + fixed-tick gameplay.
    /// Hash compare detects DESYNC; auto board snap is off by default.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class OnlineDualSimInputBinder : MonoBehaviour
    {
        OnlineNetMessenger messenger;
        DiceRegistry registry;
        DiceMatchOwnershipContext ownershipContext;
        DiceSpawnSystem spawnSystem;
        OnlineEntityIdMap entityIds;
        readonly List<GameCharacterController> characters = new();
        CharacterInputReader localHardwareInput;
        LockstepTickInputSource localTickInput;
        LockstepTickInputSource remoteTickInput;
        GameCharacterController localCharacter;
        GameCharacterController remoteCharacter;
        GameCharacterController player1Character;
        GameCharacterController player2Character;
        readonly OnlineLockstepInputBuffer inputBuffer =
            new(OnlineSessionConstants.LockstepInputBufferTicks);
        readonly Dictionary<uint, uint> localHashes = new();
        readonly Dictionary<uint, uint> remoteHashes = new();
        PlayerSlot localSlot;
        PlayerSlot remoteSlot;
        bool isHost;
        uint currentTick;
        uint localInputSequence;
        float simulatorAccumulator;
        bool pendingLift;
        bool pendingJump;
        bool pendingHasDirection;
        Direction pendingDirection;
        float nextStallLogTime;
        float nextReadySendTime;
        float nextResendTime;
        float nextResyncAllowedTime;
        bool peerReady;
        bool repliedToPeerReady;
        bool prefillDone;
        bool applyingResync;

        public void Configure(
            OnlineNetMessenger netMessenger,
            IReadOnlyList<GameCharacterController> spawnedCharacters,
            PlayerSlot localPlayerSlot,
            bool host,
            DiceRegistry diceRegistry,
            DiceMatchOwnershipContext matchOwnership,
            DiceSpawnSystem diceSpawnSystem) {
            messenger = netMessenger;
            registry = diceRegistry;
            ownershipContext = matchOwnership;
            spawnSystem = diceSpawnSystem;
            localSlot = localPlayerSlot;
            remoteSlot = localPlayerSlot == PlayerSlot.Player1
                ? PlayerSlot.Player2
                : PlayerSlot.Player1;
            isHost = host;
            currentTick = 0;
            localInputSequence = 0;
            simulatorAccumulator = 0f;
            pendingLift = false;
            pendingJump = false;
            pendingHasDirection = false;
            pendingDirection = Direction.North;
            nextStallLogTime = 0f;
            nextReadySendTime = 0f;
            nextResendTime = 0f;
            nextResyncAllowedTime = 0f;
            peerReady = false;
            repliedToPeerReady = false;
            prefillDone = false;
            applyingResync = false;
            localHashes.Clear();
            remoteHashes.Clear();
            inputBuffer.Clear();

            characters.Clear();
            if (spawnedCharacters != null) {
                characters.AddRange(spawnedCharacters);
            }

            entityIds = new OnlineEntityIdMap();
            entityIds.RebuildFromSeed(characters, registry, ownershipContext);

            UnsubscribeMessenger();
            BindCharacters(characters);

            if (messenger == null) {
                Debug.LogError("OnlineDualSimInputBinder.Configure: messenger is null.");
                return;
            }

            GameplaySimClock.SetActive(true);

            if (isHost) {
                messenger.InputReceived += OnClientInputReceived;
                messenger.LockstepReadyFromClient += OnLockstepReadyFromClient;
                messenger.SimHashReceived += OnSimHashReceived;
            } else {
                messenger.HostInputReceived += OnHostInputReceived;
                messenger.LockstepReadyReceived += OnLockstepReadyFromHost;
                messenger.SimResyncReceived += OnSimResyncReceived;
            }

            SendLockstepReady();
            nextReadySendTime = Time.realtimeSinceStartup
                + OnlineSessionConstants.LockstepReadyRetryIntervalSeconds;
            Debug.Log(
                $"OnlineDualSimInputBinder: listening slot={localSlot} host={isHost}; " +
                "waiting for LockstepReady before Prefill.");
        }

        void OnDisable() {
            GameplaySimClock.SetActive(false);
        }

        void OnDestroy() {
            GameplaySimClock.SetActive(false);
            UnsubscribeMessenger();
        }

        void UnsubscribeMessenger() {
            if (messenger == null) {
                return;
            }

            messenger.InputReceived -= OnClientInputReceived;
            messenger.HostInputReceived -= OnHostInputReceived;
            messenger.LockstepReadyFromClient -= OnLockstepReadyFromClient;
            messenger.LockstepReadyReceived -= OnLockstepReadyFromHost;
            messenger.SimHashReceived -= OnSimHashReceived;
            messenger.SimResyncReceived -= OnSimResyncReceived;
        }

        void BindCharacters(IReadOnlyList<GameCharacterController> spawnedCharacters) {
            localHardwareInput = null;
            localTickInput = null;
            remoteTickInput = null;
            localCharacter = null;
            remoteCharacter = null;
            player1Character = null;
            player2Character = null;

            if (spawnedCharacters == null) {
                Debug.LogError("OnlineDualSimInputBinder: spawnedCharacters is null.");
                return;
            }

            for (var i = 0; i < spawnedCharacters.Count; i++) {
                var character = spawnedCharacters[i];
                if (character == null) {
                    continue;
                }

                character.SetSuppressBoardMutation(false);
                character.SetLockstepSimulation(true);

                if (character.PlayerSlot == PlayerSlot.Player1) {
                    player1Character = character;
                } else if (character.PlayerSlot == PlayerSlot.Player2) {
                    player2Character = character;
                }

                if (character.PlayerSlot == localSlot) {
                    localCharacter = character;
                    localHardwareInput = character.GetComponent<CharacterInputReader>();
                    if (localHardwareInput != null) {
                        localHardwareInput.enabled = true;
                    }

                    localTickInput = character.GetComponent<LockstepTickInputSource>();
                    if (localTickInput == null) {
                        localTickInput = character.gameObject.AddComponent<LockstepTickInputSource>();
                    }

                    character.SetInputSource(localTickInput);
                } else if (character.PlayerSlot == remoteSlot) {
                    remoteCharacter = character;
                    var humanReader = character.GetComponent<CharacterInputReader>();
                    if (humanReader != null) {
                        humanReader.enabled = false;
                    }

                    remoteTickInput = character.GetComponent<LockstepTickInputSource>();
                    if (remoteTickInput == null) {
                        remoteTickInput = character.gameObject.AddComponent<LockstepTickInputSource>();
                    }

                    character.SetInputSource(remoteTickInput);
                }
            }

            if (localCharacter == null || localTickInput == null || localHardwareInput == null) {
                Debug.LogError(
                    $"OnlineDualSimInputBinder: local lockstep wiring failed for slot={localSlot}.");
            }

            if (remoteCharacter == null || remoteTickInput == null) {
                Debug.LogError(
                    $"OnlineDualSimInputBinder: remote lockstep wiring failed for slot={remoteSlot}.");
            }

            if (player1Character == null || player2Character == null) {
                Debug.LogError("OnlineDualSimInputBinder: need both Player1 and Player2 characters.");
            }
        }

        void PrefillLocalDelayWindowFrom(uint startTick) {
            var end = startTick + (uint)OnlineSessionConstants.InputDelayTicks;
            for (var tick = startTick; tick < end; tick++) {
                CommitLocalInputForTick(tick);
            }
        }

        void Update() {
            AccumulateHardwarePulses();

            if (applyingResync) {
                return;
            }

            if (!peerReady) {
                TickLockstepReadyHandshake();
                return;
            }

            if (!prefillDone) {
                PrefillLocalDelayWindowFrom(0);
                prefillDone = true;
                nextResendTime = 0f;
                Debug.Log("OnlineDualSimInputBinder: peer ready; Prefill sent, lockstep running.");
            }

            GameplaySimClock.BeginUnityFrame();
            ResendLocalInputWindowIfDue();
            TickLockstep();
        }

        void TickLockstepReadyHandshake() {
            var now = Time.realtimeSinceStartup;
            if (now < nextReadySendTime) {
                return;
            }

            nextReadySendTime = now + OnlineSessionConstants.LockstepReadyRetryIntervalSeconds;
            SendLockstepReady();
        }

        void MarkPeerReadyAndReply() {
            var becameReady = !peerReady;
            peerReady = true;
            if (repliedToPeerReady) {
                return;
            }

            repliedToPeerReady = true;
            SendLockstepReady();
            if (becameReady) {
                Debug.Log($"OnlineDualSimInputBinder: LockstepReady from peer (slot={localSlot}).");
            }
        }

        void OnLockstepReadyFromClient(ulong senderClientId) {
            MarkPeerReadyAndReply();
        }

        void OnLockstepReadyFromHost() {
            MarkPeerReadyAndReply();
        }

        void SendLockstepReady() {
            if (messenger == null) {
                return;
            }

            if (isHost) {
                messenger.SendLockstepReadyToClients();
            } else {
                messenger.SendLockstepReadyToServer();
            }
        }

        void AccumulateHardwarePulses() {
            if (localHardwareInput == null) {
                return;
            }

            if (localHardwareInput.WasLiftPressedThisFrame()) {
                pendingLift = true;
            }

            if (localHardwareInput.WasJumpPressedThisFrame()) {
                pendingJump = true;
            }

            if (localHardwareInput.TryGetDirectionPressedThisFrame(out var direction)) {
                pendingHasDirection = true;
                pendingDirection = direction;
            }
        }

        void ResendLocalInputWindowIfDue() {
            var now = Time.realtimeSinceStartup;
            if (now < nextResendTime) {
                return;
            }

            nextResendTime = now + OnlineSessionConstants.LockstepInputResendIntervalSeconds;
            var delay = (uint)OnlineSessionConstants.InputDelayTicks;
            var endTick = currentTick + delay;
            for (var tick = currentTick; tick <= endTick; tick++) {
                if (!inputBuffer.TryGet(localSlot, tick, out var payload)) {
                    continue;
                }

                SendLocalInput(payload);
            }
        }

        void TickLockstep() {
            if (messenger == null || localCharacter == null || remoteCharacter == null) {
                return;
            }

            var simDt = OnlineSessionConstants.SimTickSeconds;
            simulatorAccumulator += Time.deltaTime;
            var maxCredit = simDt * OnlineSessionConstants.LockstepMaxStepsPerFrame;
            if (simulatorAccumulator > maxCredit) {
                simulatorAccumulator = maxCredit;
            }

            var steps = 0;
            while (simulatorAccumulator >= simDt
                && steps < OnlineSessionConstants.LockstepMaxStepsPerFrame) {
                var scheduleTick = currentTick + (uint)OnlineSessionConstants.InputDelayTicks;
                if (!inputBuffer.Has(localSlot, scheduleTick)) {
                    CommitLocalInputForTick(scheduleTick);
                }

                if (!inputBuffer.HasBoth(currentTick)) {
                    nextResendTime = 0f;
                    LogStallThrottled();
                    break;
                }

                StepSimulationTick(simDt);
                simulatorAccumulator -= simDt;
                steps++;
            }
        }

        void StepSimulationTick(float simDt) {
            if (!inputBuffer.TryGet(PlayerSlot.Player1, currentTick, out var p1)
                || !inputBuffer.TryGet(PlayerSlot.Player2, currentTick, out var p2)) {
                Debug.LogError($"OnlineDualSimInputBinder: missing inputs for tick={currentTick}.");
                return;
            }

            GameplaySimClock.BeginStep(simDt);
            try {
                ApplyTickInput(PlayerSlot.Player1, p1);
                ApplyTickInput(PlayerSlot.Player2, p2);

                player1Character.SimulateLockstepFrame(simDt);
                player2Character.SimulateLockstepFrame(simDt);

                currentTick++;
                inputBuffer.DiscardBefore(
                    currentTick > 32 ? currentTick - 32 : 0u);

                MaybeEmitSimHash();
            } finally {
                GameplaySimClock.EndStep();
            }
        }

        void MaybeEmitSimHash() {
            if (currentTick == 0
                || currentTick % (uint)OnlineSessionConstants.LockstepHashIntervalTicks != 0) {
                return;
            }

            var hash = OnlineSimStateHasher.Compute(
                currentTick,
                characters,
                registry,
                ownershipContext);
            localHashes[currentTick] = hash;
            TrimHashMap(localHashes);

            if (!isHost) {
                messenger.SendSimHashToServer(new OnlineSimHashPayload {
                    Tick = currentTick,
                    Hash = hash
                });
                return;
            }

            TryCompareHashes(currentTick);
        }

        void OnSimHashReceived(OnlineSimHashPayload payload) {
            if (!isHost) {
                return;
            }

            remoteHashes[payload.Tick] = payload.Hash;
            TrimHashMap(remoteHashes);
            TryCompareHashes(payload.Tick);
        }

        void TryCompareHashes(uint tick) {
            if (!localHashes.TryGetValue(tick, out var local)
                || !remoteHashes.TryGetValue(tick, out var remote)) {
                return;
            }

            localHashes.Remove(tick);
            remoteHashes.Remove(tick);
            if (local == remote) {
                return;
            }

            Debug.LogError(
                $"OnlineDualSimInputBinder: DESYNC tick={tick} hostHash={local:X8} clientHash={remote:X8}");
            if (OnlineSessionConstants.LockstepAutoResyncEnabled) {
                SendHostResync();
            }
        }

        void SendHostResync() {
            if (!isHost || messenger == null) {
                return;
            }

            var now = Time.realtimeSinceStartup;
            if (now < nextResyncAllowedTime) {
                return;
            }

            nextResyncAllowedTime = now + OnlineSessionConstants.LockstepResyncCooldownSeconds;
            var payload = new OnlineSimResyncPayload {
                Tick = currentTick,
                Entities = OnlineSimBoardSnapshotBuilder.Build(
                    characters,
                    registry,
                    ownershipContext,
                    entityIds)
            };
            messenger.SendSimResyncToClients(payload);
            nextResendTime = 0f;
            Debug.Log(
                $"OnlineDualSimInputBinder: sent resync tick={currentTick} " +
                $"entities={payload.Entities?.Length ?? 0}");
        }

        void OnSimResyncReceived(OnlineSimResyncPayload payload) {
            if (isHost) {
                return;
            }

            applyingResync = true;
            try {
                OnlineSimResyncApplier.Apply(
                    payload,
                    characters,
                    registry,
                    ownershipContext,
                    spawnSystem,
                    entityIds);
                AlignAfterResync(payload.Tick);
                Debug.Log(
                    $"OnlineDualSimInputBinder: applied resync tick={payload.Tick} " +
                    $"entities={payload.Entities?.Length ?? 0}");
            } finally {
                applyingResync = false;
            }
        }

        void AlignAfterResync(uint tick) {
            currentTick = tick;
            simulatorAccumulator = 0f;
            inputBuffer.Clear();
            localHashes.Clear();
            remoteHashes.Clear();
            pendingLift = false;
            pendingJump = false;
            pendingHasDirection = false;
            PrefillLocalDelayWindowFrom(tick);
            nextResendTime = 0f;
            entityIds.RebuildFromSeed(characters, registry, ownershipContext);
        }

        void ApplyTickInput(PlayerSlot slot, OnlineInputPayload payload) {
            if (slot == localSlot) {
                localTickInput.SetTickInput(payload);
            } else {
                remoteTickInput.SetTickInput(payload);
            }
        }

        void CommitLocalInputForTick(uint tick) {
            if (inputBuffer.Has(localSlot, tick)) {
                return;
            }

            var move = localHardwareInput != null
                ? localHardwareInput.ReadMove()
                : Vector2.zero;
            localInputSequence++;
            var payload = OnlineInputPayload.FromSource(
                move,
                pendingLift,
                pendingJump,
                pendingHasDirection,
                pendingHasDirection ? pendingDirection : Direction.North,
                localInputSequence,
                tick);
            pendingLift = false;
            pendingJump = false;
            pendingHasDirection = false;

            inputBuffer.Set(localSlot, tick, payload);
            SendLocalInput(payload);
        }

        void SendLocalInput(OnlineInputPayload payload) {
            if (messenger == null) {
                return;
            }

            if (isHost) {
                messenger.SendInputToClients(payload);
            } else {
                messenger.SendInputToServer(payload);
            }
        }

        void OnClientInputReceived(ulong senderClientId, OnlineInputPayload payload) {
            StoreRemoteInput(payload);
        }

        void OnHostInputReceived(OnlineInputPayload payload) {
            StoreRemoteInput(payload);
        }

        void StoreRemoteInput(OnlineInputPayload payload) {
            if (inputBuffer.Has(remoteSlot, payload.Tick)) {
                return;
            }

            inputBuffer.Set(remoteSlot, payload.Tick, payload);
        }

        static void TrimHashMap(Dictionary<uint, uint> map) {
            if (map.Count <= 8) {
                return;
            }

            var keys = new List<uint>(map.Keys);
            keys.Sort();
            var removeCount = keys.Count - 8;
            for (var i = 0; i < removeCount; i++) {
                map.Remove(keys[i]);
            }
        }

        void LogStallThrottled() {
            var now = Time.realtimeSinceStartup;
            if (now < nextStallLogTime) {
                return;
            }

            nextStallLogTime = now + 2f;
            var hasLocal = inputBuffer.Has(localSlot, currentTick);
            var hasRemote = inputBuffer.Has(remoteSlot, currentTick);
            Debug.LogWarning(
                $"OnlineDualSimInputBinder: lockstep stall tick={currentTick} " +
                $"local={hasLocal} remote={hasRemote} delay={OnlineSessionConstants.InputDelayTicks}");
        }
    }
}
