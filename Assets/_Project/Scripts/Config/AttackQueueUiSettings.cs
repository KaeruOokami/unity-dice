using System;
using UnityEngine;

namespace DiceGame.Config
{
    [Serializable]
    public struct AttackQueuePanelLayout
    {
        [SerializeField] Vector2 anchorMin;
        [SerializeField] Vector2 anchorMax;
        [SerializeField] Vector2 pivot;

        public AttackQueuePanelLayout(Vector2 min, Vector2 max, Vector2 panelPivot) {
            anchorMin = min;
            anchorMax = max;
            pivot = panelPivot;
        }

        public Vector2 AnchorMin => anchorMin;
        public Vector2 AnchorMax => anchorMax;
        public Vector2 Pivot => pivot;
    }

    [CreateAssetMenu(fileName = "AttackQueueUiSettings", menuName = "Dice/Attack Queue UI Settings")]
    public sealed class AttackQueueUiSettings : ScriptableObject
    {
        [Min(16)]
        [SerializeField] int iconResolution = 128;
        [Min(1f)]
        [SerializeField] float iconPixelsPerUnit = 100f;
        [Min(1f)]
        [SerializeField] float iconSize = 32f;
        [Min(0f)]
        [SerializeField] float columnSpacing = 2f;
        [Min(0f)]
        [SerializeField] float rowSpacing = 2f;
        [Min(1f)]
        [SerializeField] float boundsPadding = 1.15f;
        [Min(0f)]
        [SerializeField] float previewLightIntensity = 1.2f;
        [SerializeField] string previewLayerName = "DiceIconPreview";
        [SerializeField] AttackQueuePanelLayout player1PanelLayout = new(
            new Vector2(0.02f, 0.98f),
            new Vector2(0.02f, 0.98f),
            new Vector2(0f, 1f));
        [SerializeField] AttackQueuePanelLayout player2PanelLayout = new(
            new Vector2(0.98f, 0.98f),
            new Vector2(0.98f, 0.98f),
            Vector2.one);

        public int IconResolution => iconResolution;
        public float IconPixelsPerUnit => iconPixelsPerUnit;
        public float IconSize => iconSize;
        public float ColumnSpacing => columnSpacing;
        public float RowSpacing => rowSpacing;
        public float BoundsPadding => boundsPadding;
        public float PreviewLightIntensity => previewLightIntensity;
        public string PreviewLayerName => previewLayerName;
        public AttackQueuePanelLayout Player1PanelLayout => player1PanelLayout;
        public AttackQueuePanelLayout Player2PanelLayout => player2PanelLayout;

        public static AttackQueueUiSettings CreateRuntimeFallback() {
            return CreateInstance<AttackQueueUiSettings>();
        }
    }
}
