using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay.Character;
using DiceGame.Gameplay.Input;
using Unity.Netcode;
using UnityEngine;
using GameCharacterController = DiceGame.Gameplay.CharacterController;

namespace DiceGame.Session.Network
{
    /// <summary>
    /// Phase C client character sync:
    /// - send local input to host
    /// - drive remote character from host input
    /// - apply sparse host pose corrections (snap if far, idle soft blend for local)
    /// </summary>
    public sealed class OnlineClientCharacterBinder : MonoBehaviour
    {
        OnlineNetMessenger messenger;
        CharacterInputReader localInputReader;
        RemoteNetworkInputSource remoteInput;
        GameCharacterController localCharacter;
        GameCharacterController remoteCharacter;
        PlayerSlot localSlot;
        PlayerSlot remoteSlot;
        float inputTimer;
        Vector2 lastMove;
        Direction? pendingDirection;

        public void Configure(
            OnlineNetMessenger netMessenger,
            IReadOnlyList<GameCharacterController> spawnedCharacters,
            PlayerSlot localPlayerSlot) {
            messenger = netMessenger;
            localSlot = localPlayerSlot;
            remoteSlot = localPlayerSlot == PlayerSlot.Player1
                ? PlayerSlot.Player2
                : PlayerSlot.Player1;
            localCharacter = null;
            remoteCharacter = null;
            localInputReader = null;
            remoteInput = null;

            if (spawnedCharacters != null) {
                for (var i = 0; i < spawnedCharacters.Count; i++) {
                    var character = spawnedCharacters[i];
                    if (character == null) {
                        continue;
                    }

                    if (character.PlayerSlot == localSlot) {
                        localCharacter = character;
                        localInputReader = character.GetComponent<CharacterInputReader>();
                    } else if (character.PlayerSlot == remoteSlot) {
                        remoteCharacter = character;
                    }
                }
            }

            if (localInputReader == null) {
                Debug.LogError(
                    $"OnlineClientCharacterBinder: local input missing for slot={localSlot}.");
            }

            BindRemoteCharacterInput();

            if (messenger != null) {
                messenger.HostInputReceived -= OnHostInputReceived;
                messenger.HostInputReceived += OnHostInputReceived;
                messenger.SnapshotChunkReceived -= OnSnapshotChunkReceived;
                messenger.SnapshotChunkReceived += OnSnapshotChunkReceived;
            } else {
                Debug.LogError("OnlineClientCharacterBinder.Configure: messenger is null.");
            }
        }

        void OnDestroy() {
            if (messenger != null) {
                messenger.HostInputReceived -= OnHostInputReceived;
                messenger.SnapshotChunkReceived -= OnSnapshotChunkReceived;
            }
        }

        void BindRemoteCharacterInput() {
            if (remoteCharacter == null) {
                Debug.LogError(
                    $"OnlineClientCharacterBinder: remote character missing for slot={remoteSlot}.");
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

        void OnHostInputReceived(OnlineInputPayload payload) {
            remoteInput?.ApplyPayload(payload);
        }

        void OnSnapshotChunkReceived(OnlineMatchSnapshotChunk chunk) {
            if (chunk.ChunkCount != 1 || chunk.ChunkIndex != 0 || chunk.Entities == null) {
                return;
            }

            for (var i = 0; i < chunk.Entities.Length; i++) {
                var entity = chunk.Entities[i];
                if (!entity.IsActive || !entity.IsCharacter) {
                    continue;
                }

                var slot = (PlayerSlot)entity.Kind;
                if (slot == localSlot) {
                    ApplyLocalCharacterCorrection(entity);
                } else if (slot == remoteSlot) {
                    ApplyRemoteCharacterCorrection(entity);
                }
            }
        }

        void ApplyLocalCharacterCorrection(OnlineTransformSnapshot entity) {
            if (localCharacter == null) {
                return;
            }

            var current = localCharacter.transform.position;
            var error = entity.Position - current;
            var snapDistance = OnlineSessionConstants.SnapshotInterpSnapDistance;
            if (error.sqrMagnitude >= snapDistance * snapDistance) {
                localCharacter.ApplyRollbackState(new CharacterRollbackState {
                    Sequence = 0,
                    Position = entity.Position,
                    Rotation = entity.Rotation,
                    Speed = 0f,
                    IsBusy = false
                });
                return;
            }

            var localSpeed = localCharacter.CaptureRollbackState(0).Speed;
            var moveMagnitude = localInputReader != null
                ? localInputReader.ReadMove().magnitude
                : 0f;
            if (localSpeed > OnlineSessionConstants.LocalCharacterReconcileIdleSpeed
                || moveMagnitude > 0.1f) {
                // Actively predicting — do not tug.
                return;
            }

            var blended = Vector3.Lerp(
                current,
                entity.Position,
                OnlineSessionConstants.LocalCharacterReconcileBlend);
            localCharacter.ApplyRollbackState(new CharacterRollbackState {
                Sequence = 0,
                Position = blended,
                Rotation = entity.Rotation,
                Speed = localSpeed,
                IsBusy = false
            });
        }

        void ApplyRemoteCharacterCorrection(OnlineTransformSnapshot entity) {
            if (remoteCharacter == null) {
                return;
            }

            var current = remoteCharacter.transform.position;
            var error = entity.Position - current;
            var snapDistance = OnlineSessionConstants.SnapshotInterpSnapDistance;
            if (error.sqrMagnitude >= snapDistance * snapDistance) {
                remoteCharacter.ApplyRollbackState(new CharacterRollbackState {
                    Sequence = 0,
                    Position = entity.Position,
                    Rotation = entity.Rotation,
                    Speed = 0f,
                    IsBusy = false
                });
                return;
            }

            // Remote is input-driven; small errors blend gently, large ones already snapped.
            var blended = Vector3.Lerp(
                current,
                entity.Position,
                OnlineSessionConstants.LocalCharacterReconcileBlend);
            if ((blended - current).sqrMagnitude < 0.0001f) {
                return;
            }

            remoteCharacter.ApplyRollbackState(new CharacterRollbackState {
                Sequence = 0,
                Position = blended,
                Rotation = entity.Rotation,
                Speed = remoteCharacter.CaptureRollbackState(0).Speed,
                IsBusy = false
            });
        }
    }
}
