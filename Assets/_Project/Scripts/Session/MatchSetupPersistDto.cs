using System;
using DiceGame.Config;
using DiceGame.Session.Network;

namespace DiceGame.Session
{
    [Serializable]
    public sealed class MatchSetupPersistFile
    {
        public int Version = 2;
        public byte GameMode;
        public DiceSpawnSettingsPersistDto SharedSpawn = new();
        public DiceCatalogPersistDto SharedCatalog = new();
        public JumboDiceSettingsPersistDto Jumbo = new();
        public PlayerSlotPersistDto Player1 = new();
        public PlayerSlotPersistDto Player2 = new();
    }

    [Serializable]
    public sealed class PlayerSlotPersistDto
    {
        public bool IsAi;
        public byte DeviceKind;
        public int GamepadIndex;
        public DiceSpawnSettingsPersistDto Spawn = new();
        public DiceCatalogPersistDto Catalog = new();
        public PlayerAttackSettingsPersistDto Attack = new();
        public PlayerNaturalSendSettingsPersistDto NaturalSend = new();
    }

    [Serializable]
    public sealed class DiceSpawnSettingsPersistDto
    {
        public int InitialDiceCount;
        public bool AnimateInitialDiceSpawn;
        public bool ContinuousSpawnEnabled;
        public float SpawnInterval;
        public float SpawnIntervalJitter;
        public float BottomSpawnWeight;
    }

    [Serializable]
    public sealed class DiceCatalogEntryPersistDto
    {
        public byte Kind;
        public float SpawnWeight;
    }

    [Serializable]
    public sealed class DiceCatalogPersistDto
    {
        public DiceCatalogEntryPersistDto[] Entries = Array.Empty<DiceCatalogEntryPersistDto>();
    }

    [Serializable]
    public sealed class SendableKindLimitPersistDto
    {
        public byte Kind;
        public int MaxCountPerVolley;
        public float MinimumPower;
        public float SelectionWeight;
    }

    [Serializable]
    public sealed class FaceAttackSendProfilePersistDto
    {
        public int[] TriggerFaces = Array.Empty<int>();
        public SendableKindLimitPersistDto[] SendableKinds = Array.Empty<SendableKindLimitPersistDto>();
    }

    [Serializable]
    public sealed class PlayerAttackSettingsPersistDto
    {
        public FaceAttackSendProfilePersistDto[] FaceSendProfiles = Array.Empty<FaceAttackSendProfilePersistDto>();
        public float AttackMultiplier;
        public float FaceGain;
        public float ChainGain;
        public float SizeGain;
        public float SnatchMultiplier;
        public float Face2Weight;
        public float Face3Weight;
        public float Face4Weight;
        public float Face5Weight;
        public float Face6Weight;
        public float QueueToBoardDelay;
    }

    [Serializable]
    public sealed class NaturalSendKindLimitPersistDto
    {
        public byte Kind;
        public int MaxCountPerVolley;
        public float SelectionWeight;
    }

    [Serializable]
    public sealed class PlayerNaturalSendSettingsPersistDto
    {
        public bool Enabled;
        public int DiceCountPerVolley;
        public NaturalSendKindLimitPersistDto[] SendableKinds = Array.Empty<NaturalSendKindLimitPersistDto>();
    }

    [Serializable]
    public sealed class JumboDiceSettingsPersistDto
    {
        public bool Enabled;
        public int SequenceStartFace;
        public int SequenceEndFace;
        public int MaxPerBoard;
    }

    public static class MatchSetupPersistMapper
    {
        public static MatchSetupPersistFile FromPayload(MatchSetupPayload payload) {
            return new MatchSetupPersistFile {
                Version = MatchSetupPersistence.CurrentVersion,
                GameMode = payload.GameMode,
                SharedSpawn = FromSpawn(payload.SharedSpawn),
                SharedCatalog = FromCatalog(payload.SharedCatalog),
                Jumbo = FromJumbo(payload.Jumbo),
                Player1 = FromPlayer(
                    payload.Player1IsAi,
                    payload.Player1DeviceKind,
                    payload.Player1GamepadIndex,
                    payload.Player1Spawn,
                    payload.Player1Catalog,
                    payload.Player1Attack,
                    payload.Player1NaturalSend),
                Player2 = FromPlayer(
                    payload.Player2IsAi,
                    payload.Player2DeviceKind,
                    payload.Player2GamepadIndex,
                    payload.Player2Spawn,
                    payload.Player2Catalog,
                    payload.Player2Attack,
                    payload.Player2NaturalSend)
            };
        }

