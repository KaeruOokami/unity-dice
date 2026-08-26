using UnityEngine;

namespace DiceGame.Config
{
    [CreateAssetMenu(fileName = "MlCurriculumCatalog", menuName = "Dice/ML Curriculum Catalog")]
    public sealed class MlCurriculumCatalog : ScriptableObject
    {
        [Tooltip("Index 0 = Stage 1, index 1 = Stage 2, ...")]
        [SerializeField] MlCurriculumStage[] stages = System.Array.Empty<MlCurriculumStage>();

        public MlCurriculumStage[] Stages => stages ?? System.Array.Empty<MlCurriculumStage>();

        public bool TryGetStage(int stageNumber, out MlCurriculumStage stage) {
            stage = null;
            if (stageNumber < 1) {
                return false;
            }

            var list = Stages;
            for (var i = 0; i < list.Length; i++) {
                var candidate = list[i];
                if (candidate == null) {
                    continue;
                }

                if (candidate.StageNumber == stageNumber) {
                    stage = candidate;
                    return true;
                }
            }

            var index = stageNumber - 1;
            if (index >= 0 && index < list.Length && list[index] != null) {
                stage = list[index];
                return true;
            }

            return false;
        }

        public bool TryValidate(out string errorMessage) {
            var list = Stages;
            if (list.Length == 0) {
                errorMessage = "MlCurriculumCatalog: No stages assigned.";
                return false;
            }

            for (var i = 0; i < list.Length; i++) {
                if (list[i] == null) {
                    errorMessage = $"MlCurriculumCatalog: stages[{i}] is null.";
                    return false;
                }

                if (!list[i].TryValidate(out errorMessage)) {
                    return false;
                }
            }

            errorMessage = null;
            return true;
        }
    }
}
