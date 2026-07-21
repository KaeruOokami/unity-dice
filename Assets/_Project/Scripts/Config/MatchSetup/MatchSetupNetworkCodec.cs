using DiceGame.Core;
using DiceGame.Session.Network;

namespace DiceGame.Config
{
    public static class MatchSetupNetworkCodec
    {
        public static MatchSetupNetworkPayload ToPayload(
            MatchSetupSnapshot snapshot,
            MatchSetupPresetRegistry registry) {
            _ = registry;
            return new MatchSetupNetworkPayload {
                GameMode = (byte)snapshot.GameMode,
                SharedSpawn = ToSpawnPayload(snapshot.SharedSpawn),
                SharedCatalog = ToCatalogPayload(snapshot.SharedCatalog),
                Player1IsAi = snapshot.Player1.IsAi ? (byte)1 : (byte)0,
                Player1DeviceKind = (byte)snapshot.Player1.InputConfig.DeviceKind,
                Player1GamepadIndex = (byte)snapshot.Player1.InputConfig.GamepadIndex,
                Player1Spawn = ToSpawnPayload(snapshot.Player1.Spawn),
                Player1Catalog = ToCatalogPayload(snapshot.Player1.Catalog),
                Player1Attack = ToAttackPayload(snapshot.Player1.Attack),
                Player1NaturalSend = ToNaturalSendPayload(snapshot.Player1.NaturalSend),
                Player2IsAi = snapshot.Player2.IsAi ? (byte)1 : (byte)0,
                Player2DeviceKind = (byte)snapshot.Player2.InputConfig.DeviceKind,
                Player2GamepadIndex = (byte)snapshot.Player2.InputConfig.GamepadIndex,
                Player2Spawn = ToSpawnPayload(snapshot.Player2.Spawn),
                Player2Catalog = ToCatalogPayload(snapshot.Player2.Catalog),
                Player2Attack = ToAttackPayload(snapshot.Player2.Attack),
                Player2NaturalSend = ToNaturalSendPayload(snapshot.Player2.NaturalSend)
            };
        }

        public static bool TryFromPayload(
            MatchSetupNetworkPayload payload,
            MatchSetupPresetRegistry registry,
            out MatchSetupSnapshot snapshot,
            out string errorMessage) {
            snapshot = null;
            if (registry == null) {
                errorMessage = "MatchSetupNetworkCodec: Preset registry is not assigned.";
                return false;
            }

            if (!System.Enum.IsDefined(typeof(GameMode), (int)payload.GameMode)) {
                errorMessage = "MatchSetupNetworkCodec: Unknown game mode.";
                return false;
            }

            var mode = (GameMode)payload.GameMode;
            var defaults = registry.CreateDefaultSnapshot(mode);
            snapshot = new MatchSetupSnapshot {
                GameMode = mode,
                SharedSpawn = FromSpawnPayload(payload.SharedSpawn),
                SharedCatalog = FromCatalogPayload(payload.SharedCatalog, defaults.SharedCatalog),
                Player1 = BuildPlayerSetup(
                    payload.Player1IsAi,
                    payload.Player1DeviceKind,
                    payload.Player1GamepadIndex,
                    payload.Player1Spawn,
                    payload.Player1Catalog,
                    payload.Player1Attack,
                    payload.Player1NaturalSend,
                    defaults.Player1.Catalog),
                Player2 = BuildPlayerSetup(
                    payload.Player2IsAi,
                    payload.Player2DeviceKind,
                    payload.Player2GamepadIndex,
                    payload.Player2Spawn,
                    payload.Player2Catalog,
                    payload.Player2Attack,
                    payload.Player2NaturalSend,
                    defaults.Player2.Catalog)
            };

            if (!snapshot.TryValidate(registry, out errorMessage)) {
                snapshot = null;
                return false;
            }

            errorMessage = null;
            return true;
        }

        static PlayerSlotSetup BuildPlayerSetup(
            byte isAi,
            byte deviceKind,
            byte gamepadIndex,
            DiceSpawnSettingsNetworkPayload spawnPayload,
            DiceCatalogNetworkPayload catalogPayload,
            PlayerAttackSettingsNetworkPayload attackPayload,
            PlayerNaturalSendSettingsNetworkPayload naturalSendPayload,
            DiceCatalogData meshSource) {
            return PlayerSlotSetup.CreateDefault(
                isAi != 0,
                new PlayerSlotInputConfig((PlayerInputDeviceKind)deviceKind, gamepadIndex),
                FromSpawnPayload(spawnPayload),
                FromCatalogPayload(catalogPayload, meshSource),
                FromAttackPayload(attackPayload),
                FromNaturalSendPayload(naturalSendPayload));
        }

