using System.Collections.Generic;
using DiceGame.Core;
using DiceGame.Gameplay;

namespace DiceGame.Gameplay.AI.Domain
{
    public enum MultiDieJoinSearchStatus
    {
        Invalid,
        Found,
        InProgress,
        Exhausted
    }

    /// <summary>
    /// Time-sliced multi-die Join A* state (Roll + optional Lift/Place).
    /// </summary>
    public sealed class MultiDieJoinSearchSession
    {
        public DiceController[] Participants { get; }
        public int ParticipantCap { get; }
        public int MaxParticipants { get; }
        public int WorkDieIndex { get; }
        public int TargetFace { get; }
        public IReadOnlyList<DiceSnapshot> Cluster { get; }
        public WorkDieRollCrushAvoidance CrushAvoidance { get; }
        public bool UsedAvoidanceFallback { get; }
        public bool AllowLift { get; }
        public int LiftActionCost { get; }
        public int PlaceActionCost { get; }
        public DiceState[] StartStates { get; }
        public Dictionary<string, int> BestG { get; }
        public MultiDieConfigSpacePlanner.JoinSearchOpenHeap Open { get; }
        public int ExpandedTotal { get; set; }
        public int MaxDepth { get; }
        public int MaxNodesTotal { get; }
        public bool IsActive { get; private set; }

        public MultiDieJoinSearchSession(
            DiceController[] participants,
            int participantCap,
            int maxParticipants,
            int workDieIndex,
            int targetFace,
            IReadOnlyList<DiceSnapshot> cluster,
            WorkDieRollCrushAvoidance crushAvoidance,
            bool usedAvoidanceFallback,
            bool allowLift,
            int liftActionCost,
            int placeActionCost,
            DiceState[] startStates,
            Dictionary<string, int> bestG,
            MultiDieConfigSpacePlanner.JoinSearchOpenHeap open,
            int maxDepth,
            int maxNodesTotal) {
            Participants = participants;
            ParticipantCap = participantCap;
            MaxParticipants = maxParticipants;
            WorkDieIndex = workDieIndex;
            TargetFace = targetFace;
            Cluster = cluster;
            CrushAvoidance = crushAvoidance;
            UsedAvoidanceFallback = usedAvoidanceFallback;
            AllowLift = allowLift;
            LiftActionCost = liftActionCost;
            PlaceActionCost = placeActionCost;
            StartStates = startStates;
            BestG = bestG;
            Open = open;
            MaxDepth = maxDepth;
            MaxNodesTotal = maxNodesTotal;
            ExpandedTotal = 0;
            IsActive = true;
        }

        public void Deactivate() {
            IsActive = false;
            Open?.Clear();
            BestG?.Clear();
        }
    }
}
