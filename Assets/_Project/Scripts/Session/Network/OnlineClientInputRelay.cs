using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay.Input;
using Unity.Netcode;
using UnityEngine;
using GameCharacterController = DiceGame.Gameplay.CharacterController;

namespace DiceGame.Session.Network
{
    /// <summary>
    /// Legacy: client→host input only. Prefer <see cref="OnlineDualSimInputBinder"/>.
    /// </summary>
    public sealed class OnlineClientInputRelay : MonoBehaviour
    {
        OnlineNetMessenger messenger;
        CharacterInputReader localInputReader;
        float inputTimer;
        Vector2 lastMove;
        Direction? pendingDirection;

        public void Configure(
            OnlineNetMessenger netMessenger,
            IReadOnlyList<GameCharacterController> spawnedCharacters,
            PlayerSlot localSlot) {
            messenger = netMessenger;
            localInputReader = null;

            if (spawnedCharacters == null) {
                return;
            }

            for (var i = 0; i < spawnedCharacters.Count; i++) {
                var character = spawnedCharacters[i];
                if (character == null || character.PlayerSlot != localSlot) {
                    continue;
                }

                localInputReader = character.GetComponent<CharacterInputReader>();
                break;
            }

            if (localInputReader == null) {
                Debug.LogError(
                    $"OnlineClientInputRelay: CharacterInputReader missing for local slot={localSlot}.");
            }
        }

        void Update() {
            if (messenger == null || localInputReader == null || NetworkManager.Singleton == null) {
                return;
            }

            if (!NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer) {
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
            var payload = OnlineInputPayload.FromSource(
                move,
                lift,
                jump,
                pendingDirection.HasValue,
                pendingDirection ?? Direction.North);
            pendingDirection = null;
            messenger.SendInputToServer(payload);
        }
    }
}
