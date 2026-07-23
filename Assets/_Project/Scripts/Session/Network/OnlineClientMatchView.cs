using System;
using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay.Input;
using DiceGame.Grid;
using DiceGame.Versus.Core;
using DiceGame.View;
using Unity.Netcode;
using UnityEngine;

namespace DiceGame.Session.Network
{
    public sealed class OnlineClientMatchView : MonoBehaviour
    {
        sealed class ProxyMotionState
        {
            public Transform PositionTransform;
            public Transform RotationTransform;
            public Vector3 TargetPosition;
            public Quaternion TargetRotation;
            public Vector3 CurrentPosition;
            public Quaternion CurrentRotation;
            public Vector3 PositionVelocity;
            public bool Initialized;
            public bool IsCharacter;
        }

        sealed class SyntheticJumpMotion
        {
            VerticalMotionState state;
            readonly float gravity;
            float lastSampleTime;
            bool started;

            public SyntheticJumpMotion(float jumpHeight, float gravityStrength) {
                gravity = Mathf.Max(0.01f, gravityStrength);
                state = GravityMotion.CreateLaunch(Mathf.Max(0f, jumpHeight), gravity);
            }

            public VerticalMotionState Sample() {
                var now = Time.time;
                if (!started) {
                    started = true;
                    lastSampleTime = now;
                    return state;
                }

                var delta = now - lastSampleTime;
                lastSampleTime = now;
                if (delta > 0f) {
                    state = GravityMotion.Step(state, gravity, delta);
                }

                return state;
            }
        }

        OnlineNetMessenger messenger;
        GameObject dicePrefab;
        GameObject characterPrefab;
        DiceCatalog primaryCatalog;
        DiceCatalog secondaryCatalog;
        Board board;
        PhysicsSettings physicsSettings;
        DiceAnimationSettings animationSettings;
        DiceErasureSettings erasureSettings;
        DiceOneVanishSettings oneVanishSettings;
        Transform proxyRoot;
        readonly Dictionary<uint, Transform> proxies = new();
        readonly Dictionary<uint, DiceView> diceViews = new();
        readonly Dictionary<uint, ProxyMotionState> motionStates = new();
        readonly Dictionary<uint, byte> lastMeshKinds = new();
        readonly Dictionary<uint, byte> lastCatalogSides = new();
        readonly HashSet<uint> sequenceSeenIds = new();
        readonly HashSet<uint> retainUntilSnapshot = new();
        AttackQueueView attackQueueView;
        AttackQueueUiSettings attackQueueUiSettings;
        CharacterMovementSettings movementSettings;
        readonly Dictionary<uint, DiceState> logicalStates = new();
        CharacterInputReader localInputReader;
        float inputTimer;
        Vector2 lastMove;
        Direction? pendingDirection;
        float nextSnapshotReceivedLogTime;
        uint activeSnapshotSequence;
        uint localCharacterEntityId;
        bool hasLocalCharacterEntity;
        float localMoveSpeed;
        Vector3 localPredictedPosition;
        bool localPredictionInitialized;

