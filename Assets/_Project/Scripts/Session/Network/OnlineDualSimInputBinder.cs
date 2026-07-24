using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay.Input;
using UnityEngine;
using GameCharacterController = DiceGame.Gameplay.CharacterController;

namespace DiceGame.Session.Network
{
    /// <summary>
    /// Step A dual-sim: both peers run the full local match simulation.
    /// Network carries bidirectional character input only (no board events / pose chase).
    /// </summary>
    public sealed class OnlineDualSimInputBinder : MonoBehaviour
    {
        OnlineNetMessenger messenger;
        RemoteNetworkInputSource remoteInput;
        CharacterInputReader localInputReader;
        PlayerSlot localSlot;
        bool isHost;
        float inputTimer;
        Vector2 lastMove;
        Direction? pendingDirection;
        uint localInputSequence;
        uint lastRemoteInputSequence;

        public void Configure(
            OnlineNetMessenger netMessenger,
            IReadOnlyList<GameCharacterController> spawnedCharacters,
            PlayerSlot localPlayerSlot,
            bool host) {
            messenger = netMessenger;
            localSlot = localPlayerSlot;
            isHost = host;
            localInputReader = null;
            remoteInput = null;
            inputTimer = 0f;
            lastMove = Vector2.zero;
            pendingDirection = null;
            localInputSequence = 0;
            lastRemoteInputSequence = 0;

            UnsubscribeMessenger();
            BindCharacters(spawnedCharacters);

            if (messenger == null) {
                Debug.LogError("OnlineDualSimInputBinder.Configure: messenger is null.");
                return;
            }

            if (isHost) {
                messenger.InputReceived += OnClientInputReceived;
            } else {
                messenger.HostInputReceived += OnHostInputReceived;
            }

            // Dual-sim: characters must drive the board on both peers.
            if (spawnedCharacters != null) {
                for (var i = 0; i < spawnedCharacters.Count; i++) {
                    spawnedCharacters[i]?.SetSuppressBoardMutation(false);
                }
            }
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
        }

        void BindCharacters(IReadOnlyList<GameCharacterController> spawnedCharacters) {
            var remoteSlot = localSlot == PlayerSlot.Player1
                ? PlayerSlot.Player2
                : PlayerSlot.Player1;

            if (spawnedCharacters == null) {
                Debug.LogError("OnlineDualSimInputBinder: spawnedCharacters is null.");
                return;
            }

            GameCharacterController remoteCharacter = null;
            for (var i = 0; i < spawnedCharacters.Count; i++) {
                var character = spawnedCharacters[i];
                if (character == null) {
                    continue;
                }

                if (character.PlayerSlot == localSlot) {
                    localInputReader = character.GetComponent<CharacterInputReader>();
                    continue;
                }

                if (character.PlayerSlot == remoteSlot) {
                    remoteCharacter = character;
                }
            }

            if (localInputReader == null) {
                Debug.LogError(
                    $"OnlineDualSimInputBinder: local CharacterInputReader missing for slot={localSlot}.");
            }

            if (remoteCharacter == null) {
                Debug.LogError(
                    $"OnlineDualSimInputBinder: remote character missing for slot={remoteSlot}.");
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

        void Update() {
            CaptureAndSendLocalInput();
        }

        void CaptureAndSendLocalInput() {
            if (messenger == null || localInputReader == null || !localInputReader.enabled) {
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
                && (move - lastMove).sqrMagnitude < 0.0001f) {
                return;
            }

            inputTimer = 0f;
            lastMove = move;
            localInputSequence++;
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

        void OnClientInputReceived(ulong senderClientId, OnlineInputPayload payload) {
            ApplyRemoteInput(payload);
        }

        void OnHostInputReceived(OnlineInputPayload payload) {
            ApplyRemoteInput(payload);
        }

        void ApplyRemoteInput(OnlineInputPayload payload) {
            if (payload.Sequence != 0 && payload.Sequence <= lastRemoteInputSequence) {
                return;
            }

            lastRemoteInputSequence = payload.Sequence != 0
                ? payload.Sequence
                : lastRemoteInputSequence + 1;
            remoteInput?.ApplyPayload(payload);
        }
    }
}
