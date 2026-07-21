using DiceGame.Config;
using DiceGame.Core;
using TMPro;
using UnityEngine;

namespace DiceGame.Session
{
    sealed class DiceCatalogPanelUi
    {
        public sealed class Bindings
        {
            public DiceCatalogEntryData[] BaseEntries;
            public TMP_InputField[] WeightFields;
        }

        public static Bindings Build(Transform parent, string sectionLabel, DiceCatalogData template) {
            var section = LobbyUiFactory.CreateVerticalSection(parent, sectionLabel);
            LobbyUiFactory.CreateLayoutLabel(section, sectionLabel, 20, 28f);

            var entries = template.Entries ?? System.Array.Empty<DiceCatalogEntryData>();
            var weightFields = new TMP_InputField[entries.Length];
            for (var i = 0; i < entries.Length; i++) {
                weightFields[i] = LobbyUiFactory.CreateLabeledFloatInput(section, $"{entries[i].Kind} Spawn Weight");
            }

            return new Bindings {
                BaseEntries = entries,
                WeightFields = weightFields
            };
        }

        public static void Apply(Bindings bindings, DiceCatalogData data) {
            if (bindings == null || bindings.WeightFields == null || bindings.BaseEntries == null) {
                return;
            }

            for (var i = 0; i < bindings.WeightFields.Length; i++) {
                var weight = 0f;
                if (data.Entries != null) {
                    weight = FindWeight(data.Entries, bindings.BaseEntries[i].Kind);
                }

                SetInputText(bindings.WeightFields[i], weight.ToString("0.###"));
            }
        }

        public static bool TryRead(Bindings bindings, out DiceCatalogData data, out string errorMessage) {
            data = default;
            if (bindings == null || bindings.BaseEntries == null || bindings.WeightFields == null) {
                errorMessage = "Dice catalog UI is not initialized.";
                return false;
            }

            if (bindings.BaseEntries.Length != bindings.WeightFields.Length) {
                errorMessage = "Dice catalog UI bindings are inconsistent.";
                return false;
            }

            var entries = new DiceCatalogEntryData[bindings.BaseEntries.Length];
            for (var i = 0; i < bindings.BaseEntries.Length; i++) {
                if (!TryParseFloat(bindings.WeightFields[i], out var weight)) {
                    errorMessage = $"{bindings.BaseEntries[i].Kind} Spawn Weight must be a number.";
                    return false;
                }

                entries[i] = new DiceCatalogEntryData {
                    Kind = bindings.BaseEntries[i].Kind,
                    MeshPrefab = bindings.BaseEntries[i].MeshPrefab,
                    SpawnWeight = weight
                };
            }

            data = new DiceCatalogData { Entries = entries };
            if (!data.TryValidate(out errorMessage)) {
                data = default;
                return false;
            }

            errorMessage = null;
            return true;
        }

        static float FindWeight(DiceCatalogEntryData[] entries, DiceKind kind) {
            for (var i = 0; i < entries.Length; i++) {
                if (entries[i].Kind == kind) {
                    return entries[i].SpawnWeight;
                }
            }

            return 0f;
        }

        static void SetInputText(TMP_InputField input, string value) {
            if (input == null) {
                return;
            }

            input.SetTextWithoutNotify(value);
        }

        static bool TryParseFloat(TMP_InputField input, out float value) {
            value = 0f;
            return input != null && float.TryParse(input.text, out value);
        }
    }
}
