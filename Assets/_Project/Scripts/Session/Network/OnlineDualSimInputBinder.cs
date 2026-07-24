using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay.Input;
using UnityEngine;
using GameCharacterController = DiceGame.Gameplay.CharacterController;

namespace DiceGame.Session.Network
{
    /// <summary>
    /// Phase B delayed lockstep: fixed-tick character simulation driven by per-tick inputs.
    /// Waits for peer listening (LockstepReady), then prefills; resends the input window on stall.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class OnlineDualSimInputBinder : MonoBehaviour
    {
        OnlineNetMessenger messenger;
        CharacterInputReader localHardwareInput;
        LockstepTickInputSource localTickInput;
        LockstepTickInputSource remoteTickInput;
        GameCharacterController localCharacter;
        GameCharacterController remoteCharacter;
        GameCharacterController player1Character;
        GameCharacterController player2Character;
        readonly OnlineLockstepInputBuffer inputBuffer =
            new(OnlineSessionConstants.LockstepInputBufferTicks);
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
        bool peerReady;
        bool repliedToPeerReady;
        bool prefillDone;

        public void Configure(
            OnlineNetMessenger netMessenger,
            IReadOnlyList<GameCharacterController> spawnedCharacters,
            PlayerSlot localPlayerSlot,
            bool host) {
            messenger = netMessenger;
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
            peerReady = false;
            repliedToPeerReady = false;
            prefillDone = false;

            UnsubscribeMessenger();
            BindCharacters(spawnedCharacters);

            if (messenger == null) {
                Debug.LogError("OnlineDualSimInputBinder.Configure: messenger is null.");
                return;
            }

            if (isHost) {
                messenger.InputReceived += OnClientInputReceived;
                messenger.LockstepReadyFromClient += OnLockstepReadyFromClient;
            } else {
                messenger.HostInputReceived += OnHostInputReceived;
                messenger.LockstepReadyReceived += OnLockstepReadyFromHost;
            }

            // Subscribe first; Prefill only after peer signals it is listening.
            SendLockstepReady();
            nextReadySendTime = Time.realtimeSinceStartup
                + OnlineSessionConstants.LockstepReadyRetryIntervalSeconds;
            Debug.Log(
                $"OnlineDualSimInputBinder: listening slot={localSlot} host={isHost}; " +
                "waiting for LockstepReady before Prefill.");
        }

        void OnDestroy() {
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

        void PrefillLocalDelayWindow() {
            for (uint tick = 0; tick < OnlineSessionConstants.InputDelayTicks; tick++) {
                CommitLocalInputForTick(tick);
            }
        }

        void Update() {
            AccumulateHardwarePulses();

            if (!peerReady) {
                TickLockstepReadyHandshake();
                return;
            }

            if (!prefillDone) {
                PrefillLocalDelayWindow();
                prefillDone = true;
                nextResendTime = 0f;
                Debug.Log("OnlineDualSimInputBinder: peer ready; Prefill sent, lockstep running.");
            }

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
                    // Keep the delay window filled and force an immediate resend on stall.
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

            ApplyTickInput(PlayerSlot.Player1, p1);
            ApplyTickInput(PlayerSlot.Player2, p2);

            player1Character.SimulateLockstepFrame(simDt);
            player2Character.SimulateLockstepFrame(simDt);

            currentTick++;
            inputBuffer.DiscardBefore(
                currentTick > 32 ? currentTick - 32 : 0u);
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
