using System.Collections.Generic;
using System.Text;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Gameplay;
using DiceGame.Placement;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Domain
{
    /// <summary>
    /// Multi-die configuration-space A* for Join.
    /// State = poses of a pruned movable set; action = roll any one participant one step.
    /// Search is time-sliced via <see cref="MultiDieJoinSearchSession"/>.
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

        public sealed class SearchNode
        {
            public DiceState[] States { get; }
            public List<MultiDieRollStep> Path { get; }
            public List<DiceState[]> Checkpoints { get; }
            public int G { get; }
            public int F { get; }

            public SearchNode(
                DiceState[] states,
                List<MultiDieRollStep> path,
                List<DiceState[]> checkpoints,
                int g,
                int f) {
                States = states;
                Path = path;
                Checkpoints = checkpoints;
                G = g;
                F = f;
            }
        }

        /// <summary>
        /// Binary min-heap ordered by F then G for Join A*.
        /// </summary>
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

                    (items[index], items[parent]) = (items[parent], items[index]);
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

                    (items[index], items[best]) = (items[best], items[index]);
                    index = best;
                }
            }
        }

        /// <summary>
        /// Build a one-participant plan from an already-found direction list (face-one clear etc.).
        /// </summary>
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
            if (passability == null
                || workDie == null
                || directions == null) {
                return false;
            }

            var participants = new[] { workDie };
            var steps = new List<MultiDieRollStep>(directions.Count);
            var checkpoints = new List<DiceState[]>(directions.Count + 1) {
                new[] { startState }
            };

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

                steps.Add(new MultiDieRollStep(workDie, directions[i]));
                checkpoints.Add(new[] { state });
            }

            plan = new MultiDieRollPlan(participants, steps, checkpoints, workDieIndex: 0);
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

                // Exhausted at this participant cap: widen the movable set and retry.
                if (TryEscalateParticipantCap(
                    session,
                    workDie,
                    workStartState,
                    cluster,
                    planningDice,
                    maxDepth,
                    maxNodesTotal,
                    out var escalated)) {
                    session.Deactivate();
                    session = escalated;
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

            if (IsJoinGoal(workDieIndex, participants, startStates, cluster, targetFace)) {
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
                maxNodesTotal);
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
                        maxNodesTotal);
                    return true;
                }

                nextCap++;
            }

            return false;
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
            int maxNodesTotal) {
            var startKey = BuildConfigKey(startStates);
            var bestG = new Dictionary<string, int> { { startKey, 0 } };
            var open = new JoinSearchOpenHeap();
            var h = EstimateJoinCost(workDieIndex, participants, startStates, cluster, targetFace);
            open.Push(new SearchNode(
                startStates,
                path: new List<MultiDieRollStep>(),
                checkpoints: new List<DiceState[]> { CloneStates(startStates) },
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

            // Board drifted from the pose that started this search: abandon.
            return MultiDieRollPlanUtil.MatchesLive(session.Participants, session.StartStates);
        }

        static MultiDieJoinSearchStatus ExpandSession(
            MultiDieJoinSearchSession session,
            MovementTransitionEvaluator passability,
            float footingWorldY,
            PlayerSlot movementOwner,
            bool allowJump,
            DiceRegistry registry,
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
                var currentKey = BuildConfigKey(current.States);
                if (session.BestG.TryGetValue(currentKey, out var bestG)
                    && current.G > bestG) {
                    continue;
                }

                // A*: first time a goal is dequeued is optimal with admissible h.
                if (current.G > 0
                    && IsJoinGoal(
                        session.WorkDieIndex,
                        participants,
                        current.States,
                        session.Cluster,
                        session.TargetFace)) {
                    plan = new MultiDieRollPlan(
                        participants,
                        current.Path,
                        current.Checkpoints,
                        session.WorkDieIndex);
                    return MultiDieJoinSearchStatus.Found;
                }

                if (current.G >= session.MaxDepth) {
                    continue;
                }

                expandedThisCall++;
                session.ExpandedTotal++;

                using (registry.BeginPlanningLayout(participants, current.States)) {
                    for (var dieIndex = 0; dieIndex < participants.Length; dieIndex++) {
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
                            var nextG = current.G + 1;
                            var key = BuildConfigKey(nextStates);
                            if (session.BestG.TryGetValue(key, out var knownG) && knownG <= nextG) {
                                continue;
                            }

                            session.BestG[key] = nextG;

                            var nextPath = new List<MultiDieRollStep>(current.Path.Count + 1);
                            nextPath.AddRange(current.Path);
                            nextPath.Add(new MultiDieRollStep(mover, direction));

                            var nextCheckpoints = new List<DiceState[]>(current.Checkpoints.Count + 1);
                            nextCheckpoints.AddRange(current.Checkpoints);
                            nextCheckpoints.Add(CloneStates(nextStates));

                            var h = EstimateJoinCost(
                                session.WorkDieIndex,
                                participants,
                                nextStates,
                                session.Cluster,
                                session.TargetFace);
                            session.Open.Push(
                                new SearchNode(nextStates, nextPath, nextCheckpoints, nextG, nextG + h));
                        }
                    }
                }
            }

            return MultiDieJoinSearchStatus.Exhausted;
        }

        /// <summary>
        /// Admissible Join heuristic: work die must show target face (orient distance),
        /// and against immovable cluster members also needs a horizontal approach lower bound.
        /// Movable cluster members are ignored for approach (they may walk to the work die).
        /// </summary>
        static int EstimateJoinCost(
            int workDieIndex,
            DiceController[] participants,
            IReadOnlyList<DiceState> states,
            IReadOnlyList<DiceSnapshot> cluster,
            int targetFace) {
            if (IsJoinGoal(workDieIndex, participants, states, cluster, targetFace)) {
                return 0;
            }

            var work = states[workDieIndex];
            var orientCost = MinRollsToFace(work.Orientation, targetFace);
            var approachCost = int.MaxValue;

            for (var i = 0; i < cluster.Count; i++) {
                var member = cluster[i].Controller;
                if (member == null) {
                    continue;
                }

                // Participant cluster dice can move toward the work die — do not use their
                // current distance (may overestimate when cooperation is optimal).
                if (IndexOf(participants, member) >= 0) {
                    continue;
                }

                var snap = cluster[i];
                if (snap.Orientation.Top != targetFace) {
                    continue;
                }

                var memberSlot = new DiceSlot(snap.GridPos, snap.Tier);
                var workSlot = new DiceSlot(work.GridPos, work.Tier);
                approachCost = Mathf.Min(approachCost, ApproachLowerBound(workSlot, memberSlot));
            }

            if (approachCost == int.MaxValue) {
                return orientCost;
            }

            return Mathf.Max(orientCost, approachCost);
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

            // Same-tier match needs grid distance 1; different tier still needs ≥1 action.
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
            int targetFace) {
            if (states == null
                || participants == null
                || workDieIndex < 0
                || workDieIndex >= states.Count
                || cluster == null
                || cluster.Count == 0) {
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
            return caps.CanGridRoll && !caps.HasExpandedFootprint;
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

        static string BuildConfigKey(IReadOnlyList<DiceState> states) {
            var sb = new StringBuilder(states.Count * 24);
            for (var i = 0; i < states.Count; i++) {
                var s = states[i];
                if (i > 0) {
                    sb.Append('|');
                }

                sb.Append(s.GridPos.x).Append(',')
                    .Append(s.GridPos.y).Append(',')
                    .Append((int)s.Tier).Append(',')
                    .Append(s.Orientation.Top).Append(',')
                    .Append(s.Orientation.North).Append(',')
                    .Append(s.Orientation.East);
            }

            return sb.ToString();
        }
    }
}
