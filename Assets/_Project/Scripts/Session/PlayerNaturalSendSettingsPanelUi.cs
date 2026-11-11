using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceGame.Session
{
    sealed class PlayerNaturalSendSettingsPanelUi
    {
        public sealed class Bindings
        {
            public RectTransform SectionRoot;
            public Toggle Enabled;
            public TMP_InputField DiceCountPerVolley;
            public Transform ListRoot;
            public List<KindRow> KindRows = new();
        }

        public sealed class KindRow
        {
            public TMP_Dropdown KindDropdown;
            public TMP_InputField MaxCountPerVolley;
            public TMP_InputField SelectionWeight;
            public Toggle IgniteOnSpawn;
        }

        public static Bindings Build(Transform parent, string sectionLabel, PlayerNaturalSendSettingsData template) {
            var section = SessionUiFactory.CreateVerticalSection(parent, sectionLabel);
            SessionUiFactory.CreateLayoutLabel(section, sectionLabel, 20, 28f);

            var headerRow = SessionUiFactory.CreateTwoColumnRow(section, "NaturalSendHeaderRow");
            var bindings = new Bindings {
                SectionRoot = section,
                Enabled = SessionUiFactory.CreateLabeledToggle(headerRow, "Natural Send Enabled"),
                DiceCountPerVolley = SessionUiFactory.CreateLabeledIntInput(headerRow, "Dice Count / Volley"),
                ListRoot = SessionUiFactory.CreateVerticalSection(section, "SendableKinds")
            };

            SessionUiFactory.CreateLayoutButton(section, "AddKindButton", "Add Kind", 36f, () => {
                AddKind(bindings);
            });

            RebuildKindRows(bindings, CloneKinds(template.SendableKinds));
            return bindings;
        }

        public static void Apply(Bindings bindings, PlayerNaturalSendSettingsData data) {
            if (bindings == null) {
                return;
            }

            bindings.Enabled.isOn = data.Enabled;
            SetInputText(bindings.DiceCountPerVolley, data.DiceCountPerVolley.ToString());
            RebuildKindRows(bindings, CloneKinds(data.SendableKinds));
        }

        public static bool TryRead(Bindings bindings, out PlayerNaturalSendSettingsData data, out string errorMessage) {
            data = default;
            if (bindings == null || bindings.KindRows == null) {
                errorMessage = "Natural send settings UI is not initialized.";
                return false;
            }

            if (!TryParseInt(bindings.DiceCountPerVolley, out var diceCount)) {
                errorMessage = "Natural Send Dice Count / Volley must be an integer.";
                return false;
            }

            if (!TryReadKinds(bindings, out var kinds, out errorMessage)) {
                return false;
            }

            data = new PlayerNaturalSendSettingsData {
                Enabled = bindings.Enabled.isOn,
                DiceCountPerVolley = diceCount,
                SendableKinds = kinds
            };

            if (!data.TryValidate(out errorMessage)) {
                data = default;
                return false;
            }

            errorMessage = null;
            return true;
        }

        static void AddKind(Bindings bindings) {
            if (!TryReadKindsLenient(bindings, out var kinds)) {
                kinds = new List<NaturalSendKindLimitData>();
            }

            kinds.Add(new NaturalSendKindLimitData {
                Kind = DiceKind.Normal,
                MaxCountPerVolley = 1,
                SelectionWeight = 1f
            });
            RebuildKindRows(bindings, kinds);
        }

        static void RemoveKind(Bindings bindings, int index) {
            if (!TryReadKindsLenient(bindings, out var kinds)) {
                return;
            }

            if (index < 0 || index >= kinds.Count) {
                return;
            }

            kinds.RemoveAt(index);
            RebuildKindRows(bindings, kinds);
        }

        static void RebuildKindRows(Bindings bindings, List<NaturalSendKindLimitData> kinds) {
            SessionUiFactory.ClearChildren(bindings.ListRoot);
            bindings.KindRows.Clear();

            for (var i = 0; i < kinds.Count; i++) {
                var index = i;
                var kindSection = SessionUiFactory.CreateVerticalSection(bindings.ListRoot, $"Kind_{i}");
                SessionUiFactory.CreateLayoutLabel(kindSection, $"Kind {i + 1}", 18, 24f);

                SessionUiFactory.CreateLayoutLabel(kindSection, "Kind", 18, 24f);
                var kindDropdown = SessionUiFactory.CreateDiceKindDropdown(
                    kindSection,
                    "KindDropdown",
                    44f);
                kindDropdown.value = (int)kinds[i].Kind;
                kindDropdown.RefreshShownValue();

                var fieldsRow = SessionUiFactory.CreateTwoColumnRow(kindSection, "KindFieldsRow");
                var maxField = SessionUiFactory.CreateLabeledIntInput(fieldsRow, "Max / Volley");
                SetInputText(maxField, kinds[i].MaxCountPerVolley.ToString());
                var weightField = SessionUiFactory.CreateLabeledFloatInput(fieldsRow, "Weight");
                SetInputText(weightField, kinds[i].SelectionWeight.ToString("0.###"));
                var igniteRow = SessionUiFactory.CreateTwoColumnRow(kindSection, "KindIgniteRow");
                var igniteToggle = SessionUiFactory.CreateLabeledToggle(igniteRow, "Ignite On Spawn");
                var canIgnite = DiceFirePolicy.CanCarryFire(kinds[i].Kind);
                igniteToggle.interactable = canIgnite;
                igniteToggle.isOn = canIgnite && kinds[i].IgniteOnSpawn;
                kindDropdown.onValueChanged.AddListener(value => {
                    var allowed = DiceFirePolicy.CanCarryFire((DiceKind)value);
                    igniteToggle.interactable = allowed;
                    if (!allowed) {
                        igniteToggle.isOn = false;
                    }
                });

                SessionUiFactory.CreateLayoutButton(kindSection, "RemoveKindButton", "Remove Kind", 36f, () => {
                    RemoveKind(bindings, index);
                });

                bindings.KindRows.Add(new KindRow {
                    KindDropdown = kindDropdown,
                    MaxCountPerVolley = maxField,
                    SelectionWeight = weightField,
                    IgniteOnSpawn = igniteToggle
                });
            }

            SessionUiFactory.ForceRebuildLayout(bindings.SectionRoot);
            if (bindings.SectionRoot.parent is RectTransform parentRect) {
                SessionUiFactory.ForceRebuildLayout(parentRect);
            }
        }

        static bool TryReadKinds(Bindings bindings, out NaturalSendKindLimitData[] kinds, out string errorMessage) {
            kinds = null;
            var rows = bindings.KindRows;
            var result = new NaturalSendKindLimitData[rows.Count];
            for (var i = 0; i < rows.Count; i++) {
                if (!TryParseInt(rows[i].MaxCountPerVolley, out var maxCount)
                    || !TryParseFloat(rows[i].SelectionWeight, out var weight)) {
                    errorMessage = $"Natural Send kind {i + 1} has invalid values.";
                    return false;
                }

                var kind = (DiceKind)rows[i].KindDropdown.value;
                result[i] = new NaturalSendKindLimitData {
                    Kind = kind,
                    MaxCountPerVolley = maxCount,
                    SelectionWeight = weight,
                    IgniteOnSpawn = rows[i].IgniteOnSpawn != null
                        && rows[i].IgniteOnSpawn.isOn
                        && DiceFirePolicy.CanCarryFire(kind)
                };
            }

            kinds = result;
            errorMessage = null;
            return true;
        }

        static bool TryReadKindsLenient(Bindings bindings, out List<NaturalSendKindLimitData> kinds) {
            kinds = new List<NaturalSendKindLimitData>();
            if (bindings?.KindRows == null) {
                return false;
            }

            for (var i = 0; i < bindings.KindRows.Count; i++) {
                var row = bindings.KindRows[i];
                TryParseInt(row.MaxCountPerVolley, out var maxCount);
                TryParseFloat(row.SelectionWeight, out var weight);
                if (maxCount <= 0) {
                    maxCount = 1;
                }

                if (weight <= 0f) {
                    weight = 1f;
                }

                var kind = row.KindDropdown != null ? (DiceKind)row.KindDropdown.value : DiceKind.Normal;
                kinds.Add(new NaturalSendKindLimitData {
                    Kind = kind,
                    MaxCountPerVolley = maxCount,
                    SelectionWeight = weight,
                    IgniteOnSpawn = row.IgniteOnSpawn != null
                        && row.IgniteOnSpawn.isOn
                        && DiceFirePolicy.CanCarryFire(kind)
                });
            }

            return true;
        }

        static List<NaturalSendKindLimitData> CloneKinds(NaturalSendKindLimitData[] source) {
            var result = new List<NaturalSendKindLimitData>();
            if (source == null) {
                return result;
            }

            for (var i = 0; i < source.Length; i++) {
                result.Add(source[i]);
            }

            return result;
        }

        static void SetInputText(TMP_InputField input, string value) {
            if (input == null) {
                return;
            }

            input.SetTextWithoutNotify(value);
        }

        static bool TryParseInt(TMP_InputField input, out int value) {
            value = 0;
            return input != null && int.TryParse(input.text, out value);
        }

        static bool TryParseFloat(TMP_InputField input, out float value) {
            value = 0f;
            return input != null && float.TryParse(input.text, out value);
        }
    }
}
