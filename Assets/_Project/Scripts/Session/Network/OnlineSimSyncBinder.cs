using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Gameplay.Input;
using DiceGame.Versus;
using DiceGame.Versus.Core;
using UnityEngine;
using GameCharacterController = DiceGame.Gameplay.CharacterController;

namespace DiceGame.Session.Network
{
    /// <summary>
    /// Deprecated full-sim dual-peer experiment. Prefer host-authoritative
    /// <see cref="OnlineHostMatchBinder"/> + client <see cref="OnlineClientMatchView"/>.
    /// </summary>
    public sealed class OnlineSimSyncBinder : MonoBehaviour
    {
        OnlineNetMessenger messenger;
        DiceSpawnSystem spawnSystem;
        VersusAttackController attackController;
        DiceMatchOwnershipContext ownershipContext;
        RemoteNetworkInputSource remoteInput;
        GameCharacterController localCharacter;
        CharacterInputReader localInputReader;
        bool isHost;
        float inputTimer;
        Vector2 lastMove;
        Direction? pendingDirection;
        readonly List<GameCharacterController> characters = new();

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

            // Experiment: no board snapshots / dice visual-motion relay.
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
        }

        void BindRemoteAndLocalCharacters() {
            var localSlot = isHost ? PlayerSlot.Player1 : PlayerSlot.Player2;
            var remoteSlot = isHost ? PlayerSlot.Player2 : PlayerSlot.Player1;

            localCharacter = null;
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

            if (isHost) {
                messenger.SendInputToClients(payload);
            } else {
                messenger.SendInputToServer(payload);
            }
        }

        void OnClientInputReceived(ulong senderClientId, OnlineInputPayload payload) {
            remoteInput?.ApplyPayload(payload);
        }

        void OnHostInputReceived(OnlineInputPayload payload) {
            remoteInput?.ApplyPayload(payload);
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
