using System;
using System.Collections;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay.AI.Domain;
using DiceGame.Grid;
using DiceGame.Placement;
using DiceGame.Session;
using DiceGame.Versus;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
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
        Board observationBoard;
        AiCharacterInputSource inputSource;
        MlAgentSettings settings;
        MlCurriculumStage curriculumStage;
        GameFlowController gameFlow;
        DiceMatchErasureSystem erasureSystem;
        float[] observationBuffer;
        int expectedObservationSize;
        readonly AiDebugOverlaySnapshot debugOverlay = new AiDebugOverlaySnapshot();

        int lastActionId = MlDiscreteActions.Wait;
        int stepsThisEpisode;
        float lastProgressScore;
        bool pulseConsumed;
        bool episodeClosing;
        bool hasPendingClose;
        float pendingTerminalReward;
        string pendingCloseReason;
        Vector2Int? pendingStepCell;
        Coroutine pendingCloseRoutine;

        public AiDebugOverlaySnapshot DebugOverlay => debugOverlay;
        public Board DebugBoard => observationBoard != null
            ? observationBoard
            : registry != null ? registry.Board : null;
        public bool DebugGizmoEnabled => settings != null && settings.DebugGizmo;

        public void Configure(
            CharacterController targetCharacter,
            DiceRegistry targetRegistry,
            AiCharacterInputSource targetInputSource,
            MlAgentSettings targetSettings,
            MlCurriculumStage targetCurriculumStage = null,
            Board targetBoard = null) {
            character = targetCharacter;
            registry = targetRegistry;
            inputSource = targetInputSource;
            settings = targetSettings;
            curriculumStage = targetCurriculumStage;
            observationBoard = targetBoard != null
                ? targetBoard
                : targetRegistry != null ? targetRegistry.Board : null;

            if (settings == null) {
                Debug.LogError("MlCharacterAgent.Configure: MlAgentSettings is null.");
                enabled = false;
                return;
            }

            // Episode length is owned here so timeout rewards can be applied before EndEpisode.
            MaxStep = 0;
            expectedObservationSize = ResolveObservationSize(observationBoard);
            EnsureObservationBuffer(expectedObservationSize);
            if (observationBoard != null
                && (observationBoard.Width > 1 || observationBoard.Height > 1)
                && expectedObservationSize <= MlObservationEncoder.GetObservationSize(1, 1)) {
                Debug.LogError(
                    "MlCharacterAgent.Configure: Observation size collapsed to 1x1 fallback " +
                    $"despite board {observationBoard.Width}x{observationBoard.Height}.");
            }

            lastActionId = MlDiscreteActions.Wait;
            stepsThisEpisode = 0;
            lastProgressScore = 0f;
            pulseConsumed = false;
            episodeClosing = false;
            hasPendingClose = false;
            pendingStepCell = null;
            AiDebugLog.Enabled = settings.DebugLog;
            BindGameFlow();
            BindErasureSystem();
            EnsureDebugOverlayGizmo();
            LazyInitialize();
        }

        protected override void OnEnable() {
            base.OnEnable();
            BindGameFlow();
            BindErasureSystem();
        }

        protected override void OnDisable() {
            CancelPendingClose();
            UnbindGameFlow();
            UnbindErasureSystem();
            base.OnDisable();
        }

        public override void OnEpisodeBegin() {
            lastActionId = MlDiscreteActions.Wait;
            stepsThisEpisode = 0;
            lastProgressScore = CaptureProgressScore();
            pulseConsumed = false;
            episodeClosing = false;
            hasPendingClose = false;
            pendingStepCell = null;
            inputSource?.SetMove(Vector2.zero);
            SyncObservationBufferToBoard();
        }

        public override void CollectObservations(VectorSensor sensor) {
            SyncObservationBufferToBoard();
            var expected = expectedObservationSize > 0
                ? expectedObservationSize
                : ResolveObservationSize(observationBoard ?? registry?.Board);

            if (character == null || registry == null || settings == null || observationBuffer == null) {
                WriteZeros(sensor, expected);
                return;
            }

            var boardForWrite = observationBoard != null ? observationBoard : registry.Board;
            if (boardForWrite == null) {
                WriteZeros(sensor, expected);
                return;
            }

            var writeSize = MlObservationEncoder.GetObservationSize(boardForWrite);
            EnsureObservationBuffer(Mathf.Max(expected, writeSize));
            var snapshot = GameStateSnapshot.Capture(character, registry);
            MlObservationEncoder.Write(snapshot, boardForWrite, observationBuffer);

            // Emit exactly the BehaviorParameters size; pad/truncate explicitly.
            for (var i = 0; i < expected; i++) {
                sensor.AddObservation(i < writeSize ? observationBuffer[i] : 0f);
            }
        }

        void SyncObservationBufferToBoard() {
            var boardForSize = observationBoard != null ? observationBoard : registry?.Board;
            if (boardForSize == null) {
                return;
            }

            var size = ResolveObservationSize(boardForSize);
            if (size != expectedObservationSize || observationBuffer == null || observationBuffer.Length != size) {
                expectedObservationSize = size;
                EnsureObservationBuffer(size);
            }

            var behaviorParameters = GetComponent<BehaviorParameters>();
            if (behaviorParameters != null
                && behaviorParameters.BrainParameters.VectorObservationSize != expectedObservationSize) {
                behaviorParameters.BrainParameters.VectorObservationSize = expectedObservationSize;
            }
        }

        void EnsureObservationBuffer(int size) {
            size = Mathf.Max(1, size);
            if (observationBuffer == null || observationBuffer.Length != size) {
                observationBuffer = new float[size];
            }
        }

        static int ResolveObservationSize(Board board) {
            return MlObservationEncoder.GetObservationSize(board);
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
            ApplyProgressShapingReward();

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

        void OnErasureResolved(ErasureResolvedEvent erasureEvent) {
            if (!isActiveAndEnabled || episodeClosing || hasPendingClose || settings == null || character == null) {
                return;
            }

            if (erasureEvent.Attacker != character.PlayerSlot) {
                return;
            }

            var reward = settings.ErasureBaseReward
                + settings.ErasurePerClusterWeight * erasureEvent.ClusterSize;

            if (erasureEvent.ChainCount > 1) {
                reward += settings.ChainBonusPerLink * (erasureEvent.ChainCount - 1);
            }

            if (erasureEvent.IsSnatch) {
                reward += settings.SnatchBonus;
            }

            AddReward(reward);
            SyncProgressScoreBaseline();

            if (settings.DebugLog) {
                AiDebugLog.Log(
                    $"MlErasure face={erasureEvent.Face} cluster={erasureEvent.ClusterSize} " +
                    $"chain={erasureEvent.ChainCount} snatch={erasureEvent.IsSnatch} reward={reward}");
            }

            if (curriculumStage != null && curriculumStage.EndEpisodeOnErasure) {
                ScheduleEpisodeClose(settings.WinReward, "CurriculumErasure");
            }
        }

        void ApplyProgressShapingReward() {
            if (character == null || registry == null || settings == null || !settings.ProgressShapingEnabled) {
                return;
            }

            var snapshot = GameStateSnapshot.Capture(character, registry);
            var currentScore = MlProgressRewardEvaluator.ComputeProgressScore(snapshot, settings);
            var growth = MlProgressRewardEvaluator.ComputeGrowthReward(currentScore, lastProgressScore);
            if (growth > 0f) {
                AddReward(growth);
            }

            var hold = MlProgressRewardEvaluator.ComputeHoldReward(currentScore, settings);
            if (hold > 0f) {
                AddReward(hold);
            }

            lastProgressScore = currentScore;
        }

        void SyncProgressScoreBaseline() {
            lastProgressScore = CaptureProgressScore();
        }

        float CaptureProgressScore() {
            if (character == null || registry == null || settings == null) {
                return 0f;
            }

            var snapshot = GameStateSnapshot.Capture(character, registry);
            return MlProgressRewardEvaluator.ComputeProgressScore(snapshot, settings);
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

        void BindErasureSystem() {
            if (erasureSystem == null) {
                erasureSystem = FindFirstObjectByType<DiceMatchErasureSystem>();
            }

            if (erasureSystem == null) {
                return;
            }

            erasureSystem.ErasureResolved -= OnErasureResolved;
            erasureSystem.ErasureResolved += OnErasureResolved;
        }

        void UnbindErasureSystem() {
            if (erasureSystem == null) {
                return;
            }

            erasureSystem.ErasureResolved -= OnErasureResolved;
        }

        void ApplyAction(int actionId) {
            pendingStepCell = null;

            if (character.IsLiftCarrying
                && MlDiscreteActions.TryGetMoveDirection(actionId, out var placeDirection)) {
                inputSource.SetMove(Vector2.zero);
                inputSource.PulseDirection(placeDirection);
                pendingStepCell = character.StandingGridCell + placeDirection.ToGridDelta();
                return;
            }

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
