using System.Collections.Generic;
using UnityEngine;

namespace DiceGame.Gameplay.AI.Application
{
    public enum AiDebugOverlayMode
    {
        Idle,
        Goal,
        SinkingMount,
        SinkingDescend,
        FloorRecovery,
        Ml
    }

    /// <summary>
    /// Read-only planning snapshot for Scene gizmos. Domain stays free of drawing code.
    /// </summary>
    public sealed class AiDebugOverlaySnapshot
    {
        readonly List<Vector2Int> clusterCells = new List<Vector2Int>(16);
        readonly List<Vector2Int> planPathCells = new List<Vector2Int>(16);

        public bool HasData { get; private set; }
        public AiDebugOverlayMode Mode { get; private set; }
        public int GoalFace { get; private set; }
        public Vector2Int PlayerCell { get; private set; }
        public Vector2Int? SubGoalTargetCell { get; private set; }
        public Vector2Int? WorkDieCell { get; private set; }
        public Vector2Int? StepCell { get; private set; }
        public Vector2Int? ActionGoalCell { get; private set; }
        public Vector2Int? HighlightCell { get; private set; }
        public string SubGoalLabel { get; private set; }
        public string ActionLabel { get; private set; }
        public IReadOnlyList<Vector2Int> ClusterCells => clusterCells;
        public IReadOnlyList<Vector2Int> PlanPathCells => planPathCells;

        public void Clear() {
            HasData = false;
            Mode = AiDebugOverlayMode.Idle;
            GoalFace = 0;
            PlayerCell = default;
            SubGoalTargetCell = null;
            WorkDieCell = null;
            StepCell = null;
            ActionGoalCell = null;
            HighlightCell = null;
            SubGoalLabel = null;
            ActionLabel = null;
            clusterCells.Clear();
            planPathCells.Clear();
        }

        public void BeginFrame(
            AiDebugOverlayMode mode,
            int goalFace,
            Vector2Int playerCell,
            string subGoalLabel,
            string actionLabel) {
            HasData = true;
            Mode = mode;
            GoalFace = goalFace;
            PlayerCell = playerCell;
            SubGoalLabel = subGoalLabel;
            ActionLabel = actionLabel;
            SubGoalTargetCell = null;
            WorkDieCell = null;
            StepCell = null;
            ActionGoalCell = null;
            HighlightCell = null;
            clusterCells.Clear();
            planPathCells.Clear();
        }

        public void SetSubGoalTarget(Vector2Int cell) {
            SubGoalTargetCell = cell;
        }

        public void SetWorkDieCell(Vector2Int cell) {
            WorkDieCell = cell;
        }

        public void SetStep(Vector2Int stepCell, Vector2Int actionGoalCell) {
            StepCell = stepCell;
            ActionGoalCell = actionGoalCell;
        }

        public void SetHighlightCell(Vector2Int cell) {
            HighlightCell = cell;
        }

        public void AddClusterCell(Vector2Int cell) {
            if (!clusterCells.Contains(cell)) {
                clusterCells.Add(cell);
            }
        }

        public void AddPlanPathCell(Vector2Int cell) {
            planPathCells.Add(cell);
        }
    }
}
