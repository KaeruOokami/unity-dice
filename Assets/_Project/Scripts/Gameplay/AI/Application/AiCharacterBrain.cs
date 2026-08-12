using DiceGame.Config;
using DiceGame.Gameplay.AI.Application.Actions;
using DiceGame.Gameplay.AI.Domain;
using DiceGame.Grid;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Application
{
    [DefaultExecutionOrder(-50)]
    public sealed class AiCharacterBrain : MonoBehaviour
    {
        CharacterController character;
        DiceRegistry registry;
        AiCharacterInputSource inputSource;
        AiPlayerSettings settings;
        AiActionExecutor executor;
        AiExecutionContext executionContext;
        MatchGoal activeGoal;
        readonly MatchGoalFailureMemory failureMemory = new MatchGoalFailureMemory();
        readonly AiDebugOverlaySnapshot debugOverlay = new AiDebugOverlaySnapshot();
        AiFloorRecoverySession floorRecoverySession;
        int? pendingFloorRecoveryTrappedFace;
        float replanCooldown;
        int stuckActionCount;

        public AiDebugOverlaySnapshot DebugOverlay => debugOverlay;
        public Board DebugBoard => registry != null ? registry.Board : null;
        public bool DebugGizmoEnabled => settings != null && settings.DebugGizmo;

        public void Configure(
            CharacterController targetCharacter,
            DiceRegistry targetRegistry,
            AiCharacterInputSource targetInputSource,
            AiPlayerSettings targetSettings) {
            character = targetCharacter;
            registry = targetRegistry;
            inputSource = targetInputSource;
            settings = targetSettings;
            AiDebugLog.Enabled = targetSettings != null && targetSettings.DebugLog;
            executionContext = new AiExecutionContext(character, registry, inputSource, settings);
            executor = new AiActionExecutor();
            executor.Configure(executionContext);
            failureMemory.Clear();
            stuckActionCount = 0;
            EnsureDebugOverlayGizmo();
        }

        void LateUpdate() {
            RefreshDebugOverlay();
        }

        void Update() {
            if (character == null || registry == null || inputSource == null || settings == null || executor == null) {
                return;
            }

            executor.Tick();

            if (executor.TryConsumeCompletedAction(out var actionFailed)) {
                HandleCompletedAction(actionFailed);
            }

            if (!executor.IsReadyToPlan()) {
                return;
            }

            replanCooldown -= Time.deltaTime;
            if (replanCooldown > 0f) {
                return;
            }

            var snapshot = GameStateSnapshot.Capture(character, registry);
            if (TryHandleSinkingClusterEscape(snapshot)) {
                return;
            }

            if (TryHandleFloorRecovery(snapshot)) {
                return;
            }

            var goal = ResolveGoal(snapshot);
            if (goal == null) {
                replanCooldown = settings.IdleReplanInterval;
                return;
            }

            MatchGoalProgressSync.Sync(goal, snapshot);

            var subGoal = goal.GetNextIncompleteSubGoal();
            if (subGoal == null) {
                // Join (or other path) finished: drop goal so ResolveGoal can pick
                // the next expansion via roll-join or Lift-Join.
                if (TryBuildImmediateMatchAction(goal, snapshot, out var immediateAction)) {
                    executor.StartAction(immediateAction);
                    stuckActionCount = 0;
                    replanCooldown = settings.MinReplanInterval;
                } else {
                    activeGoal = null;
                    replanCooldown = settings.MinReplanInterval;
                }

                return;
            }

            var action = MatchGoalPlanner.BuildAction(goal, subGoal, snapshot, character, registry, settings);
            MatchGoalProgressSync.Sync(goal, snapshot);

            if (subGoal.IsComplete) {
                stuckActionCount = 0;
                replanCooldown = settings.MinReplanInterval;
                return;
            }

            if (action == null) {
                AiDebugLog.Log(
                    $"BuildAction FAILED subGoal={subGoal.Kind} targetCell={subGoal.TargetCell} " +
                    $"targetDie={(subGoal.TargetDie != null ? subGoal.TargetDie.name : "none")} " +
                    $"playerCell={snapshot.PlayerCell} stuck={stuckActionCount + 1}");

                RegisterPlanningFailure(subGoal);
                replanCooldown = settings.FailedReplanInterval;
                return;
            }

            AiDebugLog.Log(
                $"StartAction subGoal={subGoal.Kind} action={action.GetType().Name} " +
                $"targetCell={subGoal.TargetCell} " +
                $"targetDie={(subGoal.TargetDie != null ? subGoal.TargetDie.name : "none")} " +
                $"playerCell={snapshot.PlayerCell} " +
                $"currentDice={(character.CurrentDice != null ? character.CurrentDice.name : "none")}");
            executor.StartAction(action);
            replanCooldown = settings.MinReplanInterval;
        }

        void HandleCompletedAction(bool failed) {
            if (!failed) {
                stuckActionCount = 0;
                return;
            }

            stuckActionCount++;
            AiDebugLog.Log(
                $"ActionFailed stuck={stuckActionCount}/{GetStuckThreshold()} " +
                $"goalFace={(activeGoal != null ? activeGoal.Face.ToString() : "none")}");

            if (stuckActionCount >= GetStuckThreshold()) {
                AbandonActiveGoal("action-timeout");
            }
        }

        void RegisterPlanningFailure(AiSubGoal subGoal) {
            stuckActionCount++;

            if (subGoal != null && activeGoal != null) {
                if (subGoal.Kind == AiSubGoalKind.JoinCluster) {
                    // Roll-join destinations exhausted: only then fall back to Lift → Place.
                    if (!CanActiveWorkDieExtendCluster(subGoal)) {
                        if (TryFallbackJoinToLift()) {
                            return;
                        }

                        activeGoal.MarkUnplannable();
                        AbandonActiveGoal("join-exhausted");
                        return;
                    }
                } else if (subGoal.Kind == AiSubGoalKind.OrientDie) {
                    activeGoal.MarkUnplannable();
                }
            }

            if (stuckActionCount >= GetStuckThreshold() || (activeGoal != null && activeGoal.IsMarkedUnplannable)) {
                AbandonActiveGoal("build-failed");
            }
        }

        /// <summary>
        /// After roll-join can no longer extend, convert the same work die to Lift → Place when feasible.
        /// Must not run while Join is still progressable.
        /// </summary>
        bool TryFallbackJoinToLift() {
            if (activeGoal == null || character == null || registry == null) {
                return false;
            }

            var snapshot = GameStateSnapshot.Capture(character, registry);
            if (!MatchGoalLiftPreference.TryConvertToLiftJoin(activeGoal, snapshot, registry)) {
                return false;
            }

            AiDebugLog.Log(
                $"LiftPrefer after join-exhausted face={activeGoal.Face} " +
                $"work={(activeGoal.ParticipantTarget != null ? activeGoal.ParticipantTarget.name : "none")}");
            MatchGoalProgressSync.Sync(activeGoal, snapshot);
            stuckActionCount = 0;
            replanCooldown = settings != null ? settings.MinReplanInterval : 0f;
            return true;
        }

        bool CanActiveWorkDieExtendCluster(AiSubGoal joinSubGoal = null) {
            if (activeGoal == null
                || activeGoal.ParticipantTarget == null
                || activeGoal.ClusterDice == null
                || activeGoal.ClusterDice.Count == 0
                || activeGoal.Face < 2
                || character == null
                || registry == null) {
                return false;
            }

            var snapshot = GameStateSnapshot.Capture(character, registry);
            return WorkDieSlidePlanner.TrySelectJoinTargetCell(
                activeGoal.ClusterDice,
                new DiceSnapshot(activeGoal.ParticipantTarget),
                snapshot.PlanningDice,
                registry,
                snapshot.VersusLayout,
                snapshot.PlayerSlot,
                out _,
                out _,
                joinSubGoal != null ? joinSubGoal.FailedJoinSlotKeys : null);
        }

        void AbandonActiveGoal(string reason) {
            if (activeGoal != null) {
                activeGoal.MarkUnplannable();
                failureMemory.RememberFailure(
                    activeGoal,
                    settings != null ? settings.GoalFailureBlacklistSeconds : 0f,
                    Time.time);
                AiDebugLog.Log(
                    $"AbandonGoal reason={reason} face={activeGoal.Face} score={activeGoal.PriorityScore:F1} " +
                    $"workDie={(activeGoal.ParticipantTarget != null ? activeGoal.ParticipantTarget.name : "none")} " +
                    $"blacklistSec={(settings != null ? settings.GoalFailureBlacklistSeconds : 0f)}");
            }

            activeGoal = null;
            stuckActionCount = 0;
        }

        int GetStuckThreshold() {
            return settings != null ? Mathf.Max(1, settings.StuckAttemptsBeforeGoalReset) : 3;
        }

        bool TryHandleSinkingClusterEscape(GameStateSnapshot snapshot) {
            var escapeKind = AiSinkingClusterEscapePlanner.ResolveEscape(
                snapshot,
                settings,
                out var clusterFace,
                out _,
                out var mountTarget);

            if (escapeKind == AiSinkingClusterEscapeKind.None) {
                return false;
            }

            activeGoal = null;

            if (escapeKind == AiSinkingClusterEscapeKind.MountAdjacent) {
                if (AiSinkingClusterEscapeCoordinator.TryBuildMountAdjacentAction(
                    mountTarget,
                    snapshot,
                    character,
                    registry,
                    settings,
                    out var mountAction)) {
                    stuckActionCount = 0;
                    AiDebugLog.Log(
                        $"StartSinkingMount face={clusterFace} " +
                        $"die={(mountTarget != null ? mountTarget.name : "none")} " +
                        $"action={mountAction.GetType().Name} playerCell={snapshot.PlayerCell}");
                    executor.StartAction(mountAction);
                    replanCooldown = settings.MinReplanInterval;
                    return true;
                }

                // Adjacent target exists but cannot step onto it this frame — fall through to descend.
                AiDebugLog.Log(
                    $"SinkingMount FAILED face={clusterFace} " +
                    $"die={(mountTarget != null ? mountTarget.name : "none")} fallback=descend");
            }

            pendingFloorRecoveryTrappedFace = clusterFace;

            if (AiSinkingClusterEscapePlanner.NeedsDescent(snapshot)) {
                if (!AiSinkingClusterEscapeCoordinator.TryBuildDescendAction(
                    snapshot,
                    character,
                    settings,
                    out var action)) {
                    replanCooldown = settings.FailedReplanInterval;
                    return true;
                }

                stuckActionCount = 0;
                AiDebugLog.Log(
                    $"StartSinkingDescent face={clusterFace} action={action.GetType().Name} " +
                    $"playerCell={snapshot.PlayerCell}");
                executor.StartAction(action);
                replanCooldown = settings.MinReplanInterval;
                return true;
            }

            BeginFloorRecovery(snapshot, clusterFace);
            return false;
        }

        bool TryHandleFloorRecovery(GameStateSnapshot snapshot) {
            if (floorRecoverySession == null) {
                if (!AiFloorRecoveryPlanner.NeedsRecovery(snapshot)) {
                    return false;
                }

                BeginFloorRecovery(snapshot, pendingFloorRecoveryTrappedFace);
                pendingFloorRecoveryTrappedFace = null;
                if (floorRecoverySession == null) {
                    return false;
                }
            }

            if (AiFloorRecoveryPlanner.IsRecoveryComplete(snapshot, floorRecoverySession)) {
                AiDebugLog.Log(
                    $"FloorRecoveryComplete die={snapshot.StandingDice.name} " +
                    $"phase={floorRecoverySession.Phase}");
                floorRecoverySession = null;
                replanCooldown = settings.MinReplanInterval;
                return true;
            }

            // Unintended standing die: keep session, allow normal goals until back on floor.
            if (snapshot.StandingDice != null) {
                return false;
            }

            if (!snapshot.PlayerIsOnFloor) {
                return false;
            }

            activeGoal = null;

            if (!AiFloorRecoveryCoordinator.TryBuildAction(
                floorRecoverySession,
                snapshot,
                registry,
                character,
                settings,
                out var action)) {
                replanCooldown = settings.FailedReplanInterval;
                return true;
            }

            if (action == null) {
                replanCooldown = settings.IdleReplanInterval;
                return true;
            }

            stuckActionCount = 0;
            AiDebugLog.Log(
                $"StartFloorRecovery phase={floorRecoverySession.Phase} action={action.GetType().Name} " +
                $"playerCell={snapshot.PlayerCell}");
            executor.StartAction(action);
            replanCooldown = settings.MinReplanInterval;
            return true;
        }

        void BeginFloorRecovery(GameStateSnapshot snapshot, int? sourceTrappedFace) {
            if (!AiFloorRecoveryPlanner.NeedsRecovery(snapshot)) {
                return;
            }

            floorRecoverySession = AiFloorRecoveryPlanner.CreateSession(
                snapshot,
                registry,
                settings,
                sourceTrappedFace);
            AiDebugLog.Log(
                $"FloorRecoveryStart phase={floorRecoverySession.Phase} " +
                $"sourceFace={(sourceTrappedFace.HasValue ? sourceTrappedFace.Value.ToString() : "none")} " +
                $"alternate={(floorRecoverySession.AlternateWorkDie != null ? floorRecoverySession.AlternateWorkDie.name : "none")} " +
                $"spawn={(floorRecoverySession.SpawnDie != null ? floorRecoverySession.SpawnDie.name : "none")}");
        }

        MatchGoal ResolveGoal(GameStateSnapshot snapshot) {
            if (activeGoal != null) {
                MatchGoalProgressSync.Sync(activeGoal, snapshot);

                if (!activeGoal.IsStale(snapshot, settings, registry) && !activeGoal.AreAllSubGoalsComplete()) {
                    var candidate = MatchGoalSelector.SelectBest(
                        snapshot,
                        character,
                        registry,
                        settings,
                        failureMemory);
                    if (candidate != null
                        && activeGoal.ShouldSwitchTo(candidate, settings.GoalSwitchMargin)
                        && activeGoal.AllowsWorkDieSwitch(candidate, snapshot, registry)) {
                        activeGoal = candidate;
                        stuckActionCount = 0;
                    }

                    return activeGoal;
                }

                activeGoal = null;
            }

            activeGoal = MatchGoalSelector.SelectBest(snapshot, character, registry, settings, failureMemory);
            if (activeGoal == null) {
                return null;
            }

            stuckActionCount = 0;
            AiDebugLog.Log($"NewGoal face={activeGoal.Face} score={activeGoal.PriorityScore:F1} immediate={activeGoal.IsImmediateMatch}");
            foreach (var subGoal in activeGoal.SubGoals) {
                AiDebugLog.Log(
                    $"  SubGoal kind={subGoal.Kind} face={subGoal.TargetFace} " +
                    $"die={(subGoal.TargetDie != null ? subGoal.TargetDie.name : "none")} " +
                    $"cell={subGoal.TargetCell} tier={subGoal.TargetTier}");
            }

            return activeGoal;
        }

        void OnDisable() {
            executor?.Cancel();
            inputSource?.SetMove(Vector2.zero);
            activeGoal = null;
            floorRecoverySession = null;
            pendingFloorRecoveryTrappedFace = null;
            stuckActionCount = 0;
            failureMemory.Clear();
        }

        bool TryBuildImmediateMatchAction(MatchGoal goal, GameStateSnapshot snapshot, out AiDiscreteAction action) {
            action = null;
            if (goal == null || !goal.IsImmediateMatch || goal.ParticipantTarget == null) {
                return false;
            }

            if (snapshot.StandingDice != goal.ParticipantTarget) {
                return false;
            }

            if (settings == null || !settings.AllowJump) {
                return false;
            }

            if (character.IsJumping || character.IsBusy) {
                return false;
            }

            var die = goal.ParticipantTarget;
            if (die.IsErasing || die.IsVanishing || die.IsSinkErasing) {
                return false;
            }

            if (!die.CanJumpCoupleWithPlayer) {
                AiDebugLog.Log(
                    $"ImmediateMatch blocked die={die.name} reason=cannot-jump-couple");
                return false;
            }

            action = new SameCellJumpAction(die, settings.JumpMoveMaxFrames);
            AiDebugLog.Log(
                $"StartImmediateMatch jump die={die.name} cell={die.CurrentState.GridPos} " +
                $"face={goal.Face}");
            return true;
        }

        void EnsureDebugOverlayGizmo() {
            var gizmo = GetComponent<AiDebugOverlayGizmo>();
            if (gizmo == null) {
                gizmo = gameObject.AddComponent<AiDebugOverlayGizmo>();
            }

            gizmo.Bind(this);
        }

        void RefreshDebugOverlay() {
            if (!DebugGizmoEnabled || character == null || registry == null || settings == null) {
                debugOverlay.Clear();
                return;
            }

            var snapshot = GameStateSnapshot.Capture(character, registry);
            var playerCell = snapshot.PlayerCell;
            var escapeKind = AiSinkingClusterEscapePlanner.ResolveEscape(
                snapshot,
                settings,
                out var escapeFace,
                out _,
                out var mountTarget);

            if (escapeKind == AiSinkingClusterEscapeKind.MountAdjacent && mountTarget != null) {
                debugOverlay.BeginFrame(
                    AiDebugOverlayMode.SinkingMount,
                    escapeFace,
                    playerCell,
                    "SinkingMount",
                    executor?.CurrentAction != null ? executor.CurrentAction.GetType().Name : "-");
                debugOverlay.SetHighlightCell(mountTarget.CurrentState.GridPos);
                debugOverlay.SetWorkDieCell(mountTarget.CurrentState.GridPos);
                ApplyActionGeometry(debugOverlay);
                return;
            }

            if (escapeKind == AiSinkingClusterEscapeKind.DescendToFloor) {
                debugOverlay.BeginFrame(
                    AiDebugOverlayMode.SinkingDescend,
                    escapeFace,
                    playerCell,
                    "SinkingDescend",
                    executor?.CurrentAction != null ? executor.CurrentAction.GetType().Name : "-");
                ApplyActionGeometry(debugOverlay);
                if (debugOverlay.StepCell.HasValue) {
                    debugOverlay.SetHighlightCell(debugOverlay.StepCell.Value);
                }

                return;
            }

            if (floorRecoverySession != null && AiFloorRecoveryPlanner.NeedsRecovery(snapshot)) {
                var phaseLabel = floorRecoverySession.Phase.ToString();
                debugOverlay.BeginFrame(
                    AiDebugOverlayMode.FloorRecovery,
                    floorRecoverySession.SourceTrappedFace ?? 0,
                    playerCell,
                    phaseLabel,
                    executor?.CurrentAction != null ? executor.CurrentAction.GetType().Name : "-");
                if (floorRecoverySession.AlternateWorkDie != null) {
                    var cell = floorRecoverySession.AlternateWorkDie.CurrentState.GridPos;
                    debugOverlay.SetHighlightCell(cell);
                    debugOverlay.SetWorkDieCell(cell);
                } else if (floorRecoverySession.SpawnDie != null) {
                    var cell = floorRecoverySession.SpawnDie.CurrentState.GridPos;
                    debugOverlay.SetHighlightCell(cell);
                    debugOverlay.SetWorkDieCell(cell);
                }

                ApplyActionGeometry(debugOverlay);
                return;
            }

            if (activeGoal == null) {
                debugOverlay.BeginFrame(
                    AiDebugOverlayMode.Idle,
                    0,
                    playerCell,
                    "-",
                    executor?.CurrentAction != null ? executor.CurrentAction.GetType().Name : "-");
                ApplyActionGeometry(debugOverlay);
                return;
            }

            var subGoal = activeGoal.GetNextIncompleteSubGoal();
            debugOverlay.BeginFrame(
                AiDebugOverlayMode.Goal,
                activeGoal.Face,
                playerCell,
                subGoal != null ? subGoal.Kind.ToString() : "complete",
                executor?.CurrentAction != null ? executor.CurrentAction.GetType().Name : "-");

            if (activeGoal.ClusterDice != null) {
                for (var i = 0; i < activeGoal.ClusterDice.Count; i++) {
                    debugOverlay.AddClusterCell(activeGoal.ClusterDice[i].GridPos);
                }
            }

            if (subGoal != null) {
                debugOverlay.SetSubGoalTarget(subGoal.TargetCell);
                if (subGoal.TargetDie != null) {
                    debugOverlay.SetWorkDieCell(subGoal.TargetDie.CurrentState.GridPos);
                }

                AppendSlidePlanPath(debugOverlay, subGoal);
            }

            ApplyActionGeometry(debugOverlay);
        }

        static void AppendSlidePlanPath(AiDebugOverlaySnapshot overlay, AiSubGoal subGoal) {
            WorkDieSlidePlan? plan = null;
            var stepIndex = 0;
            if (subGoal.HasJoinSlidePlan) {
                plan = subGoal.JoinSlidePlan;
                stepIndex = subGoal.JoinSlideStepIndex;
            } else if (subGoal.HasOrientRollPlan) {
                plan = subGoal.OrientRollPlan;
                stepIndex = subGoal.OrientRollStepIndex;
            }

            if (!plan.HasValue || plan.Value.Directions == null) {
                return;
            }

            var slide = plan.Value;
            for (var i = stepIndex; i <= slide.Directions.Count; i++) {
                if (!WorkDieSlidePlanner.TrySimulateAfterSteps(slide, i, out var cell, out _)) {
                    break;
                }

                overlay.AddPlanPathCell(cell);
            }
        }

        void ApplyActionGeometry(AiDebugOverlaySnapshot overlay) {
            if (executor?.CurrentAction is IAiDebugStepGeometry geometry) {
                overlay.SetStep(geometry.DebugStepCell, geometry.DebugGoalCell);
            }
        }
    }
}
