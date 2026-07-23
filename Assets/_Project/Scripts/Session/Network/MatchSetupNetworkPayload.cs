using Unity.Netcode;

namespace DiceGame.Session.Network
{
    public struct DiceSpawnSettingsNetworkPayload : INetworkSerializable
    {
        public int InitialDiceCount;
        public bool AnimateInitialDiceSpawn;
        public bool ContinuousSpawnEnabled;
        public float SpawnInterval;
        public float SpawnIntervalJitter;
        public float BottomSpawnWeight;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref InitialDiceCount);
            serializer.SerializeValue(ref AnimateInitialDiceSpawn);
            serializer.SerializeValue(ref ContinuousSpawnEnabled);
            serializer.SerializeValue(ref SpawnInterval);
            serializer.SerializeValue(ref SpawnIntervalJitter);
            serializer.SerializeValue(ref BottomSpawnWeight);
        }
    }

    public struct DiceCatalogEntryNetworkPayload : INetworkSerializable
    {
        public byte Kind;
        public float SpawnWeight;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref SpawnWeight);
        }
    }

    public struct DiceCatalogNetworkPayload : INetworkSerializable
    {
        public DiceCatalogEntryNetworkPayload[] Entries;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            var count = Entries?.Length ?? 0;
            serializer.SerializeValue(ref count);
            if (serializer.IsReader) {
                Entries = count > 0
                    ? new DiceCatalogEntryNetworkPayload[count]
                    : System.Array.Empty<DiceCatalogEntryNetworkPayload>();
            }

            for (var i = 0; i < count; i++) {
                Entries[i].NetworkSerialize(serializer);
            }
        }
    }

    public struct SendableKindLimitNetworkPayload : INetworkSerializable
    {
        public byte Kind;
        public int MaxCountPerVolley;
        public float MinimumPower;
        public float SelectionWeight;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref MaxCountPerVolley);
            serializer.SerializeValue(ref MinimumPower);
            serializer.SerializeValue(ref SelectionWeight);
        }
    }

    public struct FaceAttackSendProfileNetworkPayload : INetworkSerializable
    {
        public int[] TriggerFaces;
        public SendableKindLimitNetworkPayload[] SendableKinds;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            var faceCount = TriggerFaces?.Length ?? 0;
            serializer.SerializeValue(ref faceCount);
            if (serializer.IsReader) {
                TriggerFaces = faceCount > 0 ? new int[faceCount] : System.Array.Empty<int>();
            }

            for (var i = 0; i < faceCount; i++) {
                serializer.SerializeValue(ref TriggerFaces[i]);
            }

            var kindCount = SendableKinds?.Length ?? 0;
            serializer.SerializeValue(ref kindCount);
            if (serializer.IsReader) {
                SendableKinds = kindCount > 0
                    ? new SendableKindLimitNetworkPayload[kindCount]
                    : System.Array.Empty<SendableKindLimitNetworkPayload>();
            }

            for (var i = 0; i < kindCount; i++) {
                SendableKinds[i].NetworkSerialize(serializer);
            }
        }
    }

    public struct PlayerAttackSettingsNetworkPayload : INetworkSerializable
    {
        public FaceAttackSendProfileNetworkPayload[] FaceSendProfiles;
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

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            var count = FaceSendProfiles?.Length ?? 0;
            serializer.SerializeValue(ref count);
            if (serializer.IsReader) {
                FaceSendProfiles = count > 0
                    ? new FaceAttackSendProfileNetworkPayload[count]
                    : System.Array.Empty<FaceAttackSendProfileNetworkPayload>();
            }

            for (var i = 0; i < count; i++) {
                FaceSendProfiles[i].NetworkSerialize(serializer);
            }

            serializer.SerializeValue(ref AttackMultiplier);
            serializer.SerializeValue(ref FaceGain);
            serializer.SerializeValue(ref ChainGain);
            serializer.SerializeValue(ref SizeGain);
            serializer.SerializeValue(ref SnatchMultiplier);
            serializer.SerializeValue(ref Face2Weight);
            serializer.SerializeValue(ref Face3Weight);
            serializer.SerializeValue(ref Face4Weight);
            serializer.SerializeValue(ref Face5Weight);
            serializer.SerializeValue(ref Face6Weight);
            serializer.SerializeValue(ref QueueToBoardDelay);
        }
    }

    public struct NaturalSendKindLimitNetworkPayload : INetworkSerializable
    {
        public byte Kind;
        public int MaxCountPerVolley;
        public float SelectionWeight;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref MaxCountPerVolley);
            serializer.SerializeValue(ref SelectionWeight);
        }
    }

    public struct PlayerNaturalSendSettingsNetworkPayload : INetworkSerializable
    {
        public bool Enabled;
        public int DiceCountPerVolley;
        public NaturalSendKindLimitNetworkPayload[] SendableKinds;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref Enabled);
            serializer.SerializeValue(ref DiceCountPerVolley);
            var count = SendableKinds?.Length ?? 0;
            serializer.SerializeValue(ref count);
            if (serializer.IsReader) {
                SendableKinds = count > 0
                    ? new NaturalSendKindLimitNetworkPayload[count]
                    : System.Array.Empty<NaturalSendKindLimitNetworkPayload>();
            }

            for (var i = 0; i < count; i++) {
                SendableKinds[i].NetworkSerialize(serializer);
            }
        }
    }

    public struct MatchSetupNetworkPayload : INetworkSerializable
    {
        public const byte InvalidPresetIndex = byte.MaxValue;

        public byte GameMode;
        public DiceSpawnSettingsNetworkPayload SharedSpawn;
        public DiceCatalogNetworkPayload SharedCatalog;
        public byte Player1IsAi;
        public byte Player1DeviceKind;
        public byte Player1GamepadIndex;
        public DiceSpawnSettingsNetworkPayload Player1Spawn;
        public DiceCatalogNetworkPayload Player1Catalog;
        public PlayerAttackSettingsNetworkPayload Player1Attack;
        public PlayerNaturalSendSettingsNetworkPayload Player1NaturalSend;
        public byte Player2IsAi;
        public byte Player2DeviceKind;
        public byte Player2GamepadIndex;
        public DiceSpawnSettingsNetworkPayload Player2Spawn;
        public DiceCatalogNetworkPayload Player2Catalog;
        public PlayerAttackSettingsNetworkPayload Player2Attack;
        public PlayerNaturalSendSettingsNetworkPayload Player2NaturalSend;
        /// <summary>
        /// In-memory only. Wire format keeps lobby/setup payloads unchanged;
        /// MatchStart carries the seed as a trailing int after this payload.
        /// </summary>
        public int MatchSeed;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter {
            serializer.SerializeValue(ref GameMode);
            SharedSpawn.NetworkSerialize(serializer);
            SharedCatalog.NetworkSerialize(serializer);
            serializer.SerializeValue(ref Player1IsAi);
            serializer.SerializeValue(ref Player1DeviceKind);
            serializer.SerializeValue(ref Player1GamepadIndex);
            Player1Spawn.NetworkSerialize(serializer);
            Player1Catalog.NetworkSerialize(serializer);
            Player1Attack.NetworkSerialize(serializer);
            Player1NaturalSend.NetworkSerialize(serializer);
            serializer.SerializeValue(ref Player2IsAi);
            serializer.SerializeValue(ref Player2DeviceKind);
            serializer.SerializeValue(ref Player2GamepadIndex);
            Player2Spawn.NetworkSerialize(serializer);
            Player2Catalog.NetworkSerialize(serializer);
            Player2Attack.NetworkSerialize(serializer);
            Player2NaturalSend.NetworkSerialize(serializer);
            // Intentionally do not serialize MatchSeed here (breaks lobby setup broadcasts).
        }
    }
}
