using System.Collections.Generic;
using System.Text;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Grid;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    /// <summary>
    /// Multi-die configuration-space A* for Join.
    /// Actions: roll any participant; optionally Lift then Place (flat expansion).
    /// </summary>
    public static class MultiDieConfigSpacePlanner
    {
        static readonly Direction[] Directions = {
            Direction.East, Direction.West, Direction.North, Direction.South
        };

        const int DefaultMaxParticipants = 4;
        const int DefaultMaxSearchDepth = 24;
        const int DefaultMaxNodesTotal = 20000;
        const int DefaultNodesPerFrame = 256;
        const int DefaultLiftCost = 2;
        const int DefaultPlaceCost = 2;

        public sealed class SearchNode
        {
            public DiceState[] States { get; }
            public List<MultiDieJoinStep> Path { get; }
            public List<DiceState[]> Checkpoints { get; }
            public List<int> CarriedCheckpoints { get; }
            public int CarriedIndex { get; }
            public Vector2Int LiftFromCell { get; }
            public int G { get; }
            public int F { get; }

            public SearchNode(
                DiceState[] states,
                List<MultiDieJoinStep> path,
                List<DiceState[]> checkpoints,
                List<int> carriedCheckpoints,
                int carriedIndex,
                Vector2Int liftFromCell,
                int g,
                int f) {
                States = states;
                Path = path;
                Checkpoints = checkpoints;
                CarriedCheckpoints = carriedCheckpoints;
                CarriedIndex = carriedIndex;
                LiftFromCell = liftFromCell;
                G = g;
                F = f;
            }
        }

        public sealed class JoinSearchOpenHeap
        {
            readonly List<SearchNode> items = new List<SearchNode>();

            public int Count => items.Count;

            public void Clear() {
                items.Clear();
            }

            public void Push(SearchNode node) {
                items.Add(node);
                SiftUp(items.Count - 1);
            }

            public SearchNode Pop() {
                var root = items[0];
                var last = items[items.Count - 1];
                items.RemoveAt(items.Count - 1);
                if (items.Count > 0) {
                    items[0] = last;
                    SiftDown(0);
                }

                return root;
            }

            static int Compare(SearchNode a, SearchNode b) {
                var f = a.F.CompareTo(b.F);
                return f != 0 ? f : a.G.CompareTo(b.G);
            }

            void SiftUp(int index) {
                while (index > 0) {
                    var parent = (index - 1) / 2;
                    if (Compare(items[index], items[parent]) >= 0) {
                        break;
                    }

                    Swap(index, parent);
                    index = parent;
                }
            }

            void SiftDown(int index) {
                while (true) {
                    var left = index * 2 + 1;
                    var right = left + 1;
                    var best = index;
                    if (left < items.Count && Compare(items[left], items[best]) < 0) {
                        best = left;
                    }

                    if (right < items.Count && Compare(items[right], items[best]) < 0) {
                        best = right;
                    }

                    if (best == index) {
                        break;
                    }

                    Swap(index, best);
                    index = best;
                }
            }

            void Swap(int a, int b) {
                var tmp = items[a];
                items[a] = items[b];
                items[b] = tmp;
            }
        }

        public static bool TryBuildSingleDiePlan(
            MovementTransitionEvaluator passability,
            DiceController workDie,
            int fromLevel,
            float footingWorldY,
            PlayerSlot movementOwner,
            DiceState startState,
            IReadOnlyList<Direction> directions,
            bool allowJump,
            out MultiDieRollPlan plan,
            WorkDieRollCrushAvoidance crushAvoidance = default) {
            plan = default;
            if (passability == null || workDie == null || directions == null) {
                return false;
            }

            var participants = new[] { workDie };
            var steps = new List<MultiDieJoinStep>(directions.Count);
            var checkpoints = new List<DiceState[]>(directions.Count + 1) { new[] { startState } };
            var carried = new List<int>(directions.Count + 1) { -1 };

            var state = startState;
            var level = fromLevel;
            for (var i = 0; i < directions.Count; i++) {
                if (!WorkDieRollPathPlanner.TrySimulateRollStep(
                    passability,
                    workDie,
                    state,
                    level,
                    footingWorldY,
                    movementOwner,
                    directions[i],
                    allowJump,
                    out state,
                    out level,
                    crushAvoidance)) {
                    return false;
                }

                steps.Add(MultiDieJoinStep.Roll(workDie, directions[i]));
                checkpoints.Add(new[] { state });
                carried.Add(-1);
            }

            plan = new MultiDieRollPlan(participants, steps, checkpoints, carried, workDieIndex: 0);
            return true;
        }

        public static MultiDieJoinSearchStatus TryFindJoinPathBudgeted(
            MovementTransitionEvaluator passability,
            DiceController workDie,
            float footingWorldY,
            PlayerSlot movementOwner,
            DiceState workStartState,
            int targetFace,
            IReadOnlyList<DiceSnapshot> cluster,
            IReadOnlyList<DiceSnapshot> planningDice,
            bool allowJump,
            DiceRegistry registry,
            WorkDieRollCrushAvoidance crushAvoidance,
            int nodesPerFrame,
            int maxNodesTotal,
            int maxDepth,
            int initialParticipants,
            int maxParticipants,
            bool enableLift,
            bool liftAfterRollExhausted,
            int liftActionCost,
            int placeActionCost,
            VersusArenaLayout versusLayout,
            ref MultiDieJoinSearchSession session,
            out MultiDieRollPlan plan) {
            plan = default;
            if (passability == null
                || workDie == null
                || registry == null
                || targetFace < 1
                || cluster == null) {
                session?.Deactivate();
                session = null;
                return MultiDieJoinSearchStatus.Invalid;
            }

            if (nodesPerFrame <= 0) {
                nodesPerFrame = DefaultNodesPerFrame;
            }

            if (maxNodesTotal <= 0) {
                maxNodesTotal = DefaultMaxNodesTotal;
            }

            if (maxDepth <= 0) {
                maxDepth = DefaultMaxSearchDepth;
            }

            if (initialParticipants <= 0) {
                initialParticipants = 1;
            }

            if (maxParticipants <= 0) {
                maxParticipants = DefaultMaxParticipants;
            }

            if (initialParticipants > maxParticipants) {
                initialParticipants = maxParticipants;
            }

            if (liftActionCost <= 0) {
                liftActionCost = DefaultLiftCost;
            }

            if (placeActionCost <= 0) {
                placeActionCost = DefaultPlaceCost;
            }

            var startAllowLift = enableLift && !liftAfterRollExhausted;

            if (session != null && session.IsActive) {
                if (!IsSessionCompatible(
                    session,
                    workDie,
                    workStartState,
                    targetFace,
                    crushAvoidance)) {
                    session.Deactivate();
                    session = null;
                }
            }

            if (session == null || !session.IsActive) {
                if (!TryStartSession(
                    workDie,
                    workStartState,
                    cluster,
                    planningDice,
                    crushAvoidance,
                    targetFace,
                    participantCap: initialParticipants,
                    maxParticipants,
                    maxDepth,
                    maxNodesTotal,
                    startAllowLift,
                    liftActionCost,
                    placeActionCost,
                    out session,
                    out plan,
                    out var startStatus)) {
                    return startStatus;
                }

                if (startStatus == MultiDieJoinSearchStatus.Found) {
                    return MultiDieJoinSearchStatus.Found;
                }
            }

            var remainingBudget = nodesPerFrame;
            while (true) {
                var status = ExpandSession(
                    session,
                    passability,
                    footingWorldY,
                    movementOwner,
                    allowJump,
                    registry,
                    versusLayout,
                    remainingBudget,
                    out plan,
                    out var expanded);

                remainingBudget -= expanded;

                if (status == MultiDieJoinSearchStatus.Found) {
                    session.Deactivate();
                    session = null;
                    return MultiDieJoinSearchStatus.Found;
                }

                if (status == MultiDieJoinSearchStatus.InProgress) {
                    return MultiDieJoinSearchStatus.InProgress;
                }

                if (TryEscalateParticipantCap(
                    session,
                    workDie,
                    workStartState,
                    cluster,
                    planningDice,
                    maxDepth,
                    maxNodesTotal,
                    out var escalatedParticipants)) {
                    session.Deactivate();
                    session = escalatedParticipants;
                    if (remainingBudget <= 0) {
                        return MultiDieJoinSearchStatus.InProgress;
                    }

                    continue;
                }

                if (enableLift
                    && liftAfterRollExhausted
                    && !session.AllowLift
                    && TryEscalateAllowLift(
                        session,
                        workDie,
                        workStartState,
                        cluster,
                        planningDice,
                        maxDepth,
                        maxNodesTotal,
                        out var escalatedLift)) {
                    session.Deactivate();
                    session = escalatedLift;
                    if (remainingBudget <= 0) {
                        return MultiDieJoinSearchStatus.InProgress;
                    }

                    continue;
                }

                session.Deactivate();
                session = null;
                return MultiDieJoinSearchStatus.Exhausted;
            }
        }

        static bool TryStartSession(
            DiceController workDie,
            DiceState workStartState,
            IReadOnlyList<DiceSnapshot> cluster,
            IReadOnlyList<DiceSnapshot> planningDice,
            WorkDieRollCrushAvoidance crushAvoidance,
            int targetFace,
            int participantCap,
            int maxParticipants,
            int maxDepth,
            int maxNodesTotal,
            bool allowLift,
            int liftActionCost,
            int placeActionCost,
            out MultiDieJoinSearchSession session,
            out MultiDieRollPlan plan,
            out MultiDieJoinSearchStatus status) {
            session = null;
            plan = default;
            status = MultiDieJoinSearchStatus.Invalid;

            if (!TrySelectParticipants(
                workDie,
                workStartState,
                cluster,
                planningDice,
                participantCap,
                out var participants,
                out var startStates,
                out var workDieIndex)) {
                return false;
            }

            if (IsJoinGoal(workDieIndex, participants, startStates, cluster, targetFace, carriedIndex: -1)) {
                plan = MultiDieRollPlan.Empty(workDie, workStartState);
                status = MultiDieJoinSearchStatus.Found;
                return true;
            }

            session = CreateSession(
                participants,
                participantCap,
                maxParticipants,
                workDieIndex,
                targetFace,
                cluster,
                crushAvoidance,
                startStates,
                maxDepth,
                maxNodesTotal,
                allowLift,
                liftActionCost,
                placeActionCost);
            status = MultiDieJoinSearchStatus.InProgress;
            return true;
        }

        static bool TryEscalateParticipantCap(
            MultiDieJoinSearchSession current,
            DiceController workDie,
            DiceState workStartState,
            IReadOnlyList<DiceSnapshot> cluster,
            IReadOnlyList<DiceSnapshot> planningDice,
            int maxDepth,
            int maxNodesTotal,
            out MultiDieJoinSearchSession escalated) {
            escalated = null;
            if (current == null) {
                return false;
            }

            var nextCap = current.ParticipantCap + 1;
            while (nextCap <= current.MaxParticipants) {
                if (!TrySelectParticipants(
                    workDie,
                    workStartState,
                    cluster,
                    planningDice,
                    nextCap,
                    out var participants,
                    out var startStates,
                    out var workDieIndex)) {
                    return false;
                }

                if (participants.Length > current.Participants.Length) {
                    escalated = CreateSession(
                        participants,
                        nextCap,
                        current.MaxParticipants,
                        workDieIndex,
                        current.TargetFace,
                        cluster,
                        current.CrushAvoidance,
                        startStates,
                        maxDepth,
                        maxNodesTotal,
                        current.AllowLift,
                        current.LiftActionCost,
                        current.PlaceActionCost);
                    return true;
                }

                nextCap++;
            }

            return false;
        }

        static bool TryEscalateAllowLift(
            MultiDieJoinSearchSession current,
            DiceController workDie,
            DiceState workStartState,
            IReadOnlyList<DiceSnapshot> cluster,
            IReadOnlyList<DiceSnapshot> planningDice,
            int maxDepth,
            int maxNodesTotal,
            out MultiDieJoinSearchSession escalated) {
            escalated = null;
            if (current == null || current.AllowLift) {
                return false;
            }

            if (!TrySelectParticipants(
                workDie,
                workStartState,
                cluster,
                planningDice,
                current.ParticipantCap,
                out var participants,
                out var startStates,
                out var workDieIndex)) {
                return false;
            }

            escalated = CreateSession(
                participants,
                current.ParticipantCap,
                current.MaxParticipants,
                workDieIndex,
                current.TargetFace,
                cluster,
                current.CrushAvoidance,
                startStates,
                maxDepth,
                maxNodesTotal,
                allowLift: true,
                current.LiftActionCost,
                current.PlaceActionCost);
            return true;
        }

        static MultiDieJoinSearchSession CreateSession(
            DiceController[] participants,
            int participantCap,
            int maxParticipants,
            int workDieIndex,
            int targetFace,
            IReadOnlyList<DiceSnapshot> cluster,
            WorkDieRollCrushAvoidance crushAvoidance,
            DiceState[] startStates,
            int maxDepth,
            int maxNodesTotal,
            bool allowLift,
            int liftActionCost,
            int placeActionCost) {
            var startKey = BuildConfigKey(startStates, carriedIndex: -1);
            var bestG = new Dictionary<string, int> { { startKey, 0 } };
            var open = new JoinSearchOpenHeap();
            var h = EstimateJoinCost(
                workDieIndex,
                participants,
                startStates,
                cluster,
                targetFace,
                carriedIndex: -1,
                allowLift,
                liftActionCost,
                placeActionCost);
            open.Push(new SearchNode(
                startStates,
                path: new List<MultiDieJoinStep>(),
                checkpoints: new List<DiceState[]> { CloneStates(startStates) },
                carriedCheckpoints: new List<int> { -1 },
                carriedIndex: -1,
                liftFromCell: default,
                g: 0,
                f: h));

            return new MultiDieJoinSearchSession(
                participants,
                participantCap,
                maxParticipants,
                workDieIndex,
                targetFace,
                cluster,
                crushAvoidance,
                usedAvoidanceFallback: !crushAvoidance.IsActive,
                allowLift,
                liftActionCost,
                placeActionCost,
                startStates,
                bestG,
                open,
                maxDepth,
                maxNodesTotal);
        }

        static bool IsSessionCompatible(
            MultiDieJoinSearchSession session,
            DiceController workDie,
            DiceState workStartState,
            int targetFace,
            WorkDieRollCrushAvoidance crushAvoidance) {
            if (session.Participants == null
                || session.Participants.Length == 0
                || session.WorkDieIndex < 0
                || session.WorkDieIndex >= session.Participants.Length
                || session.Participants[session.WorkDieIndex] != workDie
                || session.TargetFace != targetFace
                || session.CrushAvoidance.ChainFace != crushAvoidance.ChainFace
                || session.CrushAvoidance.IsActive != crushAvoidance.IsActive) {
                return false;
            }

            var startWork = session.StartStates[session.WorkDieIndex];
            if (startWork.GridPos != workStartState.GridPos
                || startWork.Tier != workStartState.Tier
                || startWork.Orientation.Top != workStartState.Orientation.Top
                || startWork.Orientation.North != workStartState.Orientation.North
                || startWork.Orientation.East != workStartState.Orientation.East) {
                return false;
            }

            return MultiDieRollPlanUtil.MatchesLive(session.Participants, session.StartStates, carriedIndex: -1);
        }

        static MultiDieJoinSearchStatus ExpandSession(
            MultiDieJoinSearchSession session,
            MovementTransitionEvaluator passability,
            float footingWorldY,
            PlayerSlot movementOwner,
            bool allowJump,
            DiceRegistry registry,
            VersusArenaLayout versusLayout,
            int nodesPerFrame,
            out MultiDieRollPlan plan,
            out int expandedThisCall) {
            plan = default;
            expandedThisCall = 0;
            if (nodesPerFrame <= 0) {
                return MultiDieJoinSearchStatus.InProgress;
            }

            var participants = session.Participants;

            while (session.Open.Count > 0) {
                if (expandedThisCall >= nodesPerFrame) {
                    return MultiDieJoinSearchStatus.InProgress;
                }

                if (session.ExpandedTotal >= session.MaxNodesTotal) {
                    return MultiDieJoinSearchStatus.Exhausted;
                }

                var current = session.Open.Pop();
                var currentKey = BuildConfigKey(current.States, current.CarriedIndex);
                if (session.BestG.TryGetValue(currentKey, out var bestG) && current.G > bestG) {
                    continue;
                }

                if (current.G > 0
                    && IsJoinGoal(
                        session.WorkDieIndex,
                        participants,
                        current.States,
                        session.Cluster,
                        session.TargetFace,
                        current.CarriedIndex)) {
                    plan = new MultiDieRollPlan(
                        participants,
                        current.Path,
                        current.Checkpoints,
                        current.CarriedCheckpoints,
                        session.WorkDieIndex);
                    return MultiDieJoinSearchStatus.Found;
                }

                if (current.G >= session.MaxDepth) {
                    continue;
                }

                expandedThisCall++;
                session.ExpandedTotal++;

                BuildLayoutLists(
                    participants,
                    current.States,
                    current.CarriedIndex,
                    out var presentDice,
                    out var presentStates,
                    out var absentDice);

                using (registry.BeginPlanningLayout(presentDice, presentStates, absentDice)) {
                    if (current.CarriedIndex < 0) {
                        ExpandRolls(
                            session,
                            current,
                            passability,
                            footingWorldY,
                            movementOwner,
                            allowJump,
                            registry);

                        if (session.AllowLift) {
                            ExpandLifts(session, current, registry);
                        }
                    } else if (session.AllowLift) {
                        ExpandPlaces(
                            session,
                            current,
                            registry,
                            versusLayout,
                            movementOwner);
                    }
                }
            }

            return MultiDieJoinSearchStatus.Exhausted;
        }

        static void ExpandRolls(
            MultiDieJoinSearchSession session,
            SearchNode current,
            MovementTransitionEvaluator passability,
            float footingWorldY,
            PlayerSlot movementOwner,
            bool allowJump,
            DiceRegistry registry) {
            var participants = session.Participants;
            for (var dieIndex = 0; dieIndex < participants.Length; dieIndex++) {
                if (dieIndex == current.CarriedIndex) {
                    continue;
                }

                var mover = participants[dieIndex];
                var fromState = current.States[dieIndex];
                var fromLevel = SurfaceHeightLevel.FromDiceStackTier(fromState.Tier);

                for (var d = 0; d < Directions.Length; d++) {
                    var direction = Directions[d];
                    DiceState landingState;
                    using (registry.BeginPlanningIgnore(mover)) {
                        if (!WorkDieRollPathPlanner.TrySimulateRollStep(
                            passability,
                            mover,
                            fromState,
                            fromLevel,
                            footingWorldY,
                            movementOwner,
                            direction,
                            allowJump,
                            out landingState,
                            out _,
                            session.CrushAvoidance)) {
                            continue;
                        }
                    }

                    var nextStates = CloneStates(current.States);
                    nextStates[dieIndex] = landingState;
                    TryEnqueue(
                        session,
                        current,
                        nextStates,
                        carriedIndex: -1,
                        liftFromCell: default,
                        MultiDieJoinStep.Roll(mover, direction),
                        edgeCost: 1);
                }
            }
        }

        static void ExpandLifts(
            MultiDieJoinSearchSession session,
            SearchNode current,
            DiceRegistry registry) {
            var participants = session.Participants;
            for (var dieIndex = 0; dieIndex < participants.Length; dieIndex++) {
                var die = participants[dieIndex];
                if (die == null || !CanLiftInSearch(die)) {
                    continue;
                }

                var from = current.States[dieIndex];
                if (from.Tier == DiceStackTier.Bottom && registry.HasTopAt(from.GridPos)) {
                    continue;
                }

                TryEnqueue(
                    session,
                    current,
                    CloneStates(current.States),
                    carriedIndex: dieIndex,
                    liftFromCell: from.GridPos,
                    MultiDieJoinStep.Lift(die),
                    edgeCost: session.LiftActionCost);
            }
        }

        static void ExpandPlaces(
            MultiDieJoinSearchSession session,
            SearchNode current,
            DiceRegistry registry,
            VersusArenaLayout versusLayout,
            PlayerSlot movementOwner) {
            var carriedIndex = current.CarriedIndex;
            if (carriedIndex < 0 || carriedIndex >= session.Participants.Length) {
                return;
            }

            var carried = session.Participants[carriedIndex];
            var carriedState = current.States[carriedIndex];
            CollectPlaceCells(
                session,
                current,
                versusLayout,
                movementOwner,
                out var cells);

            for (var i = 0; i < cells.Count; i++) {
                var cell = cells[i];
                if (!CarryPlacementPassability.TryResolveTarget(cell, registry, out var tier, out _)) {
                    continue;
                }

                if (!CarryPlacementPassability.CanPlaceAt(cell, tier, registry, out _)) {
                    continue;
                }

                var nextStates = CloneStates(current.States);
                nextStates[carriedIndex] = new DiceState(
                    cell,
                    carriedState.Orientation,
                    tier,
                    carriedState.Kind);

                TryEnqueue(
                    session,
                    current,
                    nextStates,
                    carriedIndex: -1,
                    liftFromCell: default,
                    MultiDieJoinStep.Place(carried, cell, tier),
                    edgeCost: session.PlaceActionCost);
            }
        }

        static void CollectPlaceCells(
            MultiDieJoinSearchSession session,
            SearchNode current,
            VersusArenaLayout versusLayout,
            PlayerSlot movementOwner,
            out List<Vector2Int> cells) {
            var set = new HashSet<Vector2Int>();
            void Add(Vector2Int cell) {
                if (!AiRegionFilter.IsInPlayerRegion(versusLayout, movementOwner, cell)) {
                    return;
                }

                set.Add(cell);
            }

            AddNeighbors(current.LiftFromCell, Add);

            for (var i = 0; i < session.Participants.Length; i++) {
                if (i == current.CarriedIndex) {
                    continue;
                }

                AddNeighbors(current.States[i].GridPos, Add);
            }

            if (session.Cluster != null) {
                for (var i = 0; i < session.Cluster.Count; i++) {
                    AddNeighbors(session.Cluster[i].GridPos, Add);
                }
            }

            cells = new List<Vector2Int>(set.Count);
            foreach (var cell in set) {
                cells.Add(cell);
            }
        }

        static void AddNeighbors(Vector2Int origin, System.Action<Vector2Int> add) {
            for (var d = 0; d < Directions.Length; d++) {
                add(origin + Directions[d].ToGridDelta());
            }
        }

        static void TryEnqueue(
            MultiDieJoinSearchSession session,
            SearchNode current,
            DiceState[] nextStates,
            int carriedIndex,
            Vector2Int liftFromCell,
            MultiDieJoinStep step,
            int edgeCost) {
            var nextG = current.G + edgeCost;
            if (nextG > session.MaxDepth) {
                return;
            }

            var key = BuildConfigKey(nextStates, carriedIndex);
            if (session.BestG.TryGetValue(key, out var knownG) && knownG <= nextG) {
                return;
            }

            session.BestG[key] = nextG;

            var nextPath = new List<MultiDieJoinStep>(current.Path.Count + 1);
            nextPath.AddRange(current.Path);
            nextPath.Add(step);

            var nextCheckpoints = new List<DiceState[]>(current.Checkpoints.Count + 1);
            nextCheckpoints.AddRange(current.Checkpoints);
            nextCheckpoints.Add(CloneStates(nextStates));

            var nextCarried = new List<int>(current.CarriedCheckpoints.Count + 1);
            nextCarried.AddRange(current.CarriedCheckpoints);
            nextCarried.Add(carriedIndex);

            var h = EstimateJoinCost(
                session.WorkDieIndex,
                session.Participants,
                nextStates,
                session.Cluster,
                session.TargetFace,
                carriedIndex,
                session.AllowLift,
                session.LiftActionCost,
                session.PlaceActionCost);

            session.Open.Push(
                new SearchNode(
                    nextStates,
                    nextPath,
                    nextCheckpoints,
                    nextCarried,
                    carriedIndex,
                    liftFromCell,
                    nextG,
                    nextG + h));
        }

        static void BuildLayoutLists(
            DiceController[] participants,
            DiceState[] states,
            int carriedIndex,
            out List<DiceController> presentDice,
            out List<DiceState> presentStates,
            out List<DiceController> absentDice) {
            presentDice = new List<DiceController>(participants.Length);
            presentStates = new List<DiceState>(participants.Length);
            absentDice = null;
            for (var i = 0; i < participants.Length; i++) {
                if (i == carriedIndex) {
                    absentDice ??= new List<DiceController>(1);
                    absentDice.Add(participants[i]);
                    continue;
                }

                presentDice.Add(participants[i]);
                presentStates.Add(states[i]);
            }
        }

        static bool CanLiftInSearch(DiceController die) {
            if (die == null || die.IsErasing || die.IsVanishing || die.IsCarried) {
                return false;
            }

            return die.Capabilities.CanBeLiftedByPlayer
                && !die.Capabilities.HasExpandedFootprint;
        }

        static int EstimateJoinCost(
            int workDieIndex,
            DiceController[] participants,
            IReadOnlyList<DiceState> states,
            IReadOnlyList<DiceSnapshot> cluster,
            int targetFace,
            int carriedIndex,
            bool allowLift,
            int liftActionCost,
            int placeActionCost) {
            if (IsJoinGoal(workDieIndex, participants, states, cluster, targetFace, carriedIndex)) {
                return 0;
            }

            if (carriedIndex == workDieIndex) {
                var work = states[workDieIndex];
                var orient = MinRollsToFace(work.Orientation, targetFace);
                // Still carried: at least one Place.
                return orient + placeActionCost;
            }

            if (carriedIndex >= 0) {
                // Carrying a non-work die: place it, then finish join.
                return placeActionCost
                    + EstimateJoinCost(
                        workDieIndex,
                        participants,
                        states,
                        cluster,
                        targetFace,
                        carriedIndex: -1,
                        allowLift,
                        liftActionCost,
                        placeActionCost);
            }

            var workState = states[workDieIndex];
            var orientCost = MinRollsToFace(workState.Orientation, targetFace);
            var approachCost = int.MaxValue;

            for (var i = 0; i < cluster.Count; i++) {
                var member = cluster[i].Controller;
                if (member == null || IndexOf(participants, member) >= 0) {
                    continue;
                }

                var snap = cluster[i];
                if (snap.Orientation.Top != targetFace) {
                    continue;
                }

                approachCost = Mathf.Min(
                    approachCost,
                    ApproachLowerBound(
                        new DiceSlot(workState.GridPos, workState.Tier),
                        new DiceSlot(snap.GridPos, snap.Tier)));
            }

            var rollLower = approachCost == int.MaxValue
                ? orientCost
                : Mathf.Max(orientCost, approachCost);

            if (!allowLift) {
                return rollLower;
            }

            // Lift+Place can finish join without rolling when face already matches.
            var liftLower = orientCost + liftActionCost + placeActionCost;
            return Mathf.Min(rollLower, liftLower);
        }

        static int MinRollsToFace(DiceOrientation orientation, int targetFace) {
            if (targetFace < 1 || targetFace > 6) {
                return 0;
            }

            if (orientation.Top == targetFace) {
                return 0;
            }

            if (orientation.Down == targetFace) {
                return 2;
            }

            return 1;
        }

        static int ApproachLowerBound(DiceSlot work, DiceSlot member) {
            if (DiceStackAdjacency.IsAdjacentForMatch(work, member)) {
                return 0;
            }

            var manhattan = Mathf.Abs(work.Cell.x - member.Cell.x)
                + Mathf.Abs(work.Cell.y - member.Cell.y);
            if (manhattan == 0) {
                return 1;
            }

            var horizontal = Mathf.Max(0, manhattan - 1);
            if (work.Tier != member.Tier) {
                return Mathf.Max(1, horizontal);
            }

            return horizontal;
        }

        static bool IsJoinGoal(
            int workDieIndex,
            DiceController[] participants,
            IReadOnlyList<DiceState> states,
            IReadOnlyList<DiceSnapshot> cluster,
            int targetFace,
            int carriedIndex) {
            if (states == null
                || participants == null
                || workDieIndex < 0
                || workDieIndex >= states.Count
                || cluster == null
                || cluster.Count == 0
                || carriedIndex == workDieIndex) {
                return false;
            }

            var workState = states[workDieIndex];
            if (workState.Orientation.Top != targetFace) {
                return false;
            }

            var workSlot = new DiceSlot(workState.GridPos, workState.Tier);
            for (var i = 0; i < cluster.Count; i++) {
                var member = cluster[i].Controller;
                if (member == null) {
                    continue;
                }

                DiceState memberState;
                var participantIndex = IndexOf(participants, member);
                if (participantIndex >= 0) {
                    if (participantIndex == carriedIndex) {
                        continue;
                    }

                    memberState = states[participantIndex];
                } else {
                    var snap = cluster[i];
                    memberState = new DiceState(snap.GridPos, snap.Orientation, snap.Tier, snap.Kind);
                }

                if (memberState.Orientation.Top != targetFace) {
                    continue;
                }

                var memberSlot = new DiceSlot(memberState.GridPos, memberState.Tier);
                if (DiceStackAdjacency.IsAdjacentForMatch(workSlot, memberSlot)) {
                    return true;
                }
            }

            return false;
        }

        static bool TrySelectParticipants(
            DiceController workDie,
            DiceState workStartState,
            IReadOnlyList<DiceSnapshot> cluster,
            IReadOnlyList<DiceSnapshot> planningDice,
            int participantCap,
            out DiceController[] participants,
            out DiceState[] startStates,
            out int workDieIndex) {
            participants = null;
            startStates = null;
            workDieIndex = 0;

            if (participantCap <= 0) {
                participantCap = 1;
            }

            var selected = new List<DiceController>(participantCap) { workDie };
            var anchors = new List<Vector2Int> { workStartState.GridPos };
            if (cluster != null) {
                for (var i = 0; i < cluster.Count; i++) {
                    anchors.Add(cluster[i].GridPos);
                }
            }

            if (planningDice != null && participantCap > 1) {
                var ranked = new List<(DiceController die, int dist)>();
                for (var i = 0; i < planningDice.Count; i++) {
                    var snap = planningDice[i];
                    if (!CanParticipate(snap, workDie)) {
                        continue;
                    }

                    ranked.Add((snap.Controller, MinAnchorDistance(snap.GridPos, anchors)));
                }

                ranked.Sort((a, b) => a.dist.CompareTo(b.dist));
                for (var i = 0; i < ranked.Count && selected.Count < participantCap; i++) {
                    if (selected.Contains(ranked[i].die)) {
                        continue;
                    }

                    selected.Add(ranked[i].die);
                }
            }

            participants = selected.ToArray();
            startStates = new DiceState[participants.Length];
            for (var i = 0; i < participants.Length; i++) {
                startStates[i] = i == 0 ? workStartState : participants[i].CurrentState;
            }

            workDieIndex = 0;
            return participants.Length > 0;
        }

        static bool CanParticipate(DiceSnapshot snap, DiceController workDie) {
            if (snap.Controller == null || snap.Controller == workDie) {
                return false;
            }

            if (snap.IsBusy || snap.IsErasing || snap.IsCarried) {
                return false;
            }

            var caps = DiceBehaviorResolver.GetCapabilities(snap.Kind);
            return (caps.CanGridRoll || caps.CanBeLiftedByPlayer) && !caps.HasExpandedFootprint;
        }

        static int MinAnchorDistance(Vector2Int cell, List<Vector2Int> anchors) {
            var best = int.MaxValue;
            for (var i = 0; i < anchors.Count; i++) {
                var d = DiceBoardAnalyzer.ManhattanDistance(cell, anchors[i]);
                if (d < best) {
                    best = d;
                }
            }

            return best;
        }

        static int IndexOf(DiceController[] participants, DiceController die) {
            for (var i = 0; i < participants.Length; i++) {
                if (participants[i] == die) {
                    return i;
                }
            }

            return -1;
        }

        static DiceState[] CloneStates(IReadOnlyList<DiceState> states) {
            var clone = new DiceState[states.Count];
            for (var i = 0; i < states.Count; i++) {
                clone[i] = states[i];
            }

            return clone;
        }

        static string BuildConfigKey(IReadOnlyList<DiceState> states, int carriedIndex) {
            var sb = new StringBuilder(states.Count * 24 + 8);
            for (var i = 0; i < states.Count; i++) {
                var s = states[i];
                if (i > 0) {
                    sb.Append('|');
                }

                if (i == carriedIndex) {
                    sb.Append("C");
                    continue;
                }

                sb.Append(s.GridPos.x).Append(',')
                    .Append(s.GridPos.y).Append(',')
                    .Append((int)s.Tier).Append(',')
                    .Append(s.Orientation.Top).Append(',')
                    .Append(s.Orientation.North).Append(',')
                    .Append(s.Orientation.East);
            }

            sb.Append("#").Append(carriedIndex);
            return sb.ToString();
        }
    }
}
