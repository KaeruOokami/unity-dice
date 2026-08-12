using DiceGame.Grid;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DiceGame.Gameplay.AI.Application
{
    /// <summary>
    /// Scene-view gizmos for AI intent. Drawn only in the Editor Scene view.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AiDebugOverlayGizmo : MonoBehaviour
    {
        static readonly Color ClusterColor = new Color(0.2f, 0.75f, 1f, 0.28f);
        static readonly Color SubGoalColor = new Color(1f, 0.85f, 0.2f, 0.9f);
        static readonly Color WorkDieColor = new Color(1f, 0.35f, 0.85f, 0.95f);
        static readonly Color PlanPathColor = new Color(0.3f, 1f, 0.4f, 0.95f);
        static readonly Color StepColor = new Color(1f, 1f, 1f, 1f);
        static readonly Color PlayerColor = new Color(1f, 0.55f, 0.1f, 1f);
        static readonly Color MountColor = new Color(1f, 0.45f, 0.1f, 0.95f);
        static readonly Color RecoveryColor = new Color(0.3f, 0.95f, 0.95f, 0.95f);
        static readonly Color DescendColor = new Color(1f, 0.2f, 0.2f, 0.95f);

        AiCharacterBrain brain;

        public void Bind(AiCharacterBrain targetBrain) {
            brain = targetBrain;
        }

        void OnDrawGizmos() {
            if (brain == null || !brain.DebugGizmoEnabled) {
                return;
            }

            var snapshot = brain.DebugOverlay;
            var board = brain.DebugBoard;
            if (snapshot == null || !snapshot.HasData || board == null) {
                return;
            }

            var cellSize = board.CellSize;
            var cubeSize = Vector3.one * (cellSize * 0.85f);
            var liftY = cellSize * 0.15f;

            for (var i = 0; i < snapshot.ClusterCells.Count; i++) {
                DrawCellCube(board, snapshot.ClusterCells[i], cubeSize, liftY, ClusterColor);
            }

            if (snapshot.PlanPathCells.Count > 0) {
                Gizmos.color = PlanPathColor;
                for (var i = 0; i < snapshot.PlanPathCells.Count; i++) {
                    var point = CellWorld(board, snapshot.PlanPathCells[i], liftY + cellSize * 0.35f);
                    Gizmos.DrawSphere(point, cellSize * 0.12f);
                    if (i > 0) {
                        var prev = CellWorld(board, snapshot.PlanPathCells[i - 1], liftY + cellSize * 0.35f);
                        Gizmos.DrawLine(prev, point);
                    }
                }
            }

            if (snapshot.SubGoalTargetCell.HasValue) {
                DrawCellWire(board, snapshot.SubGoalTargetCell.Value, cubeSize, liftY, SubGoalColor);
            }

            if (snapshot.WorkDieCell.HasValue) {
                Gizmos.color = WorkDieColor;
                Gizmos.DrawSphere(
                    CellWorld(board, snapshot.WorkDieCell.Value, liftY + cellSize * 0.55f),
                    cellSize * 0.18f);
            }

            if (snapshot.StepCell.HasValue) {
                Gizmos.color = StepColor;
                var from = CellWorld(board, snapshot.PlayerCell, liftY + cellSize * 0.5f);
                var to = CellWorld(board, snapshot.StepCell.Value, liftY + cellSize * 0.5f);
                Gizmos.DrawLine(from, to);
                Gizmos.DrawSphere(to, cellSize * 0.14f);
            }

            if (snapshot.HighlightCell.HasValue) {
                var highlightColor = snapshot.Mode switch {
                    AiDebugOverlayMode.SinkingMount => MountColor,
                    AiDebugOverlayMode.SinkingDescend => DescendColor,
                    AiDebugOverlayMode.FloorRecovery => RecoveryColor,
                    _ => SubGoalColor
                };
                DrawCellWire(board, snapshot.HighlightCell.Value, cubeSize * 1.05f, liftY, highlightColor);
            }

            Gizmos.color = PlayerColor;
            Gizmos.DrawWireSphere(
                CellWorld(board, snapshot.PlayerCell, liftY + cellSize * 0.5f),
                cellSize * 0.22f);

#if UNITY_EDITOR
            var label = BuildLabel(snapshot);
            if (!string.IsNullOrEmpty(label)) {
                Handles.Label(
                    CellWorld(board, snapshot.PlayerCell, liftY + cellSize * 1.1f),
                    label);
            }
#endif
        }

        static string BuildLabel(AiDebugOverlaySnapshot snapshot) {
            var face = snapshot.GoalFace > 0 ? $"face={snapshot.GoalFace}" : "face=-";
            var mode = snapshot.Mode.ToString();
            var sub = string.IsNullOrEmpty(snapshot.SubGoalLabel) ? "-" : snapshot.SubGoalLabel;
            var action = string.IsNullOrEmpty(snapshot.ActionLabel) ? "-" : snapshot.ActionLabel;
            return $"[AI] {mode} {face}\n{sub}\n{action}";
        }

        static void DrawCellCube(
            Board board,
            Vector2Int cell,
            Vector3 cubeSize,
            float liftY,
            Color color) {
            Gizmos.color = color;
            Gizmos.DrawCube(CellWorld(board, cell, liftY), cubeSize);
        }

        static void DrawCellWire(
            Board board,
            Vector2Int cell,
            Vector3 cubeSize,
            float liftY,
            Color color) {
            Gizmos.color = color;
            Gizmos.DrawWireCube(CellWorld(board, cell, liftY), cubeSize);
        }

        static Vector3 CellWorld(Board board, Vector2Int cell, float liftY) {
            var world = board.GridToWorld(cell);
            world.y = board.FloorSurfaceWorldY + liftY;
            return world;
        }
    }
}
