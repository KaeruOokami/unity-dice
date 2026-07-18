using System;
using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay.Input;
using DiceGame.Placement;
using UnityEngine;
using GameCharacterController = DiceGame.Gameplay.CharacterController;

namespace DiceGame.Session.Network
{
    public sealed class OnlineHostMatchBinder : MonoBehaviour
    {
        OnlineNetMessenger messenger;
        RemoteNetworkInputSource remoteInput;
        DiceRegistry registry;
        readonly List<GameCharacterController> characters = new();
        float snapshotTimer;
        uint nextEntityId = 1;
        readonly Dictionary<int, uint> diceIds = new();
        readonly Dictionary<PlayerSlot, uint> characterIds = new();

        public void Configure(
            OnlineNetMessenger netMessenger,
            DiceRegistry diceRegistry,
            IReadOnlyList<GameCharacterController> spawnedCharacters) {
            messenger = netMessenger;
            registry = diceRegistry;
            characters.Clear();
            if (spawnedCharacters != null) {
                characters.AddRange(spawnedCharacters);
            }

            if (messenger != null) {
                messenger.InputReceived += OnInputReceived;
            }

            BindRemotePlayerInput();
            AssignIds();
        }

        void OnDestroy() {
            if (messenger != null) {
                messenger.InputReceived -= OnInputReceived;
            }
        }

        void Update() {
            if (messenger == null || registry == null) {
                return;
            }

            snapshotTimer += Time.unscaledDeltaTime;
            if (snapshotTimer < OnlineSessionConstants.SnapshotSendIntervalSeconds) {
                return;
            }

            snapshotTimer = 0f;
            messenger.SendSnapshotToClients(BuildSnapshot());
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

        OnlineMatchSnapshot BuildSnapshot() {
            var entities = new List<OnlineTransformSnapshot>(64);

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
                    Flags = (byte)(OnlineTransformSnapshot.FlagCharacter | OnlineTransformSnapshot.FlagActive)
                });
            }

            if (registry != null) {
                foreach (var dice in registry.AllDice) {
                    if (dice == null) {
                        continue;
                    }

                    var key = dice.GetInstanceID();
                    if (!diceIds.TryGetValue(key, out var id)) {
                        id = nextEntityId++;
                        diceIds[key] = id;
                    }

                    var view = dice.View;
                    var syncTransform = view != null && view.DiceTransform != null
                        ? view.DiceTransform
                        : dice.transform;

                    entities.Add(new OnlineTransformSnapshot {
                        Id = id,
                        Position = syncTransform.position,
                        Rotation = syncTransform.rotation,
                        Kind = (byte)dice.Kind,
                        Flags = (byte)(OnlineTransformSnapshot.FlagDice | OnlineTransformSnapshot.FlagActive)
                    });
                }
            }

            return new OnlineMatchSnapshot {
                Entities = entities.ToArray()
            };
        }
    }
}