        public static MatchSetupPayload ToPayload(MatchSetupPersistFile file) {
            var player1 = file.Player1 ?? new PlayerSlotPersistDto();
            var player2 = file.Player2 ?? new PlayerSlotPersistDto();
            return new MatchSetupPayload {
                GameMode = file.GameMode,
                SharedSpawn = ToSpawn(file.SharedSpawn),
                SharedCatalog = ToCatalog(file.SharedCatalog),
                Jumbo = ToJumbo(file.Jumbo),
                Player1IsAi = player1.IsAi ? (byte)1 : (byte)0,
                Player1DeviceKind = player1.DeviceKind,
                Player1GamepadIndex = (byte)player1.GamepadIndex,
                Player1Spawn = ToSpawn(player1.Spawn),
                Player1Catalog = ToCatalog(player1.Catalog),
                Player1Attack = ToAttack(player1.Attack),
                Player1NaturalSend = ToNaturalSend(player1.NaturalSend),
                Player2IsAi = player2.IsAi ? (byte)1 : (byte)0,
                Player2DeviceKind = player2.DeviceKind,
                Player2GamepadIndex = (byte)player2.GamepadIndex,
                Player2Spawn = ToSpawn(player2.Spawn),
                Player2Catalog = ToCatalog(player2.Catalog),
                Player2Attack = ToAttack(player2.Attack),
                Player2NaturalSend = ToNaturalSend(player2.NaturalSend)
            };
        }

        static PlayerSlotPersistDto FromPlayer(
            byte isAi,
            byte deviceKind,
            byte gamepadIndex,
            DiceSpawnSettingsPayload spawn,
            DiceCatalogPayload catalog,
            PlayerAttackSettingsPayload attack,
            PlayerNaturalSendSettingsPayload naturalSend) {
            return new PlayerSlotPersistDto {
                IsAi = isAi != 0,
                DeviceKind = deviceKind,
                GamepadIndex = gamepadIndex,
                Spawn = FromSpawn(spawn),
                Catalog = FromCatalog(catalog),
                Attack = FromAttack(attack),
                NaturalSend = FromNaturalSend(naturalSend)
            };
        }

        static DiceSpawnSettingsPersistDto FromSpawn(DiceSpawnSettingsPayload payload) {
            return new DiceSpawnSettingsPersistDto {
                InitialDiceCount = payload.InitialDiceCount,
                AnimateInitialDiceSpawn = payload.AnimateInitialDiceSpawn,
                ContinuousSpawnEnabled = payload.ContinuousSpawnEnabled,
                SpawnInterval = payload.SpawnInterval,
                SpawnIntervalJitter = payload.SpawnIntervalJitter,
                BottomSpawnWeight = payload.BottomSpawnWeight
            };
        }

        static DiceSpawnSettingsPayload ToSpawn(DiceSpawnSettingsPersistDto dto) {
            dto ??= new DiceSpawnSettingsPersistDto();
            return new DiceSpawnSettingsPayload {
                InitialDiceCount = dto.InitialDiceCount,
                AnimateInitialDiceSpawn = dto.AnimateInitialDiceSpawn,
                ContinuousSpawnEnabled = dto.ContinuousSpawnEnabled,
                SpawnInterval = dto.SpawnInterval,
                SpawnIntervalJitter = dto.SpawnIntervalJitter,
                BottomSpawnWeight = dto.BottomSpawnWeight
            };
        }

        static DiceCatalogPersistDto FromCatalog(DiceCatalogPayload payload) {
            var source = payload.Entries ?? Array.Empty<DiceCatalogEntryPayload>();
            var entries = new DiceCatalogEntryPersistDto[source.Length];
            for (var i = 0; i < source.Length; i++) {
                entries[i] = new DiceCatalogEntryPersistDto {
                    Kind = source[i].Kind,
                    SpawnWeight = source[i].SpawnWeight
                };
            }

            return new DiceCatalogPersistDto { Entries = entries };
        }

        static DiceCatalogPayload ToCatalog(DiceCatalogPersistDto dto) {
            var source = dto?.Entries ?? Array.Empty<DiceCatalogEntryPersistDto>();
            var entries = new DiceCatalogEntryPayload[source.Length];
            for (var i = 0; i < source.Length; i++) {
                entries[i] = new DiceCatalogEntryPayload {
                    Kind = source[i].Kind,
                    SpawnWeight = source[i].SpawnWeight
                };
            }

            return new DiceCatalogPayload { Entries = entries };
        }

