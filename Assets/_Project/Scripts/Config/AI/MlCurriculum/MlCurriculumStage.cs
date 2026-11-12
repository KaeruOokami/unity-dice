using DiceGame.Core;
using UnityEngine;

namespace DiceGame.Config
{
    /// <summary>
    /// One curriculum stage for ML training. Independent from Single Shared settings.
    /// Dice kinds are authored on the stage; DiceCatalog is not used (catalog is a spawn lottery).
    /// </summary>
    [CreateAssetMenu(fileName = "MlCurriculumStage", menuName = "Dice/AI/ML Curriculum Stage")]
    public sealed class MlCurriculumStage : ScriptableObject
    {
        [SerializeField] int stageNumber = 1;
        [SerializeField] string displayName = "Stage 1";

        [Header("Board")]
        [Min(3)]
        [SerializeField] int boardWidth = 5;
        [Min(3)]
        [SerializeField] int boardHeight = 5;

        [Header("One-Move Puzzle (Stage 1)")]
        [Range(2, 6)]
        [SerializeField] int targetFace = 2;
        [SerializeField] DiceKind workKind = DiceKind.Normal;
        [SerializeField] DiceKind partnerKind = DiceKind.Normal;

        [Header("Assets")]
        [SerializeField] MlAgentSettings mlAgentSettings;
        [SerializeField] DiceSpawnSettings spawnSettings;

        [Header("Episode")]
        [SerializeField] bool endEpisodeOnErasure = true;
        [SerializeField] bool animateInitialDiceSpawn;

        public int StageNumber => Mathf.Max(1, stageNumber);
        public string DisplayName =>
            string.IsNullOrWhiteSpace(displayName) ? $"Stage {StageNumber}" : displayName;
        public int BoardWidth => Mathf.Max(3, boardWidth);
        public int BoardHeight => Mathf.Max(3, boardHeight);
        public int TargetFace => Mathf.Clamp(targetFace, 2, 6);
        public DiceKind WorkKind => workKind;
        public DiceKind PartnerKind => partnerKind;
        public MlAgentSettings MlAgentSettings => mlAgentSettings;
        public DiceSpawnSettings SpawnSettings => spawnSettings;
        public bool EndEpisodeOnErasure => endEpisodeOnErasure;
        public bool AnimateInitialDiceSpawn => animateInitialDiceSpawn;

        public bool TryValidate(out string errorMessage) {
            if (mlAgentSettings == null) {
                errorMessage = $"{name}: MlAgentSettings is required.";
                return false;
            }

            if (spawnSettings == null) {
                errorMessage = $"{name}: DiceSpawnSettings is required.";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// Builds a Single-like snapshot for session plumbing.
        /// SharedCatalog comes from registry defaults only to satisfy MatchSetup validation;
        /// curriculum dice kinds are spawned via <see cref="WorkKind"/> / <see cref="PartnerKind"/>.
        /// </summary>
        public MatchSetupSnapshot CreateTrainingSnapshot(MatchSetupPresetRegistry registry) {
            var control = registry != null
                ? registry.GetControlDefaults(PlayerSlot.Player1)
                : PlayerSlotControlDefaults.Create(true, PlayerInputDeviceKind.Keyboard, 0);

            var spawnData = DiceSpawnSettingsData.FromTemplate(spawnSettings);
            spawnData.ContinuousSpawnEnabled = false;
            spawnData.AnimateInitialDiceSpawn = animateInitialDiceSpawn;
            spawnData.InitialDiceCount = 2;

            var plumbingCatalog = registry != null
                ? DiceCatalogData.FromTemplate(registry.DefaultSharedCatalog)
                : DiceCatalogData.Empty();

            return new MatchSetupSnapshot {
                GameMode = GameMode.Single,
                WinsToWin = 1,
                SharedSpawn = spawnData,
                SharedCatalog = plumbingCatalog,
                Player1 = PlayerSlotSetup.CreateDefault(
                    isAi: true,
                    inputConfig: control.InputConfig,
                    spawn: spawnData,
                    catalog: plumbingCatalog),
                Player2 = PlayerSlotSetup.CreateDefault(true, default)
            };
        }
    }
}
