using DiceGame.Config;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceGame.Session
{
    sealed class DiceSpawnSettingsPanelUi
    {
        public sealed class Bindings
        {
            public TMP_InputField InitialDiceCount;
            public Toggle AnimateInitialDiceSpawn;
            public Toggle ContinuousSpawnEnabled;
            public TMP_InputField SpawnInterval;
            public TMP_InputField SpawnIntervalJitter;
            public Slider BottomSpawnWeight;
            public TextMeshProUGUI BottomSpawnWeightLabel;
        }

        public static Bindings Build(Transform parent, string sectionLabel) {
            var section = LobbyUiFactory.CreateVerticalSection(parent, sectionLabel);
            LobbyUiFactory.CreateLayoutLabel(section, sectionLabel, 20, 28f);
            var bindings = new Bindings {
                InitialDiceCount = LobbyUiFactory.CreateLabeledIntInput(section, "Initial Dice Count"),
                AnimateInitialDiceSpawn = LobbyUiFactory.CreateLabeledToggle(section, "Animate Initial Spawn"),
                ContinuousSpawnEnabled = LobbyUiFactory.CreateLabeledToggle(section, "Continuous Spawn Enabled"),
                SpawnInterval = LobbyUiFactory.CreateLabeledFloatInput(section, "Spawn Interval"),
                SpawnIntervalJitter = LobbyUiFactory.CreateLabeledFloatInput(section, "Spawn Interval Jitter")
            };

            LobbyUiFactory.CreateLayoutLabel(section, "Bottom Spawn Weight", 18, 24f);
            bindings.BottomSpawnWeight = LobbyUiFactory.CreateLayoutSlider(
                section,
                "BottomSpawnWeightSlider",
                36f,
                out var weightLabel);
            bindings.BottomSpawnWeightLabel = weightLabel;
            return bindings;
        }

        public static void Apply(Bindings bindings, DiceSpawnSettingsData data) {
            if (bindings == null) {
                return;
            }

            SetInputText(bindings.InitialDiceCount, data.InitialDiceCount.ToString());
            bindings.AnimateInitialDiceSpawn.isOn = data.AnimateInitialDiceSpawn;
            bindings.ContinuousSpawnEnabled.isOn = data.ContinuousSpawnEnabled;
            SetInputText(bindings.SpawnInterval, data.SpawnInterval.ToString("0.###"));
            SetInputText(bindings.SpawnIntervalJitter, data.SpawnIntervalJitter.ToString("0.###"));
            bindings.BottomSpawnWeight.value = data.BottomSpawnWeight;
            if (bindings.BottomSpawnWeightLabel != null) {
                bindings.BottomSpawnWeightLabel.text = data.BottomSpawnWeight.ToString("0.00");
            }
        }

        public static bool TryRead(Bindings bindings, out DiceSpawnSettingsData data, out string errorMessage) {
            data = default;
            if (bindings == null) {
                errorMessage = "Dice spawn settings UI is not initialized.";
                return false;
            }

            if (!TryParseInt(bindings.InitialDiceCount, out data.InitialDiceCount)) {
                errorMessage = "Initial Dice Count must be an integer.";
                return false;
            }

            data.AnimateInitialDiceSpawn = bindings.AnimateInitialDiceSpawn.isOn;
            data.ContinuousSpawnEnabled = bindings.ContinuousSpawnEnabled.isOn;

            if (!TryParseFloat(bindings.SpawnInterval, out data.SpawnInterval)) {
                errorMessage = "Spawn Interval must be a number.";
                return false;
            }

            if (!TryParseFloat(bindings.SpawnIntervalJitter, out data.SpawnIntervalJitter)) {
                errorMessage = "Spawn Interval Jitter must be a number.";
                return false;
            }

            data.BottomSpawnWeight = bindings.BottomSpawnWeight.value;
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

        static bool TryParseFloat(TMP_InputField input, out float value) {
            value = 0f;
            return input != null && float.TryParse(input.text, out value);
        }
    }
}
