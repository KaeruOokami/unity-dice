using System;
using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Grid;
using DiceGame.Placement;
using DiceGame.Versus;
using DiceGame.Versus.Core;
using DiceGame.View;
using UnityEngine;
using GameCharacterController = DiceGame.Gameplay.CharacterController;

namespace DiceGame.Session.Network
{
    /// <summary>
    /// Phase B: apply host Reliable dice motion / attack-queue events onto the
    /// client's seed-built local board (no presentation proxies).
    /// </summary>
    public sealed class OnlineClientEventBinder : MonoBehaviour
    {
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
        OnlineEntityIdMap entityIds;
        DiceRegistry registry;
        DiceSpawnSystem spawnSystem;
        DiceMatchOwnershipContext ownershipContext;
        VersusAttackController attackController;
        Board board;
        PhysicsSettings physicsSettings;
        DiceOneVanishSettings oneVanishSettings;
        readonly List<GameCharacterController> characters = new();

        public void Configure(
            OnlineNetMessenger netMessenger,
            OnlineEntityIdMap idMap,
            DiceRegistry diceRegistry,
            DiceSpawnSystem diceSpawnSystem,
            DiceMatchOwnershipContext matchOwnership,
            VersusAttackController versusAttackController,
            Board matchBoard,
            PhysicsSettings matchPhysicsSettings,
            DiceOneVanishSettings diceOneVanishSettings,
            IReadOnlyList<GameCharacterController> spawnedCharacters) {
            messenger = netMessenger;
            entityIds = idMap;
            registry = diceRegistry;
            spawnSystem = diceSpawnSystem;
            ownershipContext = matchOwnership;
            attackController = versusAttackController;
            board = matchBoard;
            physicsSettings = matchPhysicsSettings;
            oneVanishSettings = diceOneVanishSettings;
            characters.Clear();
            if (spawnedCharacters != null) {
                characters.AddRange(spawnedCharacters);
            }

            if (entityIds != null && registry != null) {
                entityIds.RebuildFromSeed(characters, registry, ownershipContext);
            }

            SilenceClientMotionEcho();

            if (messenger != null) {
                messenger.DiceMotionReceived -= OnDiceMotionReceived;
                messenger.DiceMotionReceived += OnDiceMotionReceived;
                messenger.AttackQueueReceived -= OnAttackQueueReceived;
                messenger.AttackQueueReceived += OnAttackQueueReceived;
            } else {
                Debug.LogError("OnlineClientEventBinder.Configure: messenger is null.");
            }
        }

        void OnDestroy() {
            if (messenger != null) {
                messenger.DiceMotionReceived -= OnDiceMotionReceived;
                messenger.AttackQueueReceived -= OnAttackQueueReceived;
            }
        }

        void SilenceClientMotionEcho() {
            if (registry == null) {
                return;
            }

            for (var i = 0; i < registry.AllDice.Count; i++) {
                var dice = registry.AllDice[i];
                dice?.View?.SetEmitVisualMotionEvents(false);
            }
        }

        void OnAttackQueueReceived(OnlineAttackQueueSnapshot queueSnapshot) {
            if (attackController == null) {
                return;
            }

            attackController.ApplyNetworkQueuePresentation(
                ToVolleys(queueSnapshot.Player1Volleys),
                ToVolleys(queueSnapshot.Player2Volleys));
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

        void OnDiceMotionReceived(OnlineDiceMotionEvent motionEvent) {
            if (entityIds == null || board == null) {
                Debug.LogError("OnlineClientEventBinder: missing entity map or board.");
                return;
            }

            if (!TryResolveDiceForMotion(motionEvent, out var controller)) {
                Debug.LogError(
                    $"OnlineClientEventBinder: no dice for entity={motionEvent.EntityId} " +
                    $"kind={motionEvent.MotionKind}");
                return;
            }

            var diceView = controller.View;
            if (diceView == null) {
                Debug.LogError(
                    $"OnlineClientEventBinder: DiceView missing entity={motionEvent.EntityId}");
                return;
            }

            diceView.SetEmitVisualMotionEvents(false);
            ApplyAuthoritativeLogicalState(controller, motionEvent.ToState);

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
                        registry,
                        (motionEvent.Flags & OnlineDiceMotionEvent.FlagEnableSpawnBounce) != 0,
                        motionEvent.FallGravityScale,
                        ClearOverride);
                    break;
                case DiceVisualMotionKind.SpawnEmerge:
                    diceView.PlayBottomEmergenceAppear(
                        motionEvent.ToState,
                        board,
                        registry,
                        motionEvent.FallGravityScale,
                        ClearOverride);
                    break;
                case DiceVisualMotionKind.Erasure:
                    PlayErasureMotion(controller, diceView, motionEvent, ClearOverride);
                    break;
                case DiceVisualMotionKind.OneVanish:
                    PlayOneVanishMotion(controller, diceView, motionEvent, ClearOverride);
                    break;
                default:
                    ClearOverride();
                    break;
            }
        }

