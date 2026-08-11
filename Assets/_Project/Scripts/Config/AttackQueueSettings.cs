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

    [CreateAssetMenu(fileName = "AttackQueueSettings", menuName = "Dice/Attack Queue Settings")]
    public sealed class AttackQueueSettings : ScriptableObject
    {
        [Header("Queue")]
        [Min(0f)]
        [SerializeField] float queueToBoardDelay = 1.5f;

        [Header("Icons")]
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
        [Min(1)]
        [SerializeField] int iconPrewarmPerFrame = 2;
        [SerializeField] string previewLayerName = "DiceIconPreview";
        [SerializeField] AttackQueuePanelLayout player1PanelLayout = new(
            new Vector2(0.02f, 0.98f),
            new Vector2(0.02f, 0.98f),
            new Vector2(0f, 1f));
        [SerializeField] AttackQueuePanelLayout player2PanelLayout = new(
            new Vector2(0.98f, 0.98f),
            new Vector2(0.98f, 0.98f),
            Vector2.one);

        public float QueueToBoardDelay => Mathf.Max(0f, queueToBoardDelay);
        public int IconResolution => iconResolution;
        public float IconPixelsPerUnit => iconPixelsPerUnit;
        public float IconSize => iconSize;
        public float ColumnSpacing => columnSpacing;
        public float RowSpacing => rowSpacing;
        public float BoundsPadding => boundsPadding;
        public float PreviewLightIntensity => previewLightIntensity;
        public int IconPrewarmPerFrame => Mathf.Max(1, iconPrewarmPerFrame);
        public string PreviewLayerName => previewLayerName;
        public AttackQueuePanelLayout Player1PanelLayout => player1PanelLayout;
        public AttackQueuePanelLayout Player2PanelLayout => player2PanelLayout;

        public static AttackQueueSettings CreateRuntimeFallback() {
            return CreateInstance<AttackQueueSettings>();
        }
    }
}
