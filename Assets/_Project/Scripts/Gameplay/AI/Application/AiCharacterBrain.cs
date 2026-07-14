using DiceGame.Config;
using DiceGame.Gameplay.AI.Application.Actions;
using DiceGame.Gameplay.AI.Domain;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Application
{
    [DefaultExecutionOrder(-50)]
    public sealed class AiCharacterBrain : MonoBehaviour
    {
        const int StuckAttemptsBeforeGoalReset = 3;

        CharacterController character;
        DiceRegistry registry;
        AiCharacterInputSource inputSource;
        AiPlayerSettings settings;
        AiActionExecutor executor;
        AiExecutionContext executionContext;
        MatchGoal activeGoal;
        AiFloorRecoverySession floorRecoverySession;
        int? pendingFloorRecoveryTrappedFace;
        float replanCooldown;
        int stuckActionCount;

        public void Configure(
            CharacterController targetCharacter,
            DiceRegistry targetRegistry,
            AiCharacterInputSource targetInputSource,
            AiPlayerSettings targetSettings) {
            character = targetCharacter;
            registry = targetRegistry;
            inputSource = targetInputSource;
            settings = targetSettings;
            executionContext = new AiExecutionContext(character, registry, inputSource, settings);
            executor = new AiActionExecutor();
            executor.Configure(executionContext);
        }

        void Update() {
            if (character == null || registry == null || inputSource == null || settings == null || executor == null) {
                return;
            }

            executor.Tick();

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

                stuckActionCount++;
                if (stuckActionCount >= StuckAttemptsBeforeGoalReset) {
                    activeGoal = null;
                    stuckActionCount = 0;
                }

                replanCooldown = settings.FailedReplanInterval;
                return;
            }

            stuckActionCount = 0;
            AiDebugLog.Log(
                $"StartAction subGoal={subGoal.Kind} action={action.GetType().Name} " +
                $"targetCell={subGoal.TargetCell} " +
                $"targetDie={(subGoal.TargetDie != null ? subGoal.TargetDie.name : "none")} " +
                $"playerCell={snapshot.PlayerCell} " +
                $"currentDice={(character.CurrentDice != null ? character.CurrentDice.name : "none")}");
            executor.StartAction(action);
            replanCooldown = settings.MinReplanInterval;
        }

        bool TryHandleSinkingClusterEscape(GameStateSnapshot snapshot) {
            var trapped = AiSinkingClusterEscapePlanner.IsTrappedOnSinkingCluster(
                snapshot,
                settings,
                out var trappedFace,
                out _);

            if (!trapped) {
                return false;
            }

            activeGoal = null;

            if (AiSinkingClusterEscapePlanner.NeedsDescent(snapshot)) {
                pendingFloorRecoveryTrappedFace = trappedFace;

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
                    $"StartSinkingDescent face={trappedFace} action={action.GetType().Name} " +
                    $"playerCell={snapshot.PlayerCell}");
                executor.StartAction(action);
                replanCooldown = settings.MinReplanInterval;
                return true;
            }

            BeginFloorRecovery(snapshot, trappedFace);
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

            if (!AiFloorRecoveryPlanner.NeedsRecovery(snapshot)) {
                floorRecoverySession = null;
                return false;
            }

            activeGoal = null;

            if (AiFloorRecoveryPlanner.IsRecoveryComplete(snapshot, floorRecoverySession)) {
                AiDebugLog.Log(
                    $"FloorRecoveryComplete die={snapshot.StandingDice.name} " +
                    $"phase={floorRecoverySession.Phase}");
                floorRecoverySession = null;
                replanCooldown = settings.MinReplanInterval;
                return true;
            }

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

                if (!activeGoal.IsStale(snapshot) && !activeGoal.AreAllSubGoalsComplete()) {
                    var candidate = MatchGoalSelector.SelectBest(snapshot, character, registry, settings);
                    if (candidate != null && activeGoal.ShouldSwitchTo(candidate, settings.GoalSwitchMargin)) {
                        activeGoal = candidate;
                    }

                    return activeGoal;
                }
            }

            activeGoal = MatchGoalSelector.SelectBest(snapshot, character, registry, settings);
            if (activeGoal == null) {
                return null;
            }

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
    }
}