        static DiceSpawnSettingsNetworkPayload ToSpawnPayload(DiceSpawnSettingsData data) {
            return new DiceSpawnSettingsNetworkPayload {
                InitialDiceCount = data.InitialDiceCount,
                AnimateInitialDiceSpawn = data.AnimateInitialDiceSpawn,
                ContinuousSpawnEnabled = data.ContinuousSpawnEnabled,
                SpawnInterval = data.SpawnInterval,
                SpawnIntervalJitter = data.SpawnIntervalJitter,
                BottomSpawnWeight = data.BottomSpawnWeight
            };
        }

        static DiceSpawnSettingsData FromSpawnPayload(DiceSpawnSettingsNetworkPayload payload) {
            return new DiceSpawnSettingsData {
                InitialDiceCount = payload.InitialDiceCount,
                AnimateInitialDiceSpawn = payload.AnimateInitialDiceSpawn,
                ContinuousSpawnEnabled = payload.ContinuousSpawnEnabled,
                SpawnInterval = payload.SpawnInterval,
                SpawnIntervalJitter = payload.SpawnIntervalJitter,
                BottomSpawnWeight = payload.BottomSpawnWeight
            };
        }

        static DiceCatalogNetworkPayload ToCatalogPayload(DiceCatalogData data) {
            var source = data.Entries ?? System.Array.Empty<DiceCatalogEntryData>();
            var entries = new DiceCatalogEntryNetworkPayload[source.Length];
            for (var i = 0; i < source.Length; i++) {
                entries[i] = new DiceCatalogEntryNetworkPayload {
                    Kind = (byte)source[i].Kind,
                    SpawnWeight = source[i].SpawnWeight
                };
            }

            return new DiceCatalogNetworkPayload { Entries = entries };
        }

        static DiceCatalogData FromCatalogPayload(DiceCatalogNetworkPayload payload, DiceCatalogData meshSource) {
            var source = payload.Entries ?? System.Array.Empty<DiceCatalogEntryNetworkPayload>();
            var entries = new DiceCatalogEntryData[source.Length];
            for (var i = 0; i < source.Length; i++) {
                var kind = (DiceKind)source[i].Kind;
                entries[i] = new DiceCatalogEntryData {
                    Kind = kind,
                    MeshPrefab = FindMesh(meshSource, kind),
                    SpawnWeight = source[i].SpawnWeight
                };
            }

            return new DiceCatalogData { Entries = entries };
        }

        static PlayerAttackSettingsNetworkPayload ToAttackPayload(PlayerAttackSettingsData data) {
            var source = data.FaceSendProfiles ?? System.Array.Empty<FaceAttackSendProfileData>();
            var profiles = new FaceAttackSendProfileNetworkPayload[source.Length];
            for (var i = 0; i < source.Length; i++) {
                var kindsSource = source[i].SendableKinds ?? System.Array.Empty<SendableKindLimitData>();
                var kinds = new SendableKindLimitNetworkPayload[kindsSource.Length];
                for (var j = 0; j < kindsSource.Length; j++) {
                    kinds[j] = new SendableKindLimitNetworkPayload {
                        Kind = (byte)kindsSource[j].Kind,
                        MaxCountPerVolley = kindsSource[j].MaxCountPerVolley,
                        MinimumPower = kindsSource[j].MinimumPower,
                        SelectionWeight = kindsSource[j].SelectionWeight
                    };
                }

                var faces = source[i].TriggerFaces ?? System.Array.Empty<int>();
                var copiedFaces = new int[faces.Length];
                System.Array.Copy(faces, copiedFaces, faces.Length);
                profiles[i] = new FaceAttackSendProfileNetworkPayload {
                    TriggerFaces = copiedFaces,
                    SendableKinds = kinds
                };
            }

            return new PlayerAttackSettingsNetworkPayload {
                FaceSendProfiles = profiles,
                AttackMultiplier = data.AttackMultiplier,
                FaceGain = data.FaceGain,
                ChainGain = data.ChainGain,
                SizeGain = data.SizeGain,
                SnatchMultiplier = data.SnatchMultiplier,
                Face2Weight = data.Face2Weight,
                Face3Weight = data.Face3Weight,
                Face4Weight = data.Face4Weight,
                Face5Weight = data.Face5Weight,
                Face6Weight = data.Face6Weight,
                QueueToBoardDelay = data.QueueToBoardDelay
            };
        }