        bool TryResolveDiceForMotion(OnlineDiceMotionEvent motionEvent, out DiceController controller) {
            if (entityIds.TryGetDice(motionEvent.EntityId, out controller)) {
                return true;
            }

            if (motionEvent.MotionKind != DiceVisualMotionKind.SpawnFall
                && motionEvent.MotionKind != DiceVisualMotionKind.SpawnEmerge) {
                controller = null;
                return false;
            }

            controller = SpawnDiceFromMotion(motionEvent);
            if (controller == null) {
                return false;
            }

            entityIds.RegisterDice(controller, motionEvent.EntityId);
            controller.View?.SetEmitVisualMotionEvents(false);
            return true;
        }

        DiceController SpawnDiceFromMotion(OnlineDiceMotionEvent motionEvent) {
            if (spawnSystem == null) {
                Debug.LogError("OnlineClientEventBinder: spawn system missing for network spawn.");
                return null;
            }

            var owner = (PlayerSlot)motionEvent.CatalogSide;
            if (!spawnSystem.TryGetSpawnSettings(owner, out var spawnSettings) || spawnSettings == null) {
                Debug.LogError(
                    $"OnlineClientEventBinder: spawn settings missing for owner={owner}.");
                return null;
            }

            // Appear animation is driven by the motion event; create at rest first.
            return spawnSystem.ApplyNetworkSpawn(
                motionEvent.ToState.GridPos,
                motionEvent.ToState.Tier,
                motionEvent.ToState.Kind,
                motionEvent.ToState.Orientation,
                owner,
                spawnSettings,
                useSpawnAppear: false,
                forceFallFromAbove: false);
        }

        void ApplyAuthoritativeLogicalState(DiceController dice, DiceState toState) {
            if (dice == null) {
                return;
            }

            var fromState = dice.CurrentState;
            if (registry != null
                && (fromState.GridPos != toState.GridPos || fromState.Tier != toState.Tier)) {
                registry.MoveDice(
                    dice,
                    fromState.GridPos,
                    toState.GridPos,
                    fromState.Tier,
                    toState.Tier);
            }

            dice.ApplyExternalState(toState, snapVisual: false);
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
                registry,
                onComplete,
                (motionEvent.Flags & OnlineDiceMotionEvent.FlagFallBeforeSnap) != 0,
                jumpProvider);
        }

        void PlayTransitionMotion(DiceView diceView, OnlineDiceMotionEvent motionEvent, Action onComplete) {
            var transition = new DiceTransition {
                From = motionEvent.FromState,
                To = motionEvent.ToState,
                Path = (DiceTransitionPath)motionEvent.TransitionPath
            };
            diceView.PlayTransition(
                transition,
                board,
                registry,
                onComplete,
                Mathf.Max(1, motionEvent.SlideCellDistance));
        }

        void PlayErasureMotion(
            DiceController controller,
            DiceView diceView,
            OnlineDiceMotionEvent motionEvent,
            Action clearOverride) {
            Color? emission = (motionEvent.Flags & OnlineDiceMotionEvent.FlagHasEmissionOverride) != 0
                ? (Color)motionEvent.EmissionColor
                : null;
            diceView.PlayErasure(
                (ErasureKind)motionEvent.ErasureKind,
                board,
                motionEvent.TopFace,
                emission,
                () => {
                    clearOverride();
                    FinishRemovedDice(controller);
                });
        }

        void PlayOneVanishMotion(
            DiceController controller,
            DiceView diceView,
            OnlineDiceMotionEvent motionEvent,
            Action clearOverride) {
            if (oneVanishSettings == null) {
                Debug.LogError("OnlineClientEventBinder: OneVanish settings missing.");
                clearOverride();
                FinishRemovedDice(controller);
                return;
            }

            diceView.PlayOneVanish(oneVanishSettings, () => {
                clearOverride();
                FinishRemovedDice(controller);
            });
        }

        void FinishRemovedDice(DiceController controller) {
            if (controller == null) {
                return;
            }

            entityIds?.UnregisterDice(controller);
            controller.ForceDestroyForOverride();
        }
    }
}