        static PlayerAttackSettingsPersistDto FromAttack(PlayerAttackSettingsPayload payload) {
            var source = payload.FaceSendProfiles ?? Array.Empty<FaceAttackSendProfilePayload>();
            var profiles = new FaceAttackSendProfilePersistDto[source.Length];
            for (var i = 0; i < source.Length; i++) {
                var kindsSource = source[i].SendableKinds ?? Array.Empty<SendableKindLimitPayload>();
                var kinds = new SendableKindLimitPersistDto[kindsSource.Length];
                for (var j = 0; j < kindsSource.Length; j++) {
                    kinds[j] = new SendableKindLimitPersistDto {
                        Kind = kindsSource[j].Kind,
                        MaxCountPerVolley = kindsSource[j].MaxCountPerVolley,
                        MinimumPower = kindsSource[j].MinimumPower,
                        SelectionWeight = kindsSource[j].SelectionWeight
                    };
                }

                var faces = source[i].TriggerFaces ?? Array.Empty<int>();
                var copiedFaces = new int[faces.Length];
                Array.Copy(faces, copiedFaces, faces.Length);
                profiles[i] = new FaceAttackSendProfilePersistDto {
                    TriggerFaces = copiedFaces,
                    SendableKinds = kinds
                };
            }

            return new PlayerAttackSettingsPersistDto {
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

        static PlayerAttackSettingsPayload ToAttack(PlayerAttackSettingsPersistDto dto) {
            dto ??= new PlayerAttackSettingsPersistDto();
            var source = dto.FaceSendProfiles ?? Array.Empty<FaceAttackSendProfilePersistDto>();
            var profiles = new FaceAttackSendProfilePayload[source.Length];
            for (var i = 0; i < source.Length; i++) {
                var kindsSource = source[i].SendableKinds ?? Array.Empty<SendableKindLimitPersistDto>();
                var kinds = new SendableKindLimitPayload[kindsSource.Length];
                for (var j = 0; j < kindsSource.Length; j++) {
                    kinds[j] = new SendableKindLimitPayload {
                        Kind = kindsSource[j].Kind,
                        MaxCountPerVolley = kindsSource[j].MaxCountPerVolley,
                        MinimumPower = kindsSource[j].MinimumPower,
                        SelectionWeight = kindsSource[j].SelectionWeight
                    };
                }

                var faces = source[i].TriggerFaces ?? Array.Empty<int>();
                var copiedFaces = new int[faces.Length];
                Array.Copy(faces, copiedFaces, faces.Length);
                profiles[i] = new FaceAttackSendProfilePayload {
                    TriggerFaces = copiedFaces,
                    SendableKinds = kinds
                };
            }

            return new PlayerAttackSettingsPayload {
                FaceSendProfiles = profiles,
                AttackMultiplier = dto.AttackMultiplier,
                FaceGain = dto.FaceGain,
                ChainGain = dto.ChainGain,
                SizeGain = dto.SizeGain,
                SnatchMultiplier = dto.SnatchMultiplier,
                Face2Weight = dto.Face2Weight,
                Face3Weight = dto.Face3Weight,
                Face4Weight = dto.Face4Weight,
                Face5Weight = dto.Face5Weight,
                Face6Weight = dto.Face6Weight,
                QueueToBoardDelay = dto.QueueToBoardDelay
            };
        }

        static PlayerNaturalSendSettingsPersistDto FromNaturalSend(PlayerNaturalSendSettingsPayload payload) {
            var source = payload.SendableKinds ?? Array.Empty<NaturalSendKindLimitPayload>();
            var kinds = new NaturalSendKindLimitPersistDto[source.Length];
            for (var i = 0; i < source.Length; i++) {
                kinds[i] = new NaturalSendKindLimitPersistDto {
                    Kind = source[i].Kind,
                    MaxCountPerVolley = source[i].MaxCountPerVolley,
                    SelectionWeight = source[i].SelectionWeight
                };
            }

            return new PlayerNaturalSendSettingsPersistDto {
                Enabled = payload.Enabled,
                DiceCountPerVolley = payload.DiceCountPerVolley,
                SendableKinds = kinds
            };
        }

        static PlayerNaturalSendSettingsPayload ToNaturalSend(PlayerNaturalSendSettingsPersistDto dto) {
            dto ??= new PlayerNaturalSendSettingsPersistDto();
            var source = dto.SendableKinds ?? Array.Empty<NaturalSendKindLimitPersistDto>();
            var kinds = new NaturalSendKindLimitPayload[source.Length];
            for (var i = 0; i < source.Length; i++) {
                kinds[i] = new NaturalSendKindLimitPayload {
                    Kind = source[i].Kind,
                    MaxCountPerVolley = source[i].MaxCountPerVolley,
                    SelectionWeight = source[i].SelectionWeight
                };
            }

            return new PlayerNaturalSendSettingsPayload {
                Enabled = dto.Enabled,
                DiceCountPerVolley = dto.DiceCountPerVolley,
                SendableKinds = kinds
            };
        }

        static JumboDiceSettingsPersistDto FromJumbo(JumboDiceSettingsPayload payload) {
            return new JumboDiceSettingsPersistDto {
                Enabled = payload.Enabled,
                SequenceStartFace = payload.SequenceStartFace,
                SequenceEndFace = payload.SequenceEndFace,
                MaxPerBoard = payload.MaxPerBoard
            };
        }

        static JumboDiceSettingsPayload ToJumbo(JumboDiceSettingsPersistDto dto) {
            dto ??= new JumboDiceSettingsPersistDto();
            var defaults = JumboDiceSettingsData.Default();
            if (dto.MaxPerBoard <= 0) {
                return new JumboDiceSettingsPayload {
                    Enabled = defaults.Enabled,
                    SequenceStartFace = defaults.SequenceStartFace,
                    SequenceEndFace = defaults.SequenceEndFace,
                    MaxPerBoard = defaults.MaxPerBoard
                };
            }

            return new JumboDiceSettingsPayload {
                Enabled = dto.Enabled,
                SequenceStartFace = dto.SequenceStartFace,
                SequenceEndFace = dto.SequenceEndFace,
                MaxPerBoard = dto.MaxPerBoard
            };
        }
    }
}
