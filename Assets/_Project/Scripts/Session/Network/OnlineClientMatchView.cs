using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay.Input;
using DiceGame.Grid;
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
        DiceCatalog primaryCatalog;
        DiceCatalog secondaryCatalog;
        Board board;
        DiceErasureSettings erasureSettings;
        DiceOneVanishSettings oneVanishSettings;
        Transform proxyRoot;
        readonly Dictionary<uint, Transform> proxies = new();
        readonly Dictionary<uint, DiceView> diceViews = new();
        readonly Dictionary<uint, byte> lastVisualKinds = new();
        readonly Dictionary<uint, byte> lastMeshKinds = new();
        readonly HashSet<uint> sequenceSeenIds = new();
        CharacterInputReader localInputReader;
        float inputTimer;
        Vector2 lastMove;
        Direction? pendingDirection;
        float nextSnapshotReceivedLogTime;
        uint activeSnapshotSequence;
        ushort activeChunkCount;
        ulong receivedChunkMask;

        public void Configure(
            OnlineNetMessenger netMessenger,
            GameObject diceEntityPrefab,
            GameObject characterEntityPrefab,
            PlayerInputSettings playerInputSettings,
            DiceErasureSettings diceErasureSettings,
            DiceOneVanishSettings diceOneVanishSettings,
            Board matchBoard,
            DiceCatalog catalog,
            DiceCatalog alternateCatalog = null) {
            messenger = netMessenger;
            dicePrefab = diceEntityPrefab;
            characterPrefab = characterEntityPrefab;
            erasureSettings = diceErasureSettings;
            oneVanishSettings = diceOneVanishSettings;
            board = matchBoard;
            primaryCatalog = catalog;
            secondaryCatalog = alternateCatalog;

            if (proxyRoot == null) {
                var rootObject = new GameObject("OnlineClientProxies");
                proxyRoot = rootObject.transform;
                proxyRoot.SetParent(transform, false);
            }

            if (messenger != null) {
                messenger.SnapshotChunkReceived -= OnSnapshotChunkReceived;
                messenger.SnapshotChunkReceived += OnSnapshotChunkReceived;
            } else {
                Debug.LogError("OnlineClientMatchView.Configure: messenger is null.");
            }

            if (primaryCatalog == null && secondaryCatalog == null) {
                Debug.LogError("OnlineClientMatchView.Configure: no DiceCatalog assigned; kind meshes will be wrong.");
            }

            if (board == null) {
                Debug.LogError("OnlineClientMatchView.Configure: Board is null; jumbo scale cannot be applied.");
            }

            SetupLocalInputCapture(playerInputSettings);
        }

        void OnDestroy() {
            if (messenger != null) {
                messenger.SnapshotChunkReceived -= OnSnapshotChunkReceived;
            }
        }

        void Update() {
            CaptureAndSendInput();
        }

        void SetupLocalInputCapture(PlayerInputSettings settings) {
            if (localInputReader != null) {
                return;
            }

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

        void OnSnapshotChunkReceived(OnlineMatchSnapshotChunk chunk) {
            if (chunk.ChunkCount == 0 || chunk.ChunkIndex >= chunk.ChunkCount || chunk.ChunkCount > 64) {
                Debug.LogError(
                    $"OnlineClientMatchView: invalid chunk header seq={chunk.Sequence} " +
                    $"index={chunk.ChunkIndex} count={chunk.ChunkCount}");
                return;
            }

            if (chunk.Sequence != activeSnapshotSequence) {
                activeSnapshotSequence = chunk.Sequence;
                activeChunkCount = chunk.ChunkCount;
                receivedChunkMask = 0;
                sequenceSeenIds.Clear();
            } else if (chunk.ChunkCount != activeChunkCount) {
                activeChunkCount = chunk.ChunkCount;
                receivedChunkMask = 0;
                sequenceSeenIds.Clear();
            }

            var entities = chunk.Entities;
            if (entities != null) {
                for (var i = 0; i < entities.Length; i++) {
                    var entity = entities[i];
                    if (!entity.IsActive) {
                        continue;
                    }

                    sequenceSeenIds.Add(entity.Id);
                    var proxy = GetOrCreateProxy(entity);
                    if (proxy == null) {
                        continue;
                    }

                    ApplyEntityTransform(proxy, entity);
                    ApplyDicePresentation(entity);
                }
            }

            receivedChunkMask |= 1UL << chunk.ChunkIndex;
            var completeMask = activeChunkCount >= 64
                ? ulong.MaxValue
                : (1UL << activeChunkCount) - 1UL;
            var isComplete = (receivedChunkMask & completeMask) == completeMask;

            var now = Time.realtimeSinceStartup;
            if (now >= nextSnapshotReceivedLogTime) {
                nextSnapshotReceivedLogTime = now + 2f;
                Debug.Log(
                    $"OnlineClientMatchView.OnSnapshotChunkReceived: seq={chunk.Sequence} " +
                    $"chunk={chunk.ChunkIndex}/{chunk.ChunkCount} " +
                    $"chunkEntities={entities?.Length ?? 0} seen={sequenceSeenIds.Count} complete={isComplete}");
            }

            if (!isComplete) {
                return;
            }

            RemoveStaleProxies();
        }

        void RemoveStaleProxies() {
            var stale = new List<uint>();
            foreach (var pair in proxies) {
                if (!sequenceSeenIds.Contains(pair.Key)) {
                    stale.Add(pair.Key);
                }
            }

            for (var i = 0; i < stale.Count; i++) {
                var id = stale[i];
                if (proxies.TryGetValue(id, out var proxy) && proxy != null) {
                    Destroy(proxy.gameObject);
                }

                proxies.Remove(id);
                diceViews.Remove(id);
                lastVisualKinds.Remove(id);
                lastMeshKinds.Remove(id);
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
                if (diceView != null) {
                    diceView.Configure(null, null, erasureSettings);
                    diceViews[entity.Id] = diceView;
                    ApplyKindMesh(diceView, entity.Id, (DiceKind)entity.Kind);
                }

                DisableGameplayBehaviours(instance);
            }

            if (instance == null) {
                return null;
            }

            proxies[entity.Id] = instance.transform;
            return instance.transform;
        }

        void ApplyDicePresentation(OnlineTransformSnapshot entity) {
            if (!entity.IsDice) {
                return;
            }

            if (!diceViews.TryGetValue(entity.Id, out var diceView) || diceView == null) {
                if (!proxies.TryGetValue(entity.Id, out var proxy) || proxy == null) {
                    return;
                }

                diceView = proxy.GetComponentInChildren<DiceView>(true);
                if (diceView == null) {
                    return;
                }

                diceView.Configure(null, null, erasureSettings);
                diceViews[entity.Id] = diceView;
            }

            var kind = (DiceKind)entity.Kind;
            if (!lastMeshKinds.TryGetValue(entity.Id, out var previousMeshKind)
                || previousMeshKind != entity.Kind) {
                ApplyKindMesh(diceView, entity.Id, kind);
            }

            Color? emission = entity.HasEmissionOverride
                ? (Color)entity.EmissionColor
                : null;

            var previousKind = lastVisualKinds.TryGetValue(entity.Id, out var visualKind)
                ? visualKind
                : OnlineTransformSnapshot.VisualNone;
            if (entity.VisualKind == OnlineTransformSnapshot.VisualNone
                && previousKind == OnlineTransformSnapshot.VisualNone) {
                return;
            }

            diceView.ApplyNetworkVisualPresentation(
                entity.VisualKind,
                entity.VisualProgress,
                entity.TopFace,
                emission,
                oneVanishSettings);
            lastVisualKinds[entity.Id] = entity.VisualKind;
        }

        void ApplyKindMesh(DiceView diceView, uint entityId, DiceKind kind) {
            if (diceView == null) {
                return;
            }

            TryResolveMeshPrefab(kind, out var meshPrefab);
            diceView.ApplyNetworkKindPresentation(board, kind, meshPrefab);
            lastMeshKinds[entityId] = (byte)kind;
        }

        bool TryResolveMeshPrefab(DiceKind kind, out GameObject meshPrefab) {
            if (primaryCatalog != null && primaryCatalog.TryGetMeshPrefab(kind, out meshPrefab)) {
                return true;
            }

            if (secondaryCatalog != null && secondaryCatalog.TryGetMeshPrefab(kind, out meshPrefab)) {
                return true;
            }

            meshPrefab = null;
            return false;
        }

        static void ApplyEntityTransform(Transform proxy, OnlineTransformSnapshot entity) {
            if (proxy == null) {
                return;
            }

            if (!entity.IsDice) {
                proxy.SetPositionAndRotation(entity.Position, entity.Rotation);
                return;
            }

            var diceView = proxy.GetComponentInChildren<DiceView>(true);
            var positionRoot = diceView != null && diceView.DiceTransform != null
                ? diceView.DiceTransform
                : proxy.Find("PositionRoot");
            var rotationRoot = diceView != null && diceView.DiceRotationTransform != null
                ? diceView.DiceRotationTransform
                : positionRoot != null
                    ? positionRoot.Find("RotationRoot")
                    : null;

            if (positionRoot == null) {
                proxy.SetPositionAndRotation(entity.Position, entity.Rotation);
                return;
            }

            positionRoot.position = entity.Position;
            if (rotationRoot != null) {
                rotationRoot.rotation = entity.Rotation;
            } else {
                positionRoot.rotation = entity.Rotation;
            }
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
