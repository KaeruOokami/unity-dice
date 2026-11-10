using DiceGame.Config;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceGame.Session
{
    sealed class JumboDiceSettingsPanelUi
    {
        public sealed class Bindings
        {
            public Toggle Enabled;
            public TMP_InputField SequenceStartFace;
            public TMP_InputField SequenceEndFace;
            public TMP_InputField MaxPerBoard;
        }

        public static Bindings Build(Transform parent, string sectionLabel) {
            var section = SessionUiFactory.CreateVerticalSection(parent, sectionLabel);
            SessionUiFactory.CreateLayoutLabel(section, sectionLabel, 20, 28f);
            var enabledRow = SessionUiFactory.CreateTwoColumnRow(section, "JumboEnabledRow");
            var enabled = SessionUiFactory.CreateLabeledToggle(enabledRow, "Jumbo Enabled");
            var sequenceRow = SessionUiFactory.CreateTwoColumnRow(section, "JumboSequenceRow");
            var sequenceStart = SessionUiFactory.CreateLabeledIntInput(sequenceRow, "Sequence Start Face");
            var sequenceEnd = SessionUiFactory.CreateLabeledIntInput(sequenceRow, "Sequence End Face");
            var maxRow = SessionUiFactory.CreateTwoColumnRow(section, "JumboMaxRow");
            var maxPerBoard = SessionUiFactory.CreateLabeledIntInput(maxRow, "Max Jumbo / Board");
            return new Bindings {
                Enabled = enabled,
                SequenceStartFace = sequenceStart,
                SequenceEndFace = sequenceEnd,
                MaxPerBoard = maxPerBoard
            };
        }

        public static void Apply(Bindings bindings, JumboDiceSettingsData data) {
            if (bindings == null) {
                return;
            }

            bindings.Enabled.isOn = data.Enabled;
            SetInputText(bindings.SequenceStartFace, data.SequenceStartFace.ToString());
            SetInputText(bindings.SequenceEndFace, data.SequenceEndFace.ToString());
            SetInputText(bindings.MaxPerBoard, data.MaxPerBoard.ToString());
        }

        public static bool TryRead(Bindings bindings, out JumboDiceSettingsData data, out string errorMessage) {
            data = default;
            if (bindings == null) {
                errorMessage = "Jumbo dice settings UI is not initialized.";
                return false;
            }

            if (!TryParseInt(bindings.SequenceStartFace, out var startFace)
                || !TryParseInt(bindings.SequenceEndFace, out var endFace)
                || !TryParseInt(bindings.MaxPerBoard, out var maxPerBoard)) {
                errorMessage = "Jumbo dice settings contain invalid integers.";
                return false;
            }

            data = new JumboDiceSettingsData {
                Enabled = bindings.Enabled.isOn,
                SequenceStartFace = startFace,
                SequenceEndFace = endFace,
                MaxPerBoard = maxPerBoard
            };

            if (!data.TryValidate(out errorMessage)) {
                data = default;
                return false;
            }

            errorMessage = null;
            return true;
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
    }
}
