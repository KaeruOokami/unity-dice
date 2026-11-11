using DiceGame.Config;
using DiceGame.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceGame.Session
{
    sealed class DiceCatalogPanelUi
    {
        public sealed class Bindings
        {
            public DiceCatalogEntryData[] BaseEntries;
            public TMP_InputField[] WeightFields;
            public Toggle[] IgniteOnSpawnToggles;
        }

        public static Bindings Build(Transform parent, string sectionLabel, DiceCatalogData template) {
            var section = SessionUiFactory.CreateVerticalSection(parent, sectionLabel);
            SessionUiFactory.CreateLayoutLabel(section, sectionLabel, 20, 28f);

            var entries = template.Entries ?? System.Array.Empty<DiceCatalogEntryData>();
            var weightFields = new TMP_InputField[entries.Length];
            var igniteToggles = new Toggle[entries.Length];
            for (var i = 0; i < entries.Length; i++) {
                var row = SessionUiFactory.CreateTwoColumnRow(section, $"CatalogRow_{i}");
                weightFields[i] = SessionUiFactory.CreateKindLabeledFloatInput(
                    row,
                    entries[i].Kind,
                    "Spawn Weight");
                var canIgnite = DiceFirePolicy.CanCarryFire(entries[i].Kind);
                igniteToggles[i] = SessionUiFactory.CreateLabeledToggle(row, "Ignite On Spawn");
                igniteToggles[i].interactable = canIgnite;
                igniteToggles[i].isOn = canIgnite && entries[i].IgniteOnSpawn;
            }

            return new Bindings {
                BaseEntries = entries,
                WeightFields = weightFields,
                IgniteOnSpawnToggles = igniteToggles
            };
        }

        public static void Apply(Bindings bindings, DiceCatalogData data) {
            if (bindings == null || bindings.WeightFields == null || bindings.BaseEntries == null) {
                return;
            }

            for (var i = 0; i < bindings.WeightFields.Length; i++) {
                var weight = 0f;
                var ignite = false;
                if (data.Entries != null) {
                    FindEntry(data.Entries, bindings.BaseEntries[i].Kind, out weight, out ignite);
                }

                SetInputText(bindings.WeightFields[i], weight.ToString("0.###"));
                if (bindings.IgniteOnSpawnToggles != null && i < bindings.IgniteOnSpawnToggles.Length) {
                    var canIgnite = DiceFirePolicy.CanCarryFire(bindings.BaseEntries[i].Kind);
                    bindings.IgniteOnSpawnToggles[i].interactable = canIgnite;
                    bindings.IgniteOnSpawnToggles[i].isOn = canIgnite && ignite;
                }
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

                var ignite = bindings.IgniteOnSpawnToggles != null
                    && i < bindings.IgniteOnSpawnToggles.Length
                    && bindings.IgniteOnSpawnToggles[i] != null
                    && bindings.IgniteOnSpawnToggles[i].isOn
                    && DiceFirePolicy.CanCarryFire(bindings.BaseEntries[i].Kind);

                entries[i] = new DiceCatalogEntryData {
                    Kind = bindings.BaseEntries[i].Kind,
                    MeshPrefab = bindings.BaseEntries[i].MeshPrefab,
                    SpawnWeight = weight,
                    IgniteOnSpawn = ignite
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

        static void FindEntry(
            DiceCatalogEntryData[] entries,
            DiceKind kind,
            out float weight,
            out bool igniteOnSpawn) {
            weight = 0f;
            igniteOnSpawn = false;
            for (var i = 0; i < entries.Length; i++) {
                if (entries[i].Kind == kind) {
                    weight = entries[i].SpawnWeight;
                    igniteOnSpawn = entries[i].IgniteOnSpawn;
                    return;
                }
            }
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
