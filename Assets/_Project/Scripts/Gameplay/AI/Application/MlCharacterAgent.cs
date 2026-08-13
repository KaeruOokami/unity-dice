using System;
using System.Collections;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay.AI.Domain;
using DiceGame.Grid;
using DiceGame.Placement;
using DiceGame.Session;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Application
{
    /// <summary>
    /// ML-Agents character controller. Shares <see cref="AiCharacterInputSource"/> with rule AI.
    /// Ends episodes on match over (A) or max step timeout (B), then requests match reset.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class MlCharacterAgent : Agent, IAiDebugOverlaySource
    {
        CharacterController character;
        DiceRegistry registry;
        AiCharacterInputSource inputSource;
        MlAgentSettings settings;
        GameFlowController gameFlow;
        float[] observationBuffer;
        readonly AiDebugOverlaySnapshot debugOverlay = new AiDebugOverlaySnapshot();

        int lastActionId = MlDiscreteActions.Wait;
        int stepsThisEpisode;
        bool pulseConsumed;
        bool episodeClosing;
        bool hasPendingClose;
        float pendingTerminalReward;
        string pendingCloseReason;
        Vector2Int? pendingStepCell;
        Coroutine pendingCloseRoutine;

        public AiDebugOverlaySnapshot DebugOverlay => debugOverlay;
        public Board DebugBoard => registry != null ? registry.Board : null;
        public bool DebugGizmoEnabled => settings != null && settings.DebugGizmo;

        public void Configure(
            CharacterController targetCharacter,
            DiceRegistry targetRegistry,
            AiCharacterInputSource targetInputSource,
            MlAgentSettings targetSettings) {
            character = targetCharacter;
            registry = targetRegistry;
            inputSource = targetInputSource;
            settings = targetSettings;

            if (settings == null) {
                Debug.LogError("MlCharacterAgent.Configure: MlAgentSettings is null.");
                enabled = false;
                return;
            }

            // Episode length is owned here so timeout rewards can be applied before EndEpisode.
            MaxStep = 0;
            observationBuffer = new float[MlObservationEncoder.GetObservationSize(settings.MaxObservedDice)];
            lastActionId = MlDiscreteActions.Wait;
            stepsThisEpisode = 0;
            pulseConsumed = false;
            episodeClosing = false;
            hasPendingClose = false;
            pendingStepCell = null;
            AiDebugLog.Enabled = settings.DebugLog;
            BindGameFlow();
            EnsureDebugOverlayGizmo();
            LazyInitialize();
        }

        protected override void OnEnable() {
            base.OnEnable();
            BindGameFlow();
        }

        protected override void OnDisable() {
            CancelPendingClose();
            UnbindGameFlow();
            base.OnDisable();
        }

        public override void OnEpisodeBegin() {
            lastActionId = MlDiscreteActions.Wait;
            stepsThisEpisode = 0;
            pulseConsumed = false;
            episodeClosing = false;
            hasPendingClose = false;
            pendingStepCell = null;
            inputSource?.SetMove(Vector2.zero);
        }

        public override void CollectObservations(VectorSensor sensor) {
            if (character == null || registry == null || settings == null || observationBuffer == null) {
                WriteZeros(sensor, MlObservationEncoder.GetObservationSize(settings != null ? settings.MaxObservedDice : 0));
                return;
            }

            var snapshot = GameStateSnapshot.Capture(character, registry);
            MlObservationEncoder.Write(
                snapshot,
                registry.Board,
                settings.MaxObservedDice,
                observationBuffer);
            sensor.AddObservation(observationBuffer);
        }

        public override void OnActionReceived(ActionBuffers actions) {
            if (character == null || inputSource == null || settings == null || episodeClosing || hasPendingClose) {
                return;
            }

            if (gameFlow != null && gameFlow.IsSimulationFrozen) {
                return;
            }

            var actionId = actions.DiscreteActions.Length > 0
                ? actions.DiscreteActions[0]
                : MlDiscreteActions.Wait;

            if (actionId != lastActionId) {
                lastActionId = actionId;
                pulseConsumed = false;
            }

            ApplyAction(actionId);
            AddReward(settings.StepPenalty);
            if (character.CurrentDice != null) {
                AddReward(settings.StandingOnDieReward);
            }

            stepsThisEpisode++;
            if (settings.MaxEpisodeSteps > 0 && stepsThisEpisode >= settings.MaxEpisodeSteps) {
                BeginEpisodeClose(settings.TimeoutPenalty, "Timeout");
                return;
            }

            if (settings.DebugLog) {
                AiDebugLog.Log($"MlAction {MlDiscreteActions.ToLabel(actionId)}");
            }
        }

        public override void Heuristic(in ActionBuffers actionsOut) {
            var discrete = actionsOut.DiscreteActions;
            if (discrete.Length > 0) {
                discrete[0] = MlDiscreteActions.Wait;
            }
        }

        void LateUpdate() {
            RefreshDebugOverlay();
        }

        void OnMatchEnded(MatchEndEvent matchEnd) {
            if (!isActiveAndEnabled || episodeClosing || hasPendingClose || settings == null || character == null) {
                return;
            }

            var reward = ResolveTerminalReward(matchEnd);
            ScheduleEpisodeClose(reward, matchEnd.IsStandardGameOver ? "StandardGameOver" : "RoundEnd");
        }

        float ResolveTerminalReward(MatchEndEvent matchEnd) {
            if (matchEnd.IsStandardGameOver) {
                return settings.LoseReward;
            }

            if (!matchEnd.RoundWinner.HasValue) {
                return settings.DrawReward;
            }

            return matchEnd.RoundWinner.Value == character.PlayerSlot
                ? settings.WinReward
                : settings.LoseReward;
        }

        void ScheduleEpisodeClose(float terminalReward, string reason) {
            if (episodeClosing || hasPendingClose) {
                return;
            }

            hasPendingClose = true;
            pendingTerminalReward = terminalReward;
            pendingCloseReason = reason;
            if (pendingCloseRoutine != null) {
                StopCoroutine(pendingCloseRoutine);
            }

            pendingCloseRoutine = StartCoroutine(CloseEpisodeNextFrame());
        }

        IEnumerator CloseEpisodeNextFrame() {
            yield return null;
            pendingCloseRoutine = null;
            hasPendingClose = false;
            BeginEpisodeClose(pendingTerminalReward, pendingCloseReason);
        }

        void BeginEpisodeClose(float terminalReward, string reason) {
            if (episodeClosing) {
                return;
            }

            episodeClosing = true;
            AddReward(terminalReward);
            if (settings != null && settings.DebugLog) {
                AiDebugLog.Log($"MlEpisodeEnd reason={reason} reward={terminalReward}");
            }

            LazyInitialize();
            try {
                EndEpisode();
            } catch (Exception ex) {
                Debug.LogError($"MlCharacterAgent: EndEpisode failed ({reason}): {ex}");
            }

            // Only intentional episode closes reset the match (not Academy ForceReset / OnEpisodeBegin).
            RequestEnvironmentReset();
        }

        void CancelPendingClose() {
            hasPendingClose = false;
            if (pendingCloseRoutine == null) {
                return;
            }

            StopCoroutine(pendingCloseRoutine);
            pendingCloseRoutine = null;
        }

        void RequestEnvironmentReset() {
            var session = SessionState.Instance;
            if (session != null && session.IsOnline) {
                return;
            }

            if (gameFlow == null) {
                BindGameFlow();
            }

            if (gameFlow == null) {
                Debug.LogError("MlCharacterAgent: GameFlowController not found; cannot reset match.");
                return;
            }

            gameFlow.QueueTrainingMatchReset();
        }

        void BindGameFlow() {
            if (gameFlow == null) {
                gameFlow = FindFirstObjectByType<GameFlowController>();
            }

            if (gameFlow == null) {
                return;
            }

            gameFlow.MatchEnded -= OnMatchEnded;
            gameFlow.MatchEnded += OnMatchEnded;
        }

        void UnbindGameFlow() {
            if (gameFlow == null) {
                return;
            }

            gameFlow.MatchEnded -= OnMatchEnded;
        }

        void ApplyAction(int actionId) {
            pendingStepCell = null;

            if (MlDiscreteActions.TryGetMoveDirection(actionId, out var direction)) {
                inputSource.SetMove(CharacterController.DirectionToMoveVector(direction));
                pendingStepCell = character.StandingGridCell + direction.ToGridDelta();
                return;
            }

            inputSource.SetMove(Vector2.zero);

            if (actionId == MlDiscreteActions.Jump) {
                if (!pulseConsumed) {
                    inputSource.PulseJump();
                    pulseConsumed = true;
                }

                return;
            }

            if (actionId == MlDiscreteActions.Lift) {
                if (!pulseConsumed) {
                    inputSource.PulseLift();
                    pulseConsumed = true;
                }
            }
        }

        void EnsureDebugOverlayGizmo() {
            var gizmo = GetComponent<AiDebugOverlayGizmo>();
            if (gizmo == null) {
                gizmo = gameObject.AddComponent<AiDebugOverlayGizmo>();
            }

            gizmo.Bind(this);
        }

        void RefreshDebugOverlay() {
            if (!DebugGizmoEnabled || character == null || registry == null) {
                debugOverlay.Clear();
                return;
            }

            var snapshot = GameStateSnapshot.Capture(character, registry);
            debugOverlay.BeginFrame(
                AiDebugOverlayMode.Ml,
                0,
                snapshot.PlayerCell,
                "MlAgent",
                MlDiscreteActions.ToLabel(lastActionId));

            if (pendingStepCell.HasValue) {
                debugOverlay.SetStep(pendingStepCell.Value, pendingStepCell.Value);
            }

            if (snapshot.StandingDice != null) {
                debugOverlay.SetWorkDieCell(snapshot.StandingDice.CurrentState.GridPos);
            }
        }

        static void WriteZeros(VectorSensor sensor, int count) {
            for (var i = 0; i < count; i++) {
                sensor.AddObservation(0f);
            }
        }
    }
}
