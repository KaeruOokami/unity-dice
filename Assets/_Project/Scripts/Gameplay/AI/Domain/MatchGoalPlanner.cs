using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay.Character;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    public static class MatchGoalPlanner
    {
        public static Application.AiDiscreteAction BuildAction(
            MatchGoal goal,
            AiSubGoal subGoal,
            GameStateSnapshot snapshot,
            CharacterController character,
            DiceRegistry registry,
            AiPlayerSettings settings) {
            if (goal == null || subGoal == null || character == null) {
                return null;
            }

            if (snapshot.PlayerIsCarrying || subGoal.Kind == AiSubGoalKind.PlaceCarriedDie) {
                return BuildPlaceCarriedAction(subGoal, character);
            }

            return subGoal.Kind switch {
                AiSubGoalKind.ReachParticipant => BuildReachAction(goal, subGoal, snapshot, character, registry, settings),
                AiSubGoalKind.ReachWorkDie => BuildReachAction(goal, subGoal, snapshot, character, registry, settings),
                AiSubGoalKind.OrientDie => BuildOrientAction(goal, subGoal, snapshot, character, registry, settings),
                AiSubGoalKind.JoinCluster => BuildJoinClusterAction(goal, subGoal, snapshot, character, registry, settings),
                AiSubGoalKind.LiftDie => BuildLiftAction(goal, subGoal, snapshot, character, registry, settings),
                _ => BuildReachAction(goal, subGoal, snapshot, character, registry, settings)
            };
        }

        static Application.AiDiscreteAction BuildPlaceCarriedAction(AiSubGoal subGoal, CharacterController character) {
            var origin = character.StandingGridCell;
            var delta = subGoal.TargetCell - origin;

            Direction? placeDirection = null;
            if (delta.x == 1 && delta.y == 0) {
                placeDirection = Direction.East;
            } else if (delta.x == -1 && delta.y == 0) {
                placeDirection = Direction.West;
            } else if (delta.x == 0 && delta.y == 1) {
                placeDirection = Direction.North;
            } else if (delta.x == 0 && delta.y == -1) {
                placeDirection = Direction.South;
            }

            if (!placeDirection.HasValue || !character.CanPlaceCarriedAt(placeDirection.Value)) {
                Application.AiDebugLog.Log(
                    $"PlaceCarried FAILED origin={origin} target={subGoal.TargetCell} " +
                    $"dir={(placeDirection.HasValue ? placeDirection.Value.ToString() : "none")}");
                return null;
            }

            return new Application.Actions.PlaceCarriedDiceAction(placeDirection.Value);
        }

        static Application.AiDiscreteAction BuildReachAction(
            MatchGoal goal,
            AiSubGoal subGoal,
            GameStateSnapshot snapshot,
            CharacterController character,
            DiceRegistry registry,
            AiPlayerSettings settings) {
            var targetDie = subGoal.TargetDie;
            if (IsStandingOnTarget(snapshot, targetDie)) {
                subGoal.MarkComplete();
                return null;
            }

            if (targetDie == null) {
                return null;
            }

            // Always use live die cell; stale SubGoal.TargetCell alone conflates Top/Bottom stacks.
            var targetCell = targetDie.CurrentState.GridPos;
            var avoidClusterCells = subGoal.Kind == AiSubGoalKind.ReachWorkDie
                && goal?.ClusterDice != null
                && !ClusterSelectionEvaluator.ClusterContainsController(goal.ClusterDice, targetDie);

            // Same grid as the die but standing on another stack tier (e.g. Top over Bottom target).
            if (snapshot.PlayerCell == targetCell
                && snapshot.StandingDice != null
                && snapshot.StandingDice != targetDie) {
                Application.AiDebugLog.Log(
                    $"ReachSameCellWrongTier player={snapshot.PlayerCell} " +
                    $"standOn={snapshot.StandingDice.name} " +
                    $"target={targetDie.name} tier={targetDie.CurrentState.Tier}");
                if (TryBuildLeaveCellAction(snapshot, character, settings, targetCell, out var leaveAction)) {
                    return leaveAction;
                }

                return null;
            }

            if (snapshot.PlayerCell == targetCell) {
                var inCellDirection = GetFacingDirectionTowardDie(snapshot, targetDie)
                    ?? DiceBoardAnalyzer.GetPrimaryDirectionToward(snapshot.PlayerCell, targetCell);
                if (inCellDirection.HasValue) {
                    var maxFrames = settings != null ? settings.MoveActionMaxFrames : 30;
                    Application.AiDebugLog.Log(
                        $"SelectInCellStandOn player={snapshot.PlayerCell} die={targetDie.name} dir={inCellDirection.Value}");

                    return new Application.Actions.MoveInDirectionAction(
                        inCellDirection.Value,
                        maxFrames,
                        targetCell,
                        Application.Actions.MoveActionPurpose.StandOnDie,
                        targetDie);
                }
            }

            var constraints = avoidClusterCells
                ? AiReachExternalWorkDieNavigation.ResolveConstraints(
                    snapshot,
                    goal.ClusterDice,
                    goal.Face)
                : AiNavigationConstraints.None;
            if (avoidClusterCells && constraints.ProtectedCluster == null) {
                Application.AiDebugLog.Log(
                    $"ReachWorkDie allow-cluster-roll player={snapshot.PlayerCell} " +
                    $"standOn={(snapshot.StandingDice != null ? snapshot.StandingDice.name : "none")} " +
                    $"goal={targetCell}");
            }

            return BuildStandOnDieMovement(
                targetDie,
                snapshot,
                character,
                registry,
                settings,
                constraints);
        }

        /// <summary>
        /// Climbing onto a higher-tier die while standing on another die cannot be done by Jump.
        /// Leave the current die first; if leave is impossible, fail planning instead of Jump-looping.
        /// </summary>
        static Application.AiDiscreteAction BuildStandOnDieMovement(
            DiceController targetDie,
            GameStateSnapshot snapshot,
            CharacterController character,
            DiceRegistry registry,
            AiPlayerSettings settings,
            AiNavigationConstraints constraints) {
            if (targetDie == null || snapshot == null) {
                return null;
            }

            // Leave-to-climb is not a separate policy: BFS toward the target die yields the
            // same first step (often walking off the current footing) without sink-blind leave.
            var preferJump = settings == null || settings.AllowJump;

            return SelectMovementAction(
                targetDie.CurrentState.GridPos,
                snapshot,
                character,
                registry,
                settings,
                preferJump,
                standOnDie: targetDie,
                constraints);
        }

        static bool RequiresJumpToStandOn(GameStateSnapshot snapshot, DiceController targetDie) {
            if (snapshot == null || targetDie == null) {
                return false;
            }

            var targetLevel = SurfaceHeightLevel.FromDiceStackTier(targetDie.CurrentState.Tier);
            var standingLevel = snapshot.StandingDice != null
                ? SurfaceHeightLevel.FromDiceStackTier(snapshot.StandingDice.CurrentState.Tier)
                : SurfaceHeightLevel.Floor;
            return targetLevel > standingLevel;
        }

        static bool TryBuildLeaveCellAction(
            GameStateSnapshot snapshot,
            CharacterController character,
            AiPlayerSettings settings,
            Vector2Int blockedCell,
            out Application.AiDiscreteAction action,
            Vector2Int? preferredLeaveCell = null) {
            action = null;
            if (!character.TryGetAiNavigationQuery(out var passability, out var footingWorldY)) {
                return false;
            }

            var navState = character.GetAiNavigationState();
            var maxSearchSteps = settings != null ? settings.PathSearchMaxSteps : 64;
            AdjacentCellCandidate? bestStep = null;
            var bestScore = float.MinValue;

            foreach (var neighbor in DiceBoardAnalyzer.GetAdjacentCells(blockedCell)) {
                if (!snapshot.IsInPlayerRegion(neighbor)) {
                    continue;
                }

                if (!AiCellMoveEvaluator.TrySelectBestAdjacentCell(
                    passability,
                    navState,
                    neighbor,
                    footingWorldY,
                    character.PlayerSlot,
                    standOnDie: null,
                    maxSearchSteps,
                    AiNavigationConstraints.None,
                    out var step,
                    out _)) {
                    continue;
                }

                // Prefer leaving onto a different cell; higher scores from evaluator win ties.
                if (step.Cell == blockedCell) {
                    continue;
                }

                var score = step.Score;
                if (preferredLeaveCell.HasValue && step.Cell == preferredLeaveCell.Value) {
                    score += 100f;
                } else if (preferredLeaveCell.HasValue) {
                    score -= DiceBoardAnalyzer.ManhattanDistance(step.Cell, preferredLeaveCell.Value);
                }

                if (score > bestScore) {
                    bestScore = score;
                    bestStep = step;
                }
            }

            if (!bestStep.HasValue) {
                return false;
            }

            var picked = bestStep.Value;
            var maxFrames = settings != null ? settings.MoveActionMaxFrames : 30;
            Application.AiDebugLog.Log(
                $"LeaveOccupiedStackCell from={blockedCell} step={picked.Cell} edge={picked.EdgeKind}" +
                (preferredLeaveCell.HasValue ? $" prefer={preferredLeaveCell.Value}" : string.Empty));
            action = new Application.Actions.MoveToAdjacentCellAction(
                picked.Cell,
                picked.Cell,
                maxFrames,
                Application.Actions.MoveActionPurpose.NavigateToCell,
                null,
                picked.EdgeKind);
            return true;
        }

        static Application.AiDiscreteAction BuildOrientAction(
            MatchGoal goal,
            AiSubGoal subGoal,
            GameStateSnapshot snapshot,
            CharacterController character,
            DiceRegistry registry,
            AiPlayerSettings settings) {
            var die = subGoal.TargetDie;
            if (die == null) {
                return null;
            }

            var state = die.CurrentState;
            if (state.Orientation.Top == subGoal.TargetFace) {
                subGoal.MarkComplete();
                return null;
            }

            if (!IsStandingOnTarget(snapshot, die)) {
                return BuildStandOnDieMovement(
                    die,
                    snapshot,
                    character,
                    registry,
                    settings,
                    AiNavigationConstraints.None);
            }

            if (!character.TryGetAiNavigationQuery(out var passability, out var footingWorldY)) {
                Application.AiDebugLog.Log("OrientPlan FAILED navigation-query-unavailable");
                return null;
            }

            // Join-equivalent: blocked next step clears the plan and re-searches.
            // Abandon only when no alternate orient path remains.
            const int maxOrientReplans = 4;
            for (var replanAttempt = 0; replanAttempt < maxOrientReplans; replanAttempt++) {
                state = die.CurrentState;
                if (state.Orientation.Top == subGoal.TargetFace) {
                    subGoal.MarkComplete();
                    return null;
                }

                subGoal.TryAdvanceOrientRollStep(state);

                if (!TryEnsureOrientRollPlan(
                    goal,
                    subGoal,
                    state,
                    passability,
                    character,
                    footingWorldY,
                    settings,
                    registry)) {
                    Application.AiDebugLog.Log(
                        $"OrientPlan FAILED die={die.name} cell={state.GridPos} " +
                        $"currentTop={state.Orientation.Top} targetTop={subGoal.TargetFace} " +
                        $"reason=no-alternate-path");
                    return null;
                }

                var plan = subGoal.OrientRollPlan.Value;
                var stepIndex = subGoal.OrientRollStepIndex;
                if (stepIndex >= plan.Directions.Count) {
                    if (state.Orientation.Top == subGoal.TargetFace) {
                        subGoal.MarkComplete();
                    }

                    return null;
                }

                var fromLevel = character.StandingPlacement.Level;
                var allowJump = settings != null && settings.AllowJump;
                if (!WorkDieOrientPlanner.TrySelectNextStep(
                    passability,
                    die,
                    fromLevel,
                    footingWorldY,
                    character.PlayerSlot,
                    plan,
                    stepIndex,
                    allowJump,
                    out var orientStep,
                    out var remainingRolls)) {
                    Application.AiDebugLog.Log(
                        $"OrientStepBlocked die={die.name} cell={state.GridPos} " +
                        $"currentTop={state.Orientation.Top} targetTop={subGoal.TargetFace} " +
                        $"step={stepIndex}/{plan.Directions.Count} replan={replanAttempt + 1}");
                    subGoal.ClearOrientRollPlan();
                    continue;
                }

                Application.AiDebugLog.Log(
                    $"OrientPlan die={die.name} currentTop={state.Orientation.Top} targetTop={subGoal.TargetFace} " +
                    $"stepIndex={stepIndex}/{plan.Directions.Count} planned={plan.Directions[stepIndex]} " +
                    $"exec={orientStep.Direction} mode={orientStep.Mode} landing={orientStep.LandingCell} " +
                    $"landingTier={orientStep.LandingTier} remainingRolls={remainingRolls}");

                return BuildWorkDieRollAction(orientStep, die, settings, allowJump);
            }

            Application.AiDebugLog.Log(
                $"OrientPlan FAILED die={die.name} cell={die.CurrentState.GridPos} " +
                $"currentTop={die.CurrentState.Orientation.Top} targetTop={subGoal.TargetFace} " +
                $"reason=replan-budget-exhausted");
            return null;
        }

        static bool TryEnsureOrientRollPlan(
            MatchGoal goal,
            AiSubGoal subGoal,
            DiceState state,
            MovementTransitionEvaluator passability,
            CharacterController character,
            float footingWorldY,
            AiPlayerSettings settings,
            DiceRegistry registry) {
            if (subGoal.HasOrientRollPlan) {
                var existing = subGoal.OrientRollPlan.Value;
                if (WorkDieSlidePlanner.IsPlanStillValid(existing, subGoal.OrientRollStepIndex, state)) {
                    return existing.Directions != null && existing.Directions.Count > 0;
                }

                subGoal.ClearOrientRollPlan();
            }

            var allowJump = settings != null && settings.AllowJump;
            var preferredAvoidance = ResolveChainCrushAvoidance(goal, registry);
            if (TryBuildOrientPlanWithAvoidance(
                    passability,
                    subGoal,
                    character,
                    footingWorldY,
                    state,
                    allowJump,
                    preferredAvoidance,
                    registry,
                    out var plan)
                || (preferredAvoidance.IsActive
                    && TryBuildOrientPlanWithAvoidance(
                        passability,
                        subGoal,
                        character,
                        footingWorldY,
                        state,
                        allowJump,
                        WorkDieRollCrushAvoidance.None,
                        registry,
                        out plan))) {
                subGoal.SetOrientRollPlan(plan);
                return true;
            }

            return false;
        }

        static bool TryBuildOrientPlanWithAvoidance(
            MovementTransitionEvaluator passability,
            AiSubGoal subGoal,
            CharacterController character,
            float footingWorldY,
            DiceState state,
            bool allowJump,
            WorkDieRollCrushAvoidance crushAvoidance,
            DiceRegistry registry,
            out WorkDieSlidePlan plan) {
            return WorkDieOrientPlanner.TryBuildOrientPlan(
                passability,
                subGoal.TargetDie,
                character.StandingPlacement.Level,
                footingWorldY,
                character.PlayerSlot,
                state,
                subGoal.TargetFace,
                allowJump,
                out plan,
                crushAvoidance,
                registry)
                && plan.Directions != null
                && plan.Directions.Count > 0;
        }

        static WorkDieRollCrushAvoidance ResolveChainCrushAvoidance(MatchGoal goal, DiceRegistry registry) {
            if (goal == null || !goal.IsChainGoal || registry == null || goal.Face < 2) {
                return WorkDieRollCrushAvoidance.None;
            }

            return new WorkDieRollCrushAvoidance(goal.Face, registry);
        }

        static Application.AiDiscreteAction BuildJoinClusterAction(
            MatchGoal goal,
            AiSubGoal subGoal,
            GameStateSnapshot snapshot,
            CharacterController character,
            DiceRegistry registry,
            AiPlayerSettings settings) {
            var workDie = subGoal.TargetDie;
            if (workDie == null) {
                return null;
            }

            var workState = workDie.CurrentState;
            // Face-one clear must perform at least one roll while adjacent; do not complete early.
            if (!goal.IsFaceOneClearGoal
                && WorkDieSlidePlanner.IsJoinComplete(
                    workState,
                    goal.ClusterDice,
                    subGoal.TargetFace)) {
                subGoal.MarkComplete();
                return null;
            }

            if (!character.TryGetAiNavigationQuery(out var passability, out var footingWorldY)) {
                Application.AiDebugLog.Log("JoinPlan FAILED navigation-query-unavailable");
                return null;
            }

            // One planning tick: at most one search slice. Step block defers replan to next tick.
            workState = workDie.CurrentState;
            subGoal.TryAdvanceJoinRollStep();

            if (!goal.IsFaceOneClearGoal
                && WorkDieSlidePlanner.IsJoinComplete(
                    workState,
                    goal.ClusterDice,
                    subGoal.TargetFace)) {
                subGoal.MarkComplete();
                return null;
            }

            if (!TryEnsureJoinRollPlan(
                goal,
                subGoal,
                snapshot,
                workState,
                passability,
                character,
                footingWorldY,
                settings,
                registry)) {
                if (subGoal.HasPendingJoinSearch) {
                    var pending = subGoal.JoinSearchSession;
                    Application.AiDebugLog.Log(
                        $"JoinSearch InProgress die={workDie.name} cell={workState.GridPos} " +
                        $"cap={(pending != null ? pending.ParticipantCap : 0)} " +
                        $"live={(pending != null && pending.Participants != null ? pending.Participants.Length : 0)}");
                } else {
                    Application.AiDebugLog.Log(
                        $"JoinPlan FAILED die={workDie.name} cell={workState.GridPos} " +
                        $"top={workState.Orientation.Top} reason=no-join-path");
                }

                return null;
            }

            var plan = subGoal.JoinRollPlan.Value;
            var stepIndex = subGoal.JoinRollStepIndex;
            if (plan.Steps == null || stepIndex >= plan.Steps.Count) {
                workState = workDie.CurrentState;
                if (goal.IsFaceOneClearGoal) {
                    if (FaceOneClearEvaluator.IsClearComplete(workState, goal.ClusterDice)) {
                        subGoal.MarkComplete();
                    }
                } else if (WorkDieSlidePlanner.IsJoinComplete(
                    workState,
                    goal.ClusterDice,
                    subGoal.TargetFace)) {
                    subGoal.MarkComplete();
                }

                return null;
            }

            var step = plan.Steps[stepIndex];
            var activeDie = step.Die;
            if (activeDie == null) {
                Application.AiDebugLog.Log(
                    $"JoinStepInvalid die=null step={stepIndex}/{plan.Steps.Count} — defer replan");
                subGoal.ClearJoinRollPlan();
                return null;
            }

            if (!IsStandingOnTarget(snapshot, activeDie)) {
                if (snapshot.PlayerCell == activeDie.CurrentState.GridPos
                    && snapshot.StandingDice != null
                    && snapshot.StandingDice != activeDie) {
                    Application.AiDebugLog.Log(
                        $"JoinReachSameCellWrongTier player={snapshot.PlayerCell} " +
                        $"standOn={snapshot.StandingDice.name} target={activeDie.name}");
                    if (TryBuildLeaveCellAction(
                        snapshot,
                        character,
                        settings,
                        activeDie.CurrentState.GridPos,
                        out var leaveAction)) {
                        return leaveAction;
                    }

                    return null;
                }

                Application.AiDebugLog.Log(
                    $"JoinRemount from={(snapshot.StandingDice != null ? snapshot.StandingDice.name : "floor")} " +
                    $"to={activeDie.name} step={stepIndex}/{plan.Steps.Count}");
                return BuildStandOnDieMovement(
                    activeDie,
                    snapshot,
                    character,
                    registry,
                    settings,
                    AiNavigationConstraints.None);
            }

            var fromLevel = character.StandingPlacement.Level;
            var allowJump = settings != null && settings.AllowJump;
            if (!WorkDieRollPlanner.TrySelectRollStep(
                passability,
                activeDie,
                fromLevel,
                footingWorldY,
                character.PlayerSlot,
                step.Direction,
                allowJump,
                out var slideStep)) {
                Application.AiDebugLog.Log(
                    $"JoinStepBlocked die={activeDie.name} cell={activeDie.CurrentState.GridPos} " +
                    $"step={stepIndex}/{plan.Steps.Count} — defer replan");
                subGoal.ClearJoinRollPlan();
                return null;
            }

            Application.AiDebugLog.Log(
                $"JoinPlan work={workDie.name} active={activeDie.name} " +
                $"top={activeDie.CurrentState.Orientation.Top} " +
                $"goalCell={subGoal.TargetCell}/{subGoal.TargetTier} " +
                $"stepIndex={stepIndex}/{plan.Steps.Count} planned={step.Direction} " +
                $"exec={slideStep.Direction} mode={slideStep.Mode} landing={slideStep.LandingCell} " +
                $"landingTier={slideStep.LandingTier}");

            return BuildWorkDieRollAction(slideStep, activeDie, settings, allowJump);
        }

        static bool TryEnsureJoinRollPlan(
            MatchGoal goal,
            AiSubGoal subGoal,
            GameStateSnapshot snapshot,
            DiceState workState,
            MovementTransitionEvaluator passability,
            CharacterController character,
            float footingWorldY,
            AiPlayerSettings settings,
            DiceRegistry registry) {
            if (subGoal.HasJoinRollPlan) {
                var existing = subGoal.JoinRollPlan.Value;
                if (MultiDieRollPlanUtil.IsPlanStillValid(existing, subGoal.JoinRollStepIndex)) {
                    return existing.Steps != null;
                }

                subGoal.ClearJoinRollPlan();
            }

            // Face-one clear always needs a non-empty action path (OneVanish trigger).
            if (!goal.IsFaceOneClearGoal
                && WorkDieSlidePlanner.IsJoinComplete(
                    workState,
                    goal.ClusterDice,
                    subGoal.TargetFace)) {
                subGoal.SetJoinRollPlan(MultiDieRollPlan.Empty(subGoal.TargetDie, workState));
                return true;
            }

            var allowJump = settings != null && settings.AllowJump;
            var preferredAvoidance = ResolveChainCrushAvoidance(goal, registry);
            var crushAvoidance = preferredAvoidance;
            if (subGoal.JoinSearchAwaitingAvoidanceFallback
                || (subGoal.JoinSearchSession != null
                    && subGoal.JoinSearchSession.IsActive
                    && !subGoal.JoinSearchSession.CrushAvoidance.IsActive)) {
                crushAvoidance = WorkDieRollCrushAvoidance.None;
                subGoal.ClearJoinSearchAwaitingAvoidanceFallback();
            }

            if (!TryBuildJoinRollPlanWithAvoidance(
                passability,
                goal,
                subGoal,
                snapshot,
                character,
                footingWorldY,
                workState,
                allowJump,
                crushAvoidance,
                registry,
                settings,
                out var plan,
                out var searchStatus)) {
                if (searchStatus == MultiDieJoinSearchStatus.Exhausted
                    && preferredAvoidance.IsActive
                    && crushAvoidance.IsActive) {
                    // Defer no-avoidance fallback to the next planning tick (same frame budget).
                    subGoal.MarkJoinSearchAwaitingAvoidanceFallback();
                    Application.AiDebugLog.Log(
                        $"JoinSearch Exhausted with crush-avoidance — defer fallback die={subGoal.TargetDie.name}");
                }

                return false;
            }

            subGoal.SetJoinRollPlan(plan);
            return true;
        }

        static bool TryBuildJoinRollPlanWithAvoidance(
            MovementTransitionEvaluator passability,
            MatchGoal goal,
            AiSubGoal subGoal,
            GameStateSnapshot snapshot,
            CharacterController character,
            float footingWorldY,
            DiceState workState,
            bool allowJump,
            WorkDieRollCrushAvoidance crushAvoidance,
            DiceRegistry registry,
            AiPlayerSettings settings,
            out MultiDieRollPlan plan,
            out MultiDieJoinSearchStatus searchStatus) {
            plan = default;
            searchStatus = MultiDieJoinSearchStatus.Invalid;
            var workDie = subGoal.TargetDie;
            if (workDie == null || goal == null) {
                return false;
            }

            if (goal.IsFaceOneClearGoal) {
                if (!WorkDieRollPathPlanner.TryFindFaceOneClearPath(
                    passability,
                    workDie,
                    character.StandingPlacement.Level,
                    footingWorldY,
                    character.PlayerSlot,
                    workState,
                    goal.ClusterDice,
                    allowJump,
                    out var directions,
                    out var endState,
                    crushAvoidance,
                    registry)
                    || directions == null
                    || directions.Count == 0) {
                    searchStatus = MultiDieJoinSearchStatus.Exhausted;
                    return false;
                }

                if (!MultiDieConfigSpacePlanner.TryBuildSingleDiePlan(
                    passability,
                    workDie,
                    character.StandingPlacement.Level,
                    footingWorldY,
                    character.PlayerSlot,
                    workState,
                    directions,
                    allowJump,
                    out plan,
                    crushAvoidance)) {
                    searchStatus = MultiDieJoinSearchStatus.Exhausted;
                    return false;
                }

                subGoal.ClearJoinSearchSession();
                subGoal.RetargetJoin(endState.GridPos, endState.Tier);
                searchStatus = MultiDieJoinSearchStatus.Found;
                return true;
            }

            var session = subGoal.JoinSearchSession;
            var nodesPerFrame = settings != null ? settings.JoinSearchNodesPerFrame : 256;
            var maxNodesTotal = settings != null ? settings.JoinSearchMaxNodesTotal : 20000;
            var maxDepth = settings != null ? settings.JoinSearchMaxDepth : 24;
            var initialParticipants = settings != null ? settings.JoinSearchInitialParticipants : 1;
            var maxParticipants = settings != null ? settings.JoinSearchMaxParticipants : 4;
            var stageBefore = session != null && session.IsActive
                ? session.ParticipantCap
                : initialParticipants;
            searchStatus = MultiDieConfigSpacePlanner.TryFindJoinPathBudgeted(
                passability,
                workDie,
                footingWorldY,
                character.PlayerSlot,
                workState,
                subGoal.TargetFace,
                goal.ClusterDice,
                snapshot != null ? snapshot.PlanningDice : null,
                allowJump,
                registry,
                crushAvoidance,
                nodesPerFrame,
                maxNodesTotal,
                maxDepth,
                initialParticipants,
                maxParticipants,
                ref session,
                out plan);
            subGoal.SetJoinSearchSession(session);
            if (session != null
                && session.IsActive
                && session.ParticipantCap > stageBefore) {
                Application.AiDebugLog.Log(
                    $"JoinSearch Escalate participants {stageBefore}->{session.ParticipantCap} " +
                    $"(live={session.Participants.Length}) die={workDie.name}");
            }

            if (searchStatus == MultiDieJoinSearchStatus.Found) {
                if (plan.Steps == null) {
                    searchStatus = MultiDieJoinSearchStatus.Invalid;
                    return false;
                }

                if (plan.Steps.Count == 0) {
                    return WorkDieSlidePlanner.IsJoinComplete(
                        workState,
                        goal.ClusterDice,
                        subGoal.TargetFace);
                }

                if (MultiDieRollPlanUtil.TryResolveWorkDieEndState(plan, out var workEnd)) {
                    // RetargetJoin clears plans/sessions; stash end cell then restore plan via caller.
                    var endCell = workEnd.GridPos;
                    var endTier = workEnd.Tier;
                    subGoal.RetargetJoin(endCell, endTier);
                    // Caller SetJoinRollPlan after this returns; Retarget cleared session already.
                }

                return true;
            }

            if (searchStatus == MultiDieJoinSearchStatus.InProgress) {
                return false;
            }

            return false;
        }

        static Application.AiDiscreteAction BuildWorkDieRollAction(
            WorkDieRollStep rollStep,
            DiceController die,
            AiPlayerSettings settings,
            bool allowJump) {
            var maxFrames = settings != null ? settings.MoveActionMaxFrames : 30;
            if (rollStep.Mode == WorkDieRollExecutionMode.GroundRoll) {
                return new Application.Actions.MoveToAdjacentCellAction(
                    rollStep.LandingCell,
                    rollStep.LandingCell,
                    maxFrames,
                    Application.Actions.MoveActionPurpose.RollWorkDie,
                    die,
                    rollStep.EdgeKind);
            }

            if (!allowJump) {
                return null;
            }

            return new Application.Actions.JumpThenMoveAction(
                rollStep.Direction,
                rollStep.LandingCell,
                settings != null ? settings.JumpMoveMaxFrames : 48,
                die,
                releaseInputDuringRoll: false,
                expectedLandingTier: rollStep.LandingTier);
        }

        static Application.AiDiscreteAction BuildLiftAction(
            MatchGoal goal,
            AiSubGoal subGoal,
            GameStateSnapshot snapshot,
            CharacterController character,
            DiceRegistry registry,
            AiPlayerSettings settings) {
            var die = subGoal.TargetDie;
            if (die == null) {
                return null;
            }

            if (!TryGetFollowingPlaceCell(goal, subGoal, out var placeCell)) {
                Application.AiDebugLog.Log(
                    $"Lift FAILED die={(die != null ? die.name : "none")} reason=no-place-cell");
                return null;
            }

            if (!LiftJoinPlanner.TrySelectStanceCell(
                snapshot,
                registry,
                die,
                die.CurrentState.GridPos,
                placeCell,
                out var stanceCell)) {
                Application.AiDebugLog.Log(
                    $"Lift FAILED die={die.name} place={placeCell} reason=no-stance");
                return null;
            }

            // Must leave the work die onto (or toward) the planned stance before lifting.
            if (snapshot.StandingDice == die) {
                if (TryBuildLeaveCellAction(
                    snapshot,
                    character,
                    settings,
                    snapshot.PlayerCell,
                    out var leaveAction,
                    preferredLeaveCell: stanceCell)) {
                    Application.AiDebugLog.Log(
                        $"LiftLeaveStandingDie die={die.name} from={snapshot.PlayerCell} stance={stanceCell} place={placeCell}");
                    return leaveAction;
                }

                Application.AiDebugLog.Log(
                    $"LiftLeaveStandingDie FAILED die={die.name} from={snapshot.PlayerCell} stance={stanceCell}");
                return null;
            }

            if (snapshot.PlayerCell != stanceCell
                || DiceBoardAnalyzer.ManhattanDistance(snapshot.PlayerCell, placeCell) != 1) {
                Application.AiDebugLog.Log(
                    $"LiftApproachStance die={die.name} stance={stanceCell} place={placeCell} player={snapshot.PlayerCell}");
                return SelectMovementAction(
                    stanceCell,
                    snapshot,
                    character,
                    registry,
                    settings,
                    preferJump: false,
                    standOnDie: null,
                    AiNavigationConstraints.None);
            }

            if (!IsAdjacentToDice(snapshot, die)) {
                Application.AiDebugLog.Log(
                    $"LiftStanceMismatch die={die.name} player={snapshot.PlayerCell} stance={stanceCell}");
                return null;
            }

            var faceDirection = GetFacingDirectionTowardDie(snapshot, die);
            if (!faceDirection.HasValue || !character.CanLiftTarget(die)) {
                Application.AiDebugLog.Log(
                    $"Lift FAILED die={die.name} player={snapshot.PlayerCell} " +
                    $"canLift={character.CanLiftTarget(die)}");
                return null;
            }

            return new Application.Actions.PulseLiftAction(faceDirection.Value);
        }

        static bool TryGetFollowingPlaceCell(MatchGoal goal, AiSubGoal liftSubGoal, out Vector2Int placeCell) {
            placeCell = default;
            if (goal?.SubGoals == null || liftSubGoal == null) {
                return false;
            }

            var foundLift = false;
            for (var i = 0; i < goal.SubGoals.Count; i++) {
                var candidate = goal.SubGoals[i];
                if (!foundLift) {
                    if (candidate == liftSubGoal) {
                        foundLift = true;
                    }

                    continue;
                }

                if (candidate.IsComplete) {
                    continue;
                }

                if (candidate.Kind == AiSubGoalKind.PlaceCarriedDie) {
                    placeCell = candidate.TargetCell;
                    return true;
                }

                break;
            }

            return false;
        }

        public static Application.AiDiscreteAction BuildNavigateToTarget(
            Vector2Int targetCell,
            GameStateSnapshot snapshot,
            CharacterController character,
            AiPlayerSettings settings,
            DiceController standOnDie = null,
            bool preferJump = true,
            DiceRegistry registry = null) {
            return SelectMovementAction(
                targetCell,
                snapshot,
                character,
                registry,
                settings,
                preferJump,
                standOnDie,
                AiNavigationConstraints.None);
        }

        public static Application.AiDiscreteAction BuildStandOnDie(
            DiceController targetDie,
            GameStateSnapshot snapshot,
            CharacterController character,
            DiceRegistry registry,
            AiPlayerSettings settings) {
            return BuildStandOnDieMovement(
                targetDie,
                snapshot,
                character,
                registry,
                settings,
                AiNavigationConstraints.None);
        }

        static Application.AiDiscreteAction SelectMovementAction(
            Vector2Int targetCell,
            GameStateSnapshot snapshot,
            CharacterController character,
            DiceRegistry registry,
            AiPlayerSettings settings,
            bool preferJump,
            DiceController standOnDie,
            AiNavigationConstraints constraints) {
            var purpose = standOnDie != null
                ? Application.Actions.MoveActionPurpose.StandOnDie
                : Application.Actions.MoveActionPurpose.NavigateToCell;

            if (standOnDie == null && snapshot.PlayerCell == targetCell) {
                return null;
            }

            if (!character.TryGetAiNavigationQuery(out var passability, out var footingWorldY)) {
                Application.AiDebugLog.Log("SelectCellStep FAILED navigation-query-unavailable");
                return null;
            }

            var maxSearchSteps = settings != null ? settings.PathSearchMaxSteps : 64;
            var navState = character.GetAiNavigationState();

            if (AiCellMoveEvaluator.TrySelectBestAdjacentCell(
                passability,
                navState,
                targetCell,
                footingWorldY,
                character.PlayerSlot,
                standOnDie,
                maxSearchSteps,
                constraints,
                out var best,
                out var candidateLog)) {
                var maxFrames = character.IsJumping
                    ? (settings != null ? settings.JumpMoveMaxFrames : 45)
                    : (settings != null ? settings.MoveActionMaxFrames : 30);

                Application.AiDebugLog.Log(
                    $"SelectCellStep purpose={purpose} player={snapshot.PlayerCell} goal={targetCell} " +
                    $"standOn={(standOnDie != null ? standOnDie.name : "none")} " +
                    $"picked={best.Cell} dir={best.Direction} edge={best.EdgeKind} pathLen={best.PathLength} score={best.Score:F1} " +
                    $"detail={candidateLog}");

                return new Application.Actions.MoveToAdjacentCellAction(
                    best.Cell,
                    targetCell,
                    maxFrames,
                    purpose,
                    standOnDie,
                    best.EdgeKind);
            }

            if (preferJump
                && !character.IsJumping
                && settings != null
                && settings.AllowJump
                && !IsUnsafeClimbJump(snapshot, standOnDie)) {
                var jumpDirection = DiceBoardAnalyzer.GetPrimaryDirectionToward(snapshot.PlayerCell, targetCell);
                if (jumpDirection.HasValue) {
                    Application.AiDebugLog.Log(
                        $"SelectCellStep purpose={purpose} player={snapshot.PlayerCell} goal={targetCell} " +
                        $"picked=Jump direction={jumpDirection.Value} detail={candidateLog}");

                    return new Application.Actions.JumpThenMoveAction(
                        jumpDirection.Value,
                        targetCell,
                        settings.JumpMoveMaxFrames,
                        standOnDie,
                        releaseInputDuringRoll: false,
                        expectedLandingTier: standOnDie != null
                            ? standOnDie.CurrentState.Tier
                            : (DiceStackTier?)null);
                }
            }

            if (registry != null
                && LiftClearancePlanner.TryPlan(snapshot, registry, targetCell, out var clearance)) {
                Application.AiDebugLog.Log(
                    $"LiftClearance die={clearance.BlockDie.name} place={clearance.PlaceCell} " +
                    $"dir={clearance.FaceDirection} goal={targetCell}");
                return new Application.Actions.LiftSequenceAction(
                    clearance.FaceDirection,
                    clearance.PlaceCell);
            }

            Application.AiDebugLog.Log(
                $"SelectCellStep FAILED purpose={purpose} player={snapshot.PlayerCell} goal={targetCell} " +
                $"detail={candidateLog}");

            return null;
        }

        /// <summary>
        /// Jumping from another die onto a higher-tier target does not remount; leave first instead.
        /// </summary>
        static bool IsUnsafeClimbJump(GameStateSnapshot snapshot, DiceController standOnDie) {
            if (snapshot?.StandingDice == null || standOnDie == null) {
                return false;
            }

            if (snapshot.StandingDice == standOnDie) {
                return false;
            }

            return RequiresJumpToStandOn(snapshot, standOnDie);
        }

        // Legacy scoring removed — cell steps use AiCellMoveEvaluator.

        static bool IsStandingOnTarget(GameStateSnapshot snapshot, DiceController die) {
            return die != null && snapshot.StandingDice == die;
        }

        static bool IsAdjacentToDice(GameStateSnapshot snapshot, DiceController die) {
            if (die == null) {
                return false;
            }

            return IsAdjacentToCell(snapshot, die.CurrentState.GridPos);
        }

        static bool IsAdjacentToCell(GameStateSnapshot snapshot, Vector2Int cell) {
            return DiceBoardAnalyzer.ManhattanDistance(snapshot.PlayerCell, cell) == 1;
        }

        public static Direction? GetFacingDirectionTowardDie(GameStateSnapshot snapshot, DiceController die) {
            var delta = die.CurrentState.GridPos - snapshot.PlayerCell;
            if (delta.x == 1 && delta.y == 0) {
                return Direction.East;
            }

            if (delta.x == -1 && delta.y == 0) {
                return Direction.West;
            }

            if (delta.x == 0 && delta.y == 1) {
                return Direction.North;
            }

            if (delta.x == 0 && delta.y == -1) {
                return Direction.South;
            }

            return DiceBoardAnalyzer.GetPrimaryDirectionToward(snapshot.PlayerCell, die.CurrentState.GridPos);
        }
    }
}