        static PlayerAttackSettingsData FromAttackPayload(PlayerAttackSettingsNetworkPayload payload) {
            var source = payload.FaceSendProfiles ?? System.Array.Empty<FaceAttackSendProfileNetworkPayload>();
            var profiles = new FaceAttackSendProfileData[source.Length];
            for (var i = 0; i < source.Length; i++) {
                var kindsSource = source[i].SendableKinds ?? System.Array.Empty<SendableKindLimitNetworkPayload>();
                var kinds = new SendableKindLimitData[kindsSource.Length];
                for (var j = 0; j < kindsSource.Length; j++) {
                    kinds[j] = new SendableKindLimitData {
                        Kind = (DiceKind)kindsSource[j].Kind,
                        MaxCountPerVolley = kindsSource[j].MaxCountPerVolley,
                        MinimumPower = kindsSource[j].MinimumPower,
                        SelectionWeight = kindsSource[j].SelectionWeight
                    };
                }

                var faces = source[i].TriggerFaces ?? System.Array.Empty<int>();
                var copiedFaces = new int[faces.Length];
                System.Array.Copy(faces, copiedFaces, faces.Length);
                profiles[i] = new FaceAttackSendProfileData {
                    TriggerFaces = copiedFaces,
                    SendableKinds = kinds
                };
            }

            return new PlayerAttackSettingsData {
                FaceSendProfiles = profiles,
                AttackMultiplier = payload.AttackMultiplier,
                FaceGain = payload.FaceGain,
                ChainGain = payload.ChainGain,
                SizeGain = payload.SizeGain,
                SnatchMultiplier = payload.SnatchMultiplier,
                Face2Weight = payload.Face2Weight,
                Face3Weight = payload.Face3Weight,
                Face4Weight = payload.Face4Weight,
                Face5Weight = payload.Face5Weight,
                Face6Weight = payload.Face6Weight,
                QueueToBoardDelay = payload.QueueToBoardDelay
            };
        }

        static PlayerNaturalSendSettingsNetworkPayload ToNaturalSendPayload(PlayerNaturalSendSettingsData data) {
            var source = data.SendableKinds ?? System.Array.Empty<NaturalSendKindLimitData>();
            var kinds = new NaturalSendKindLimitNetworkPayload[source.Length];
            for (var i = 0; i < source.Length; i++) {
                kinds[i] = new NaturalSendKindLimitNetworkPayload {
                    Kind = (byte)source[i].Kind,
                    MaxCountPerVolley = source[i].MaxCountPerVolley,
                    SelectionWeight = source[i].SelectionWeight
                };
            }

            return new PlayerNaturalSendSettingsNetworkPayload {
                Enabled = data.Enabled,
                DiceCountPerVolley = data.DiceCountPerVolley,
                SendableKinds = kinds
            };
        }

        static PlayerNaturalSendSettingsData FromNaturalSendPayload(PlayerNaturalSendSettingsNetworkPayload payload) {
            var source = payload.SendableKinds ?? System.Array.Empty<NaturalSendKindLimitNetworkPayload>();
            var kinds = new NaturalSendKindLimitData[source.Length];
            for (var i = 0; i < source.Length; i++) {
                kinds[i] = new NaturalSendKindLimitData {
                    Kind = (DiceKind)source[i].Kind,
                    MaxCountPerVolley = source[i].MaxCountPerVolley,
                    SelectionWeight = source[i].SelectionWeight
                };
            }

            return new PlayerNaturalSendSettingsData {
                Enabled = payload.Enabled,
                DiceCountPerVolley = payload.DiceCountPerVolley,
                SendableKinds = kinds
            };
        }

        static UnityEngine.GameObject FindMesh(DiceCatalogData meshSource, DiceKind kind) {
            if (meshSource.Entries == null) {
                return null;
            }

            for (var i = 0; i < meshSource.Entries.Length; i++) {
                if (meshSource.Entries[i].Kind == kind) {
                    return meshSource.Entries[i].MeshPrefab;
                }
            }

            return null;
        }
    }
}