        public void Configure(
            OnlineNetMessenger netMessenger,
            GameObject diceEntityPrefab,
            GameObject characterEntityPrefab,
            PlayerInputSettings playerInputSettings,
            PhysicsSettings matchPhysicsSettings,
            DiceAnimationSettings matchAnimationSettings,
            DiceErasureSettings diceErasureSettings,
            DiceOneVanishSettings diceOneVanishSettings,
            Board matchBoard,
            DiceCatalog catalog,
            DiceCatalog alternateCatalog = null,
            AttackQueueUiSettings queueUiSettings = null,
            CharacterMovementSettings characterMovement = null) {
            messenger = netMessenger;
            dicePrefab = diceEntityPrefab;
            characterPrefab = characterEntityPrefab;
            physicsSettings = matchPhysicsSettings;
            animationSettings = matchAnimationSettings;
            erasureSettings = diceErasureSettings;
            oneVanishSettings = diceOneVanishSettings;
            board = matchBoard;
            primaryCatalog = catalog;
            secondaryCatalog = alternateCatalog;
            attackQueueUiSettings = queueUiSettings;
            movementSettings = characterMovement;

            if (proxyRoot == null) {
                var rootObject = new GameObject("OnlineClientProxies");
                proxyRoot = rootObject.transform;
                proxyRoot.SetParent(transform, false);
            }

            if (messenger != null) {
                messenger.SnapshotChunkReceived -= OnSnapshotChunkReceived;
                messenger.SnapshotChunkReceived += OnSnapshotChunkReceived;
                messenger.DiceMotionReceived -= OnDiceMotionReceived;
                messenger.DiceMotionReceived += OnDiceMotionReceived;
                messenger.AttackQueueReceived -= OnAttackQueueReceived;
                messenger.AttackQueueReceived += OnAttackQueueReceived;
            } else {
                Debug.LogError("OnlineClientMatchView.Configure: messenger is null.");
            }

            if (physicsSettings == null || animationSettings == null) {
                Debug.LogError(
                    "OnlineClientMatchView.Configure: physics/animation settings missing; dice motion events cannot play.");
            }

            if (primaryCatalog == null && secondaryCatalog == null) {
                Debug.LogError("OnlineClientMatchView.Configure: no DiceCatalog assigned; kind meshes will be wrong.");
            }

            if (board == null) {
                Debug.LogError("OnlineClientMatchView.Configure: Board is null; jumbo scale cannot be applied.");
            }

            EnsureAttackQueueView();
            SetupLocalInputCapture(playerInputSettings);
        }

        void OnDestroy() {
            if (messenger != null) {
                messenger.SnapshotChunkReceived -= OnSnapshotChunkReceived;
                messenger.DiceMotionReceived -= OnDiceMotionReceived;
                messenger.AttackQueueReceived -= OnAttackQueueReceived;
            }
        }

        void Update() {
            CaptureAndSendInput();
            TickLocalCharacterPrediction();
            TickCharacterInterpolation();
        }

