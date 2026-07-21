using System.Collections.Generic;
using DiceGame.Config;
using DiceGame.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceGame.Session
{
    sealed class PlayerAttackSettingsPanelUi
    {
        const int MinTriggerFace = 1;
        const int MaxTriggerFace = 6;

        public sealed class Bindings
        {
            public RectTransform SectionRoot;
            public Transform ProfilesRoot;
            public List<ProfileRow> ProfileRows = new();
            public TMP_InputField AttackMultiplier;
            public TMP_InputField FaceGain;
            public TMP_InputField ChainGain;
            public TMP_InputField SizeGain;
            public TMP_InputField SnatchMultiplier;
            public TMP_InputField Face2Weight;
            public TMP_InputField Face3Weight;
            public TMP_InputField Face4Weight;
            public TMP_InputField Face5Weight;
            public TMP_InputField Face6Weight;
            public TMP_InputField QueueToBoardDelay;
        }

        public sealed class ProfileRow
        {
            public Toggle[] FaceToggles;
            public Transform KindsRoot;
            public List<KindRow> KindRows = new();
        }

        public sealed class KindRow
        {
            public TMP_Dropdown KindDropdown;
            public TMP_InputField MaxCountPerVolley;
            public TMP_InputField MinimumPower;
            public TMP_InputField SelectionWeight;
        }

        public static Bindings Build(Transform parent, string sectionLabel, PlayerAttackSettingsData template) {
            var section = LobbyUiFactory.CreateVerticalSection(parent, sectionLabel);
            LobbyUiFactory.CreateLayoutLabel(section, sectionLabel, 20, 28f);

            var bindings = new Bindings {
                SectionRoot = section,
                ProfilesRoot = LobbyUiFactory.CreateVerticalSection(section, "FaceProfiles")
            };

            LobbyUiFactory.CreateLayoutButton(section, "AddProfileButton", "Add Face Profile", 36f, () => {
                AddProfile(bindings);
            });

            LobbyUiFactory.CreateLayoutLabel(section, "Power", 20, 28f);
            bindings.AttackMultiplier = LobbyUiFactory.CreateLabeledFloatInput(section, "Attack Multiplier");
            bindings.FaceGain = LobbyUiFactory.CreateLabeledFloatInput(section, "Face Gain");
            bindings.ChainGain = LobbyUiFactory.CreateLabeledFloatInput(section, "Chain Gain");
            bindings.SizeGain = LobbyUiFactory.CreateLabeledFloatInput(section, "Size Gain");
            bindings.SnatchMultiplier = LobbyUiFactory.CreateLabeledFloatInput(section, "Snatch Multiplier");
            bindings.Face2Weight = LobbyUiFactory.CreateLabeledFloatInput(section, "Face 2 Weight");
            bindings.Face3Weight = LobbyUiFactory.CreateLabeledFloatInput(section, "Face 3 Weight");
            bindings.Face4Weight = LobbyUiFactory.CreateLabeledFloatInput(section, "Face 4 Weight");
            bindings.Face5Weight = LobbyUiFactory.CreateLabeledFloatInput(section, "Face 5 Weight");
            bindings.Face6Weight = LobbyUiFactory.CreateLabeledFloatInput(section, "Face 6 Weight");
            bindings.QueueToBoardDelay = LobbyUiFactory.CreateLabeledFloatInput(section, "Queue To Board Delay");

            RebuildProfiles(bindings, CloneProfiles(template.FaceSendProfiles));
            return bindings;
        }

        public static void Apply(Bindings bindings, PlayerAttackSettingsData data) {
            if (bindings == null) {
                return;
            }

            SetInputText(bindings.AttackMultiplier, data.AttackMultiplier.ToString("0.###"));
            SetInputText(bindings.FaceGain, data.FaceGain.ToString("0.###"));
            SetInputText(bindings.ChainGain, data.ChainGain.ToString("0.###"));
            SetInputText(bindings.SizeGain, data.SizeGain.ToString("0.###"));
            SetInputText(bindings.SnatchMultiplier, data.SnatchMultiplier.ToString("0.###"));
            SetInputText(bindings.Face2Weight, data.Face2Weight.ToString("0.###"));
            SetInputText(bindings.Face3Weight, data.Face3Weight.ToString("0.###"));
            SetInputText(bindings.Face4Weight, data.Face4Weight.ToString("0.###"));
            SetInputText(bindings.Face5Weight, data.Face5Weight.ToString("0.###"));
            SetInputText(bindings.Face6Weight, data.Face6Weight.ToString("0.###"));
            SetInputText(bindings.QueueToBoardDelay, data.QueueToBoardDelay.ToString("0.###"));
            RebuildProfiles(bindings, CloneProfiles(data.FaceSendProfiles));
        }

        public static bool TryRead(Bindings bindings, out PlayerAttackSettingsData data, out string errorMessage) {
            data = default;
            if (bindings == null || bindings.ProfileRows == null) {
                errorMessage = "Attack settings UI is not initialized.";
                return false;
            }

            if (!TryParseFloat(bindings.AttackMultiplier, out var attackMultiplier)
                || !TryParseFloat(bindings.FaceGain, out var faceGain)
                || !TryParseFloat(bindings.ChainGain, out var chainGain)
                || !TryParseFloat(bindings.SizeGain, out var sizeGain)
                || !TryParseFloat(bindings.SnatchMultiplier, out var snatchMultiplier)
                || !TryParseFloat(bindings.Face2Weight, out var face2)
                || !TryParseFloat(bindings.Face3Weight, out var face3)
                || !TryParseFloat(bindings.Face4Weight, out var face4)
                || !TryParseFloat(bindings.Face5Weight, out var face5)
                || !TryParseFloat(bindings.Face6Weight, out var face6)
                || !TryParseFloat(bindings.QueueToBoardDelay, out var queueDelay)) {
                errorMessage = "Attack settings contain invalid numbers.";
                return false;
            }

            if (!TryReadProfiles(bindings, out var profiles, out errorMessage)) {
                return false;
            }

            data = new PlayerAttackSettingsData {
                FaceSendProfiles = profiles,
                AttackMultiplier = attackMultiplier,
                FaceGain = faceGain,
                ChainGain = chainGain,
                SizeGain = sizeGain,
                SnatchMultiplier = snatchMultiplier,
                Face2Weight = face2,
                Face3Weight = face3,
                Face4Weight = face4,
                Face5Weight = face5,
                Face6Weight = face6,
                QueueToBoardDelay = queueDelay
            };

            if (!data.TryValidate(out errorMessage)) {
                data = default;
                return false;
            }

            errorMessage = null;
            return true;
        }

        static void AddProfile(Bindings bindings) {
            if (!TryReadProfilesLenient(bindings, out var profiles)) {
                profiles = new List<FaceAttackSendProfileData>();
            }

            profiles.Add(CreateDefaultProfile());
            RebuildProfiles(bindings, profiles);
        }

        static void RemoveProfile(Bindings bindings, int index) {
            if (!TryReadProfilesLenient(bindings, out var profiles)) {
                return;
            }

            if (index < 0 || index >= profiles.Count) {
                return;
            }

            profiles.RemoveAt(index);
            RebuildProfiles(bindings, profiles);
        }

        static void AddKind(Bindings bindings, int profileIndex) {
            if (!TryReadProfilesLenient(bindings, out var profiles)) {
                return;
            }

            if (profileIndex < 0 || profileIndex >= profiles.Count) {
                return;
            }

            var profile = profiles[profileIndex];
            var kinds = new List<SendableKindLimitData>(profile.SendableKinds ?? System.Array.Empty<SendableKindLimitData>());
            kinds.Add(CreateDefaultKind());
            profile.SendableKinds = kinds.ToArray();
            profiles[profileIndex] = profile;
            RebuildProfiles(bindings, profiles);
        }

        static void RemoveKind(Bindings bindings, int profileIndex, int kindIndex) {
            if (!TryReadProfilesLenient(bindings, out var profiles)) {
                return;
            }

            if (profileIndex < 0 || profileIndex >= profiles.Count) {
                return;
            }

            var profile = profiles[profileIndex];
            var kinds = new List<SendableKindLimitData>(profile.SendableKinds ?? System.Array.Empty<SendableKindLimitData>());
            if (kindIndex < 0 || kindIndex >= kinds.Count) {
                return;
            }

            kinds.RemoveAt(kindIndex);
            profile.SendableKinds = kinds.ToArray();
            profiles[profileIndex] = profile;
            RebuildProfiles(bindings, profiles);
        }

        static void RebuildProfiles(Bindings bindings, List<FaceAttackSendProfileData> profiles) {
            LobbyUiFactory.ClearChildren(bindings.ProfilesRoot);
            bindings.ProfileRows.Clear();

            var kindLabels = LobbyUiFactory.GetDiceKindOptionLabels();
            for (var i = 0; i < profiles.Count; i++) {
                var profileIndex = i;
                var profile = profiles[i];
                var profileSection = LobbyUiFactory.CreateVerticalSection(bindings.ProfilesRoot, $"Profile_{i}");
                LobbyUiFactory.CreateLayoutLabel(profileSection, $"Face Profile {i + 1}", 18, 24f);

                LobbyUiFactory.CreateLayoutLabel(profileSection, "Trigger Faces", 18, 24f);
                var faceToggles = CreateFaceToggles(profileSection, profile.TriggerFaces);

                var kindsRoot = LobbyUiFactory.CreateVerticalSection(profileSection, "Kinds");
                var kindRows = new List<KindRow>();
                var kinds = profile.SendableKinds ?? System.Array.Empty<SendableKindLimitData>();
                for (var j = 0; j < kinds.Length; j++) {
                    var kindIndex = j;
                    var kindSection = LobbyUiFactory.CreateVerticalSection(kindsRoot, $"Kind_{j}");
                    LobbyUiFactory.CreateLayoutLabel(kindSection, $"Kind {j + 1}", 18, 24f);
                    LobbyUiFactory.CreateLayoutLabel(kindSection, "Kind", 18, 24f);
                    var kindDropdown = LobbyUiFactory.CreateLayoutDropdown(
                        kindSection,
                        "KindDropdown",
                        kindLabels,
                        40f);
                    kindDropdown.value = (int)kinds[j].Kind;
                    kindDropdown.RefreshShownValue();

                    var maxField = LobbyUiFactory.CreateLabeledIntInput(kindSection, "Max / Volley");
                    SetInputText(maxField, kinds[j].MaxCountPerVolley.ToString());
                    var minPowerField = LobbyUiFactory.CreateLabeledFloatInput(kindSection, "Min Power");
                    SetInputText(minPowerField, kinds[j].MinimumPower.ToString("0.###"));
                    var weightField = LobbyUiFactory.CreateLabeledFloatInput(kindSection, "Weight");
                    SetInputText(weightField, kinds[j].SelectionWeight.ToString("0.###"));

                    LobbyUiFactory.CreateLayoutButton(kindSection, "RemoveKindButton", "Remove Kind", 36f, () => {
                        RemoveKind(bindings, profileIndex, kindIndex);
                    });

                    kindRows.Add(new KindRow {
                        KindDropdown = kindDropdown,
                        MaxCountPerVolley = maxField,
                        MinimumPower = minPowerField,
                        SelectionWeight = weightField
                    });
                }

                LobbyUiFactory.CreateLayoutButton(profileSection, "AddKindButton", "Add Kind", 36f, () => {
                    AddKind(bindings, profileIndex);
                });
                LobbyUiFactory.CreateLayoutButton(profileSection, "RemoveProfileButton", "Remove Profile", 36f, () => {
                    RemoveProfile(bindings, profileIndex);
                });

                bindings.ProfileRows.Add(new ProfileRow {
                    FaceToggles = faceToggles,
                    KindsRoot = kindsRoot,
                    KindRows = kindRows
                });
            }

            LobbyUiFactory.ForceRebuildLayout(bindings.SectionRoot);
            if (bindings.SectionRoot.parent is RectTransform parentRect) {
                LobbyUiFactory.ForceRebuildLayout(parentRect);
            }
        }

        static Toggle[] CreateFaceToggles(Transform parent, int[] triggerFaces) {
            var faceCount = MaxTriggerFace - MinTriggerFace + 1;
            var toggles = new Toggle[faceCount];
            var selected = new bool[faceCount];
            if (triggerFaces != null) {
                for (var i = 0; i < triggerFaces.Length; i++) {
                    var face = triggerFaces[i];
                    if (face >= MinTriggerFace && face <= MaxTriggerFace) {
                        selected[face - MinTriggerFace] = true;
                    }
                }
            }

            for (var face = MinTriggerFace; face <= MaxTriggerFace; face++) {
                var toggle = LobbyUiFactory.CreateLabeledToggle(parent, $"Face {face}");
                toggle.isOn = selected[face - MinTriggerFace];
                toggles[face - MinTriggerFace] = toggle;
            }

            return toggles;
        }

        static bool TryReadProfiles(
            Bindings bindings,
            out FaceAttackSendProfileData[] profiles,
            out string errorMessage) {
            profiles = null;
            var rows = bindings.ProfileRows;
            var result = new FaceAttackSendProfileData[rows.Count];
            for (var i = 0; i < rows.Count; i++) {
                if (!TryReadProfile(rows[i], i, out result[i], out errorMessage)) {
                    return false;
                }
            }

            profiles = result;
            errorMessage = null;
            return true;
        }

        static bool TryReadProfile(
            ProfileRow row,
            int profileIndex,
            out FaceAttackSendProfileData profile,
            out string errorMessage) {
            profile = default;
            var faces = ReadSelectedFaces(row.FaceToggles);
            var kinds = new SendableKindLimitData[row.KindRows.Count];
            for (var j = 0; j < row.KindRows.Count; j++) {
                var kindRow = row.KindRows[j];
                if (!TryParseInt(kindRow.MaxCountPerVolley, out var maxCount)
                    || !TryParseFloat(kindRow.MinimumPower, out var minPower)
                    || !TryParseFloat(kindRow.SelectionWeight, out var weight)) {
                    errorMessage = $"Attack profile {profileIndex + 1} kind {j + 1} has invalid values.";
                    return false;
                }

                kinds[j] = new SendableKindLimitData {
                    Kind = (DiceKind)kindRow.KindDropdown.value,
                    MaxCountPerVolley = maxCount,
                    MinimumPower = minPower,
                    SelectionWeight = weight
                };
            }

            profile = new FaceAttackSendProfileData {
                TriggerFaces = faces,
                SendableKinds = kinds
            };
            errorMessage = null;
            return true;
        }

        static bool TryReadProfilesLenient(Bindings bindings, out List<FaceAttackSendProfileData> profiles) {
            profiles = new List<FaceAttackSendProfileData>();
            if (bindings?.ProfileRows == null) {
                return false;
            }

            for (var i = 0; i < bindings.ProfileRows.Count; i++) {
                var row = bindings.ProfileRows[i];
                var kinds = new List<SendableKindLimitData>();
                for (var j = 0; j < row.KindRows.Count; j++) {
                    var kindRow = row.KindRows[j];
                    TryParseInt(kindRow.MaxCountPerVolley, out var maxCount);
                    TryParseFloat(kindRow.MinimumPower, out var minPower);
                    TryParseFloat(kindRow.SelectionWeight, out var weight);
                    if (maxCount <= 0) {
                        maxCount = 1;
                    }

                    if (weight <= 0f) {
                        weight = 1f;
                    }

                    kinds.Add(new SendableKindLimitData {
                        Kind = kindRow.KindDropdown != null
                            ? (DiceKind)kindRow.KindDropdown.value
                            : DiceKind.Normal,
                        MaxCountPerVolley = maxCount,
                        MinimumPower = minPower,
                        SelectionWeight = weight
                    });
                }

                profiles.Add(new FaceAttackSendProfileData {
                    TriggerFaces = ReadSelectedFaces(row.FaceToggles),
                    SendableKinds = kinds.ToArray()
                });
            }

            return true;
        }

        static int[] ReadSelectedFaces(Toggle[] faceToggles) {
            if (faceToggles == null) {
                return System.Array.Empty<int>();
            }

            var faces = new List<int>(faceToggles.Length);
            for (var i = 0; i < faceToggles.Length; i++) {
                if (faceToggles[i] != null && faceToggles[i].isOn) {
                    faces.Add(i + MinTriggerFace);
                }
            }

            return faces.ToArray();
        }

        static List<FaceAttackSendProfileData> CloneProfiles(FaceAttackSendProfileData[] source) {
            var result = new List<FaceAttackSendProfileData>();
            if (source == null) {
                return result;
            }

            for (var i = 0; i < source.Length; i++) {
                var faces = source[i].TriggerFaces ?? System.Array.Empty<int>();
                var copiedFaces = new int[faces.Length];
                System.Array.Copy(faces, copiedFaces, faces.Length);
                var kinds = source[i].SendableKinds ?? System.Array.Empty<SendableKindLimitData>();
                var copiedKinds = new SendableKindLimitData[kinds.Length];
                System.Array.Copy(kinds, copiedKinds, kinds.Length);
                result.Add(new FaceAttackSendProfileData {
                    TriggerFaces = copiedFaces,
                    SendableKinds = copiedKinds
                });
            }

            return result;
        }

        static FaceAttackSendProfileData CreateDefaultProfile() {
            return new FaceAttackSendProfileData {
                TriggerFaces = new[] { 1 },
                SendableKinds = new[] { CreateDefaultKind() }
            };
        }

        static SendableKindLimitData CreateDefaultKind() {
            return new SendableKindLimitData {
                Kind = DiceKind.Normal,
                MaxCountPerVolley = 1,
                MinimumPower = 0f,
                SelectionWeight = 1f
            };
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
