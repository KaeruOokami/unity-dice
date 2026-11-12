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

    [CreateAssetMenu(fileName = "AttackQueueSettings", menuName = "Dice/View/Attack Queue Settings")]
    public sealed class AttackQueueSettings : ScriptableObject
    {
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
        [Header("Fire Icon Pulse")]
        [Min(0.05f)]
        [SerializeField] float fireIconPulsePeriodSeconds = 0.85f;
        [SerializeField] Color fireIconIdleColor = Color.white;
        [SerializeField] Color fireIconGlowColor = new(1f, 0.28f, 0.08f, 1f);
        [Header("Drop Anticipation")]
        [Min(1f)]
        [SerializeField] float dropAnticipationPeakScale = 1.35f;
        [Min(0.01f)]
        [SerializeField] float dropAnticipationPeakDurationSeconds = 0.12f;
        [Min(0f)]
        [SerializeField] float dropAnticipationGapSeconds = 0.06f;
        [Min(1)]
        [SerializeField] int dropAnticipationPeakCount = 2;
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
        public int IconPrewarmPerFrame => Mathf.Max(1, iconPrewarmPerFrame);
        public string PreviewLayerName => previewLayerName;
        public float FireIconPulsePeriodSeconds => Mathf.Max(0.05f, fireIconPulsePeriodSeconds);
        public Color FireIconIdleColor => fireIconIdleColor;
        public Color FireIconGlowColor => fireIconGlowColor;
        public float DropAnticipationPeakScale => Mathf.Max(1f, dropAnticipationPeakScale);
        public float DropAnticipationPeakDurationSeconds =>
            Mathf.Max(0.01f, dropAnticipationPeakDurationSeconds);
        public float DropAnticipationGapSeconds => Mathf.Max(0f, dropAnticipationGapSeconds);
        public int DropAnticipationPeakCount => Mathf.Max(1, dropAnticipationPeakCount);
        public float DropAnticipationDurationSeconds =>
            DropAnticipationPeakDurationSeconds * DropAnticipationPeakCount
            + DropAnticipationGapSeconds * Mathf.Max(0, DropAnticipationPeakCount - 1);
        public AttackQueuePanelLayout Player1PanelLayout => player1PanelLayout;
        public AttackQueuePanelLayout Player2PanelLayout => player2PanelLayout;

        public static AttackQueueSettings CreateRuntimeFallback() {
            return CreateInstance<AttackQueueSettings>();
        }
    }
}