        void SetupLocalInputCapture(PlayerInputSettings settings) {
            if (localInputReader != null) {
                return;
            }

            var inputObject = new GameObject("OnlineClientLocalInput");
            inputObject.transform.SetParent(transform, false);
            localInputReader = inputObject.AddComponent<CharacterInputReader>();
            if (settings != null) {
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

        void OnDiceMotionReceived(OnlineDiceMotionEvent motionEvent) {
            retainUntilSnapshot.Add(motionEvent.EntityId);
            var diceView = GetOrCreateDiceViewForMotion(motionEvent);
            if (diceView == null || board == null) {
                Debug.LogError(
                    $"OnlineClientMatchView: cannot play motion kind={motionEvent.Kind} entity={motionEvent.EntityId}");
                return;
            }

            ApplyKindMesh(
                diceView,
                motionEvent.EntityId,
                motionEvent.ToState.Kind,
                (PlayerSlot)motionEvent.CatalogSide);

            diceView.SetNetworkSurfaceOverride(
                motionEvent.FromSurfaceWorldY,
                motionEvent.ToSurfaceWorldY,
                motionEvent.ToState.GridPos);

            void ClearOverride() {
                diceView.ClearNetworkSurfaceOverride();
            }

            switch (motionEvent.MotionKind) {
                case DiceVisualMotionKind.JumpRoll:
                    PlayJumpRollMotion(diceView, motionEvent, ClearOverride);
                    break;
                case DiceVisualMotionKind.Transition:
                    PlayTransitionMotion(diceView, motionEvent, ClearOverride);
                    break;
                case DiceVisualMotionKind.SpawnFall:
                    diceView.PlaySpawnAppear(
                        motionEvent.ToState,
                        board,
                        registry: null,
                        (motionEvent.Flags & OnlineDiceMotionEvent.FlagEnableSpawnBounce) != 0,
                        motionEvent.FallGravityScale,
                        ClearOverride);
                    break;
                case DiceVisualMotionKind.SpawnEmerge:
                    diceView.PlayBottomEmergenceAppear(
                        motionEvent.ToState,
                        board,
                        registry: null,
                        motionEvent.FallGravityScale,
                        ClearOverride);
                    break;
                case DiceVisualMotionKind.Erasure:
                    Color? emission = (motionEvent.Flags & OnlineDiceMotionEvent.FlagHasEmissionOverride) != 0
                        ? (Color)motionEvent.EmissionColor
                        : null;
                    diceView.PlayErasure(
                        (ErasureKind)motionEvent.ErasureKind,
                        board,
                        motionEvent.TopFace,
                        emission,
                        ClearOverride);
                    break;
                case DiceVisualMotionKind.OneVanish:
                    if (oneVanishSettings == null) {
                        Debug.LogError("OnlineClientMatchView: OneVanish settings missing.");
                        ClearOverride();
                        return;
                    }

                    diceView.PlayOneVanish(oneVanishSettings, ClearOverride);
                    break;
                default:
                    ClearOverride();
                    break;
            }
        }

        void PlayJumpRollMotion(DiceView diceView, OnlineDiceMotionEvent motionEvent, Action onComplete) {
            Func<VerticalMotionState> jumpProvider = null;
            if ((motionEvent.Flags & OnlineDiceMotionEvent.FlagUseArcJump) != 0
                && physicsSettings != null
                && board != null) {
                var jumpHeight = board.CellSize * physicsSettings.JumpHeightDiceMultiplier;
                var synthetic = new SyntheticJumpMotion(jumpHeight, physicsSettings.Gravity);
                jumpProvider = synthetic.Sample;
            }

            diceView.PlayJumpRoll(
                (Direction)motionEvent.Direction,
                motionEvent.FromState,
                motionEvent.ToState,
                motionEvent.JumpYOffset,
                Mathf.Max(1, motionEvent.RollDistance),
                board,
                registry: null,
                onComplete,
                (motionEvent.Flags & OnlineDiceMotionEvent.FlagFallBeforeSnap) != 0,
                jumpProvider);
        }

        void PlayTransitionMotion(DiceView diceView, OnlineDiceMotionEvent motionEvent, Action onComplete) {
            var transition = new DiceTransition {
                From = motionEvent.FromState,
                To = motionEvent.ToState,
                Path = (DiceTransitionPath)motionEvent.TransitionPath,
                RollDirection = (Direction)motionEvent.Direction,
                SnapToGridOnComplete = (motionEvent.Flags & OnlineDiceMotionEvent.FlagSnapToGridOnComplete) != 0,
                FromWorldOverride = (motionEvent.Flags & OnlineDiceMotionEvent.FlagHasFromWorldOverride) != 0
                    ? motionEvent.FromWorldOverride
                    : null,
                ToWorldOverride = (motionEvent.Flags & OnlineDiceMotionEvent.FlagHasToWorldOverride) != 0
                    ? motionEvent.ToWorldOverride
                    : null
            };
            diceView.PlayTransition(
                transition,
                board,
                registry: null,
                onComplete,
                Mathf.Max(1, motionEvent.SlideCellDistance));
        }

        void OnSnapshotChunkReceived(OnlineMatchSnapshotChunk chunk) {
            // Snapshots are always a single Reliable packet (ChunkCount == 1).
            if (chunk.ChunkCount != 1 || chunk.ChunkIndex != 0) {
                Debug.LogError(
                    $"OnlineClientMatchView: expected single-chunk snapshot, got " +
                    $"{chunk.ChunkIndex}/{chunk.ChunkCount} seq={chunk.Sequence}");
                return;
            }

            activeSnapshotSequence = chunk.Sequence;
            sequenceSeenIds.Clear();

            var entities = chunk.Entities;
            if (entities != null) {
                // Pass 1: create proxies and cache logical dice states (so Top can resolve Bottom height).
                for (var i = 0; i < entities.Length; i++) {
                    var entity = entities[i];
                    if (!entity.IsActive) {
                        continue;
                    }

                    sequenceSeenIds.Add(entity.Id);
                    retainUntilSnapshot.Remove(entity.Id);
                    if (GetOrCreateProxy(entity) == null) {
                        continue;
                    }

                    if (entity.IsDice) {
                        logicalStates[entity.Id] = entity.ToDiceState();
                        if (diceViews.TryGetValue(entity.Id, out var diceView) && diceView != null) {
                            ApplyKindMesh(
                                diceView,
                                entity.Id,
                                (DiceKind)entity.Kind,
                                entity.CatalogPlayerSlot);
                        }
                    } else if (IsLocalPlayerCharacter(entity)) {
                        ApplyLocalCharacterAuthority(entity);
                    } else {
                        SetCharacterProxyTarget(entity);
                    }
                }

                // Pass 2: idle dice SnapTo from logical state (skip while local Play* owns motion).
                for (var i = 0; i < entities.Length; i++) {
                    var entity = entities[i];
                    if (!entity.IsActive || !entity.IsDice) {
                        continue;
                    }

                    SnapIdleDiceToLogicalState(entity.Id);
                }
            }

            var now = Time.realtimeSinceStartup;
            if (now >= nextSnapshotReceivedLogTime) {
                nextSnapshotReceivedLogTime = now + 2f;
                Debug.Log(
                    $"OnlineClientMatchView.OnSnapshotChunkReceived: seq={chunk.Sequence} " +
                    $"entities={entities?.Length ?? 0} complete=True");
            }

            RemoveStaleProxies();
        }

        void OnAttackQueueReceived(OnlineAttackQueueSnapshot queueSnapshot) {
            EnsureAttackQueueView();
            if (attackQueueView == null) {
                return;
            }

            attackQueueView.RenderAll(
                ToVolleys(queueSnapshot.Player1Volleys),
                ToVolleys(queueSnapshot.Player2Volleys));
        }

        void EnsureAttackQueueView() {
            if (attackQueueView != null) {
                return;
            }

            var viewObject = new GameObject("OnlineClientAttackQueueView");
            viewObject.transform.SetParent(transform, false);
            attackQueueView = viewObject.AddComponent<AttackQueueView>();
            attackQueueView.Configure(
                primaryCatalog,
                secondaryCatalog,
                attackQueueUiSettings);
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

        void TickCharacterInterpolation() {
            var smoothTime = OnlineSessionConstants.SnapshotInterpSmoothTimeSeconds;
            var delta = Time.unscaledDeltaTime;
            if (delta <= 0f) {
                return;
            }

            var rotationBlend = 1f - Mathf.Exp(-delta / Mathf.Max(0.0001f, smoothTime));
            foreach (var pair in motionStates) {
                if (hasLocalCharacterEntity && pair.Key == localCharacterEntityId) {
                    continue;
                }

                var state = pair.Value;
                if (state?.PositionTransform == null || !state.Initialized || !state.IsCharacter) {
                    continue;
                }

                state.CurrentPosition = Vector3.SmoothDamp(
                    state.CurrentPosition,
                    state.TargetPosition,
                    ref state.PositionVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    delta);
                state.CurrentRotation = Quaternion.Slerp(
                    state.CurrentRotation,
                    state.TargetRotation,
                    rotationBlend);

                state.PositionTransform.position = state.CurrentPosition;
                if (state.RotationTransform != null) {
                    state.RotationTransform.rotation = state.CurrentRotation;
                } else {
                    state.PositionTransform.rotation = state.CurrentRotation;
                }
            }
        }

        static bool IsLocalPlayerCharacter(OnlineTransformSnapshot entity) {
            // Host snapshot stores PlayerSlot in Kind for characters. Online client controls Player2.
            return entity.IsCharacter && (PlayerSlot)entity.Kind == PlayerSlot.Player2;
        }

        void ApplyLocalCharacterAuthority(OnlineTransformSnapshot entity) {
            localCharacterEntityId = entity.Id;
            hasLocalCharacterEntity = true;

            if (!proxies.TryGetValue(entity.Id, out var proxy) || proxy == null) {
                return;
            }

            if (!TryResolveMotionTransforms(proxy, entity, out var positionTransform, out _)) {
                return;
            }

            // Drop SmoothDamp state so remote interp never fights prediction.
            motionStates.Remove(entity.Id);

            if (!localPredictionInitialized) {
                localPredictedPosition = entity.Position;
                positionTransform.position = entity.Position;
                positionTransform.rotation = entity.Rotation;
                localPredictionInitialized = true;
                localMoveSpeed = 0f;
                return;
            }

            var error = entity.Position - localPredictedPosition;
            var snapDistance = OnlineSessionConstants.SnapshotInterpSnapDistance;
            if (error.sqrMagnitude >= snapDistance * snapDistance) {
                localPredictedPosition = entity.Position;
                localMoveSpeed = 0f;
            } else {
                // Soft reconcile: pull toward host without waiting a full RTT to start moving.
                localPredictedPosition = Vector3.Lerp(
                    localPredictedPosition,
                    entity.Position,
                    OnlineSessionConstants.LocalCharacterReconcileBlend);
            }

            positionTransform.position = localPredictedPosition;
            // Face from host when nearly idle; prediction updates yaw while moving.
            if (localMoveSpeed < 0.05f) {
                positionTransform.rotation = entity.Rotation;
            }
        }

        void TickLocalCharacterPrediction() {
            if (!hasLocalCharacterEntity
                || !localPredictionInitialized
                || localInputReader == null
                || movementSettings == null) {
                return;
            }

            if (NetworkManager.Singleton == null
                || !NetworkManager.Singleton.IsClient
                || NetworkManager.Singleton.IsServer) {
                return;
            }

            if (!proxies.TryGetValue(localCharacterEntityId, out var proxy) || proxy == null) {
                return;
            }

            var delta = Time.unscaledDeltaTime;
            if (delta <= 0f) {
                return;
            }

            var input = localInputReader.ReadMove();
            var inputMagnitude = Mathf.Clamp01(input.magnitude);
            var targetSpeed = movementSettings.MaxMoveSpeed * inputMagnitude;
            localMoveSpeed = Mathf.MoveTowards(
                localMoveSpeed,
                targetSpeed,
                movementSettings.MoveAcceleration * delta);

            if (localMoveSpeed > 0.0001f && inputMagnitude > 0.0001f) {
                var moveDir = new Vector3(input.x, 0f, input.y);
                if (moveDir.sqrMagnitude > 0.0001f) {
                    moveDir.Normalize();
                    localPredictedPosition += moveDir * (localMoveSpeed * delta);
                    proxy.rotation = Quaternion.LookRotation(moveDir, Vector3.up);
                }
            }

            proxy.position = localPredictedPosition;
        }

        void RemoveStaleProxies() {
            var stale = new List<uint>();
            foreach (var pair in proxies) {
                if (sequenceSeenIds.Contains(pair.Key)) {
                    continue;
                }

                if (retainUntilSnapshot.Contains(pair.Key) || IsDiceDrivenByLocalMotion(pair.Key)) {
                    continue;
                }

                stale.Add(pair.Key);
            }

            for (var i = 0; i < stale.Count; i++) {
                var id = stale[i];
                if (proxies.TryGetValue(id, out var proxy) && proxy != null) {
                    Destroy(proxy.gameObject);
                }

                proxies.Remove(id);
                diceViews.Remove(id);
                motionStates.Remove(id);
                lastMeshKinds.Remove(id);
                lastCatalogSides.Remove(id);
                logicalStates.Remove(id);
                retainUntilSnapshot.Remove(id);

                if (hasLocalCharacterEntity && id == localCharacterEntityId) {
                    hasLocalCharacterEntity = false;
                    localPredictionInitialized = false;
                    localMoveSpeed = 0f;
                }
            }
        }

        DiceView GetOrCreateDiceViewForMotion(OnlineDiceMotionEvent motionEvent) {
            if (diceViews.TryGetValue(motionEvent.EntityId, out var existing) && existing != null) {
                return existing;
            }

            var entity = new OnlineTransformSnapshot {
                Id = motionEvent.EntityId,
                Kind = (byte)motionEvent.ToState.Kind,
                Flags = (byte)(OnlineTransformSnapshot.FlagDice | OnlineTransformSnapshot.FlagActive),
                CatalogSide = motionEvent.CatalogSide,
                GridX = (short)motionEvent.ToState.GridPos.x,
                GridY = (short)motionEvent.ToState.GridPos.y,
                Tier = (byte)motionEvent.ToState.Tier,
                TopFace = (byte)motionEvent.ToState.Orientation.Top,
                NorthFace = (byte)motionEvent.ToState.Orientation.North,
                EastFace = (byte)motionEvent.ToState.Orientation.East,
                Position = Vector3.zero,
                Rotation = Quaternion.identity
            };
            GetOrCreateProxy(entity);
            return diceViews.TryGetValue(motionEvent.EntityId, out var created) ? created : null;
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
                    diceView.Configure(physicsSettings, animationSettings, erasureSettings);
                    diceView.SetEmitVisualMotionEvents(false);
                    diceViews[entity.Id] = diceView;
                    ApplyKindMesh(diceView, entity.Id, (DiceKind)entity.Kind, entity.CatalogPlayerSlot);
                }

                DisableGameplayBehaviours(instance);
            }

            if (instance == null) {
                return null;
            }

            proxies[entity.Id] = instance.transform;
            return instance.transform;
        }

        void SetCharacterProxyTarget(OnlineTransformSnapshot entity) {
            if (!proxies.TryGetValue(entity.Id, out var proxy) || proxy == null) {
                return;
            }

            if (!TryResolveMotionTransforms(proxy, entity, out var positionTransform, out var rotationTransform)) {
                return;
            }

            if (!motionStates.TryGetValue(entity.Id, out var state) || state == null) {
                state = new ProxyMotionState();
                motionStates[entity.Id] = state;
            }

            state.PositionTransform = positionTransform;
            state.RotationTransform = rotationTransform;
            state.TargetPosition = entity.Position;
            state.TargetRotation = entity.Rotation;
            state.IsCharacter = true;

            var snapDistance = OnlineSessionConstants.SnapshotInterpSnapDistance;
            var shouldSnap = !state.Initialized
                || (state.CurrentPosition - state.TargetPosition).sqrMagnitude >= snapDistance * snapDistance
                || Quaternion.Angle(state.CurrentRotation, state.TargetRotation) > 90f;

            if (shouldSnap) {
                state.CurrentPosition = state.TargetPosition;
                state.CurrentRotation = state.TargetRotation;
                state.PositionVelocity = Vector3.zero;
                positionTransform.position = state.TargetPosition;
                if (rotationTransform != null) {
                    rotationTransform.rotation = state.TargetRotation;
                } else {
                    positionTransform.rotation = state.TargetRotation;
                }
            }

            state.Initialized = true;
        }

        void SnapIdleDiceToLogicalState(uint entityId) {
            if (!logicalStates.TryGetValue(entityId, out var state)) {
                return;
            }

            if (!diceViews.TryGetValue(entityId, out var diceView) || diceView == null) {
                if (!proxies.TryGetValue(entityId, out var proxy) || proxy == null) {
                    return;
                }

                diceView = proxy.GetComponentInChildren<DiceView>(true);
                if (diceView == null) {
                    return;
                }

                diceView.Configure(physicsSettings, animationSettings, erasureSettings);
                diceView.SetEmitVisualMotionEvents(false);
                diceViews[entityId] = diceView;
            }

            if (IsDiceDrivenByLocalMotion(entityId) || board == null) {
                return;
            }

            // Local Play* owns motion. Idle correction uses logical SnapTo (no world-pose chase).
            var surfaceY = ResolvePresentationSurfaceWorldY(state);
            diceView.SetNetworkSurfaceOverride(surfaceY, surfaceY, state.GridPos);
            diceView.SnapTo(state, board, registry: null);
            diceView.ClearNetworkSurfaceOverride();
        }

        float ResolvePresentationSurfaceWorldY(DiceState state) {
            if (board == null) {
                return 0f;
            }

            if (state.Tier != DiceStackTier.Top) {
                return board.FloorSurfaceWorldY;
            }

            foreach (var pair in logicalStates) {
                if (pair.Value.GridPos != state.GridPos || pair.Value.Tier != DiceStackTier.Bottom) {
                    continue;
                }

                if (!diceViews.TryGetValue(pair.Key, out var bottomView) || bottomView == null) {
                    continue;
                }

                return bottomView.GetLogicalBottomTierTopSurfaceWorldY(board);
            }

            return board.FloorSurfaceWorldY + board.CellSize;
        }

        bool IsDiceDrivenByLocalMotion(uint entityId) {
            if (!diceViews.TryGetValue(entityId, out var diceView) || diceView == null) {
                return false;
            }

            return diceView.IsAnimating
                || diceView.IsOneVanishing
                || diceView.ErasureProgress > 0f;
        }

        static bool TryResolveMotionTransforms(
            Transform proxy,
            OnlineTransformSnapshot entity,
            out Transform positionTransform,
            out Transform rotationTransform) {
            positionTransform = null;
            rotationTransform = null;
            if (proxy == null) {
                return false;
            }

            if (!entity.IsDice) {
                positionTransform = proxy;
                rotationTransform = proxy;
                return true;
            }

            var diceView = proxy.GetComponentInChildren<DiceView>(true);
            positionTransform = diceView != null && diceView.DiceTransform != null
                ? diceView.DiceTransform
                : proxy.Find("PositionRoot");
            rotationTransform = diceView != null && diceView.DiceRotationTransform != null
                ? diceView.DiceRotationTransform
                : positionTransform != null
                    ? positionTransform.Find("RotationRoot")
                    : null;

            if (positionTransform == null) {
                positionTransform = proxy;
                rotationTransform = proxy;
            }

            return positionTransform != null;
        }

        void ApplyKindMesh(DiceView diceView, uint entityId, DiceKind kind, PlayerSlot catalogSide) {
            if (diceView == null) {
                return;
            }

            var sideByte = (byte)catalogSide;
            if (lastMeshKinds.TryGetValue(entityId, out var previousKind)
                && previousKind == (byte)kind
                && lastCatalogSides.TryGetValue(entityId, out var previousSide)
                && previousSide == sideByte) {
                return;
            }

            TryResolveMeshPrefab(kind, catalogSide, out var meshPrefab);
            diceView.ApplyNetworkKindPresentation(board, kind, meshPrefab);
            lastMeshKinds[entityId] = (byte)kind;
            lastCatalogSides[entityId] = sideByte;
        }

        bool TryResolveMeshPrefab(DiceKind kind, PlayerSlot catalogSide, out GameObject meshPrefab) {
            var preferred = catalogSide == PlayerSlot.Player2 ? secondaryCatalog : primaryCatalog;
            var fallback = catalogSide == PlayerSlot.Player2 ? primaryCatalog : secondaryCatalog;

            if (preferred != null && preferred.TryGetMeshPrefab(kind, out meshPrefab)) {
                return true;
            }

            if (fallback != null && fallback.TryGetMeshPrefab(kind, out meshPrefab)) {
                return true;
            }

            meshPrefab = null;
            return false;
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
