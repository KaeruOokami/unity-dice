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
        }

        public static Bindings Build(Transform parent, string sectionLabel, PlayerNaturalSendSettingsData template) {
            var section = LobbyUiFactory.CreateVerticalSection(parent, sectionLabel);
            LobbyUiFactory.CreateLayoutLabel(section, sectionLabel, 20, 28f);

            var bindings = new Bindings {
                SectionRoot = section,
                Enabled = LobbyUiFactory.CreateLabeledToggle(section, "Natural Send Enabled"),
                DiceCountPerVolley = LobbyUiFactory.CreateLabeledIntInput(section, "Dice Count / Volley"),
                ListRoot = LobbyUiFactory.CreateVerticalSection(section, "SendableKinds")
            };

            LobbyUiFactory.CreateLayoutButton(section, "AddKindButton", "Add Kind", 36f, () => {
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
            LobbyUiFactory.ClearChildren(bindings.ListRoot);
            bindings.KindRows.Clear();

            var kindLabels = LobbyUiFactory.GetDiceKindOptionLabels();
            for (var i = 0; i < kinds.Count; i++) {
                var index = i;
                var kindSection = LobbyUiFactory.CreateVerticalSection(bindings.ListRoot, $"Kind_{i}");
                LobbyUiFactory.CreateLayoutLabel(kindSection, $"Kind {i + 1}", 18, 24f);

                LobbyUiFactory.CreateLayoutLabel(kindSection, "Kind", 18, 24f);
                var kindDropdown = LobbyUiFactory.CreateLayoutDropdown(
                    kindSection,
                    "KindDropdown",
                    kindLabels,
                    40f);
                kindDropdown.value = (int)kinds[i].Kind;
                kindDropdown.RefreshShownValue();

                var maxField = LobbyUiFactory.CreateLabeledIntInput(kindSection, "Max / Volley");
                SetInputText(maxField, kinds[i].MaxCountPerVolley.ToString());
                var weightField = LobbyUiFactory.CreateLabeledFloatInput(kindSection, "Weight");
                SetInputText(weightField, kinds[i].SelectionWeight.ToString("0.###"));

                LobbyUiFactory.CreateLayoutButton(kindSection, "RemoveKindButton", "Remove Kind", 36f, () => {
                    RemoveKind(bindings, index);
                });

                bindings.KindRows.Add(new KindRow {
                    KindDropdown = kindDropdown,
                    MaxCountPerVolley = maxField,
                    SelectionWeight = weightField
                });
            }

            LobbyUiFactory.ForceRebuildLayout(bindings.SectionRoot);
            if (bindings.SectionRoot.parent is RectTransform parentRect) {
                LobbyUiFactory.ForceRebuildLayout(parentRect);
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

                result[i] = new NaturalSendKindLimitData {
                    Kind = (DiceKind)rows[i].KindDropdown.value,
                    MaxCountPerVolley = maxCount,
                    SelectionWeight = weight
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

                kinds.Add(new NaturalSendKindLimitData {
                    Kind = row.KindDropdown != null ? (DiceKind)row.KindDropdown.value : DiceKind.Normal,
                    MaxCountPerVolley = maxCount,
                    SelectionWeight = weight
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
