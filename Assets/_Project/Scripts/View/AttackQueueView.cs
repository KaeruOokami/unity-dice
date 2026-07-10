using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using DiceGame.Grid;
using DiceGame.Versus.Core;
using UnityEngine;

namespace DiceGame.View
{
    public class AttackQueueView : MonoBehaviour
    {
        [SerializeField] float iconScale = 0.35f;
        [SerializeField] float columnSpacing = 0.6f;
        [SerializeField] float rowSpacing = 0.55f;
        [SerializeField] float heightOffset = 3f;

        Board board;
        DiceCatalog catalog;
        Transform player1Root;
        Transform player2Root;

        public void Configure(Board targetBoard, DiceCatalog targetCatalog, Transform parent) {
            board = targetBoard;
            catalog = targetCatalog;
            var parentTransform = parent != null ? parent : transform;
            player1Root = EnsureRoot(parentTransform, "AttackQueueIcons_P1");
            player2Root = EnsureRoot(parentTransform, "AttackQueueIcons_P2");
        }

        public void RenderAll(
            IReadOnlyList<AttackVolley> player1Volleys,
            IReadOnlyList<AttackVolley> player2Volleys) {
            RenderForSlot(PlayerSlot.Player1, player1Volleys, player1Root);
            RenderForSlot(PlayerSlot.Player2, player2Volleys, player2Root);
        }

        void RenderForSlot(PlayerSlot defenderSlot, IReadOnlyList<AttackVolley> volleys, Transform root) {
            ClearRoot(root);
            if (board == null || catalog == null || root == null || volleys == null || volleys.Count == 0) {
                return;
            }

            if (board.VersusLayout == null) {
                return;
            }

            board.VersusLayout.GetPlayerGridBounds(defenderSlot, out var minCell, out var maxCell);
            var centerCell = new Vector2Int((minCell.x + maxCell.x) / 2, maxCell.y);
            var basePosition = board.GridToWorld(centerCell) + Vector3.up * heightOffset;

            for (var column = 0; column < volleys.Count; column++) {
                var volley = volleys[column];
                if (volley == null) {
                    continue;
                }

                for (var row = 0; row < volley.Count; row++) {
                    var spec = volley.Dice[row];
                    if (!catalog.TryGetMeshPrefab(spec.Kind, out var meshPrefab) || meshPrefab == null) {
                        continue;
                    }

                    var iconObject = Instantiate(meshPrefab, root);
                    iconObject.name = $"AttackIcon_{defenderSlot}_{column}_{row}_{spec.Kind}_{spec.Pip}";
                    iconObject.transform.localScale = Vector3.one * iconScale;

                    var offset = Vector3.right * (column * columnSpacing) + Vector3.back * (row * rowSpacing);
                    iconObject.transform.position = basePosition + offset;
                    iconObject.transform.rotation = DiceOrientationMapper.ToRotation(
                        DiceOrientation.CreateWithTopFace(spec.Pip));
                }
            }
        }

        static Transform EnsureRoot(Transform parent, string name) {
            var existing = parent.Find(name);
            if (existing != null) {
                return existing;
            }

            var rootObject = new GameObject(name);
            rootObject.transform.SetParent(parent, false);
            return rootObject.transform;
        }

        static void ClearRoot(Transform root) {
            if (root == null) {
                return;
            }

            for (var i = root.childCount - 1; i >= 0; i--) {
                Destroy(root.GetChild(i).gameObject);
            }
        }
    }
}
