using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Gameplay.Input;
using DiceGame.View;
using Unity.Netcode;
using UnityEngine;

namespace DiceGame.Session.Network
{
    public sealed class OnlineClientMatchView : MonoBehaviour
    {
        OnlineNetMessenger messenger;
        GameObject dicePrefab;
        GameObject characterPrefab;
        Transform proxyRoot;
        readonly Dictionary<uint, Transform> proxies = new();
        CharacterInputReader localInputReader;
        float inputTimer;
        Vector2 lastMove;
        Direction? pendingDirection;

        public void Configure(
            OnlineNetMessenger netMessenger,
            GameObject diceEntityPrefab,
            GameObject characterEntityPrefab,
            PlayerInputSettings playerInputSettings) {
            messenger = netMessenger;
            dicePrefab = diceEntityPrefab;
            characterPrefab = characterEntityPrefab;

            if (proxyRoot == null) {
                var rootObject = new GameObject("OnlineClientProxies");
                proxyRoot = rootObject.transform;
                proxyRoot.SetParent(transform, false);
            }

            if (messenger != null) {
                messenger.SnapshotReceived += OnSnapshotReceived;
            }

            SetupLocalInputCapture(playerInputSettings);
        }

        void OnDestroy() {
            if (messenger != null) {
                messenger.SnapshotReceived -= OnSnapshotReceived;
            }
        }

        void Update() {
            CaptureAndSendInput();
        }

        void SetupLocalInputCapture(PlayerInputSettings settings) {
            var inputObject = new GameObject("OnlineClientLocalInput");
            inputObject.transform.SetParent(transform, false);
            localInputReader = inputObject.AddComponent<CharacterInputReader>();
            if (settings != null) {
                // Client controls Player2 on the host; locally read Player1 bindings for comfort.
                localInputReader.Configure(PlayerSlot.Player1, settings);
            }
        }

        void CaptureAndSendInput() {
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

        void OnSnapshotReceived(OnlineMatchSnapshot snapshot) {
            if (snapshot.Entities == null) {
                return;
            }

            var seen = new HashSet<uint>();
            foreach (var entity in snapshot.Entities) {
                if (!entity.IsActive) {
                    continue;
                }

                seen.Add(entity.Id);
                var proxy = GetOrCreateProxy(entity);
                if (proxy == null) {
                    continue;
                }

                var target = ResolveSyncTransform(proxy, entity.IsDice);
                target.SetPositionAndRotation(entity.Position, entity.Rotation);
            }

            var stale = new List<uint>();
            foreach (var pair in proxies) {
                if (!seen.Contains(pair.Key)) {
                    stale.Add(pair.Key);
                }
            }

            foreach (var id in stale) {
                if (proxies.TryGetValue(id, out var proxy) && proxy != null) {
                    Destroy(proxy.gameObject);
                }

                proxies.Remove(id);
            }
        }

        Transform GetOrCreateProxy(OnlineTransformSnapshot entity) {
            if (proxies.TryGetValue(entity.Id, out var existing) && existing != null) {
                return existing;
            }

            GameObject instance = null;
            if (entity.IsCharacter && characterPrefab != null) {
                instance = Instantiate(characterPrefab, proxyRoot);
                instance.name = $"ProxyCharacter_{entity.Id}";
                DisableGameplayBehaviours(instance);
            } else if (entity.IsDice && dicePrefab != null) {
                instance = Instantiate(dicePrefab, proxyRoot);
                instance.name = $"ProxyDice_{entity.Id}_{((DiceKind)entity.Kind)}";
                var diceView = instance.GetComponentInChildren<DiceView>(true);
                diceView?.EnsureDiceInstance();
                DisableGameplayBehaviours(instance);
            }

            if (instance == null) {
                return null;
            }

            proxies[entity.Id] = instance.transform;
            return instance.transform;
        }

        static Transform ResolveSyncTransform(Transform proxy, bool isDice) {
            if (!isDice || proxy == null) {
                return proxy;
            }

            var positionRoot = proxy.Find("PositionRoot");
            return positionRoot != null ? positionRoot : proxy;
        }

        static void DisableGameplayBehaviours(GameObject instance) {
            foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true)) {
                if (behaviour == null) {
                    continue;
                }

                var typeName = behaviour.GetType().Name;
                if (typeName == "DiceView") {
                    continue;
                }

                behaviour.enabled = false;
            }

            foreach (var rb in instance.GetComponentsInChildren<Rigidbody>(true)) {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }

            foreach (var col in instance.GetComponentsInChildren<Collider>(true)) {
                col.enabled = false;
            }
        }
    }
}
