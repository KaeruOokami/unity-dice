namespace DiceGame.Session
{
    using System;
    using System.Text;
    using DiceGame.Config;
    using DiceGame.Session.Network;
    using Photon.Client;
    using Photon.Realtime;
    using UnityEngine;

    /// <summary>
    /// Syncs online match setup between Quantum peers via Photon room properties + RaiseEvent.
    /// Mirrors the UGS broadcast/update semantics without NGO.
    /// </summary>
    public sealed class QuantumMatchSetupSync : IDisposable
    {
        readonly MatchSetupPresetRegistry registry;
        RealtimeClient client;
        Action<EventData> eventHandler;
        int lastAppliedRevision = -1;
        string lastPublishedJson = string.Empty;

        public int LocalRevision { get; private set; }
        public bool HasRemoteSetup => lastAppliedRevision > 0;
        public MatchSetupSnapshot LastAppliedSnapshot { get; private set; }

        public QuantumMatchSetupSync(MatchSetupPresetRegistry registry)
        {
            this.registry = registry;
        }

        public void Bind(RealtimeClient realtimeClient)
        {
            UnbindEvents();
            client = realtimeClient;
            if (client == null)
            {
                return;
            }

            eventHandler = OnEvent;
            client.EventReceived += eventHandler;
        }

        public void Dispose()
        {
            UnbindEvents();
            client = null;
            lastAppliedRevision = -1;
            lastPublishedJson = string.Empty;
            LastAppliedSnapshot = null;
            LocalRevision = 0;
        }

        public bool TryPublishHostSetup(MatchSetupSnapshot snapshot, GameMode mode, out string error)
        {
            error = null;
            if (client?.CurrentRoom == null)
            {
                error = "Not in a Quantum room.";
                return false;
            }

            if (registry == null || snapshot == null)
            {
                error = "Setup registry or snapshot missing.";
                return false;
            }

            snapshot.GameMode = mode;
            if (!snapshot.TryValidate(registry, out error))
            {
                return false;
            }

            if (!TryEncode(snapshot, out var json, out error))
            {
                return false;
            }

            LocalRevision++;
            lastPublishedJson = json;
            LastAppliedSnapshot = snapshot;
            lastAppliedRevision = LocalRevision;

            var props = new PhotonHashtable
            {
                { SessionConstants.QuantumRoomGameModeProperty, (int)mode },
                { SessionConstants.QuantumRoomSetupRevisionProperty, LocalRevision },
            };
            WriteChunkedSetup(props, json);
            client.CurrentRoom.SetCustomProperties(props);
            return true;
        }

        public bool TrySendClientDraft(MatchSetupSnapshot snapshot, GameMode mode, out string error)
        {
            error = null;
            if (client == null || !client.InRoom)
            {
                error = "Not in a Quantum room.";
                return false;
            }

            snapshot.GameMode = mode;
            if (!snapshot.TryValidate(registry, out error))
            {
                return false;
            }

            if (!TryEncode(snapshot, out var json, out error))
            {
                return false;
            }

            var content = new PhotonHashtable
            {
                { SessionConstants.QuantumSetupEventJsonKey, json },
            };
            var raised = client.OpRaiseEvent(
                SessionConstants.QuantumSetupDraftEventCode,
                content,
                new RaiseEventArgs { Receivers = ReceiverGroup.MasterClient },
                SendOptions.SendReliable);
            if (!raised)
            {
                error = "Failed to raise Quantum setup draft event.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Polls room properties for host-published setup. Returns true when a new revision was applied.
        /// </summary>
        public bool TryPullHostSetup(out MatchSetupSnapshot snapshot, out bool isFirstSetup)
        {
            snapshot = null;
            isFirstSetup = false;
            if (client?.CurrentRoom?.CustomProperties == null || registry == null)
            {
                return false;
            }

            var props = client.CurrentRoom.CustomProperties;
            if (!TryReadInt(props, SessionConstants.QuantumRoomSetupRevisionProperty, out var revision)
                || revision <= 0
                || revision == lastAppliedRevision)
            {
                return false;
            }

            if (!TryReadChunkedSetup(props, out var json) || string.IsNullOrEmpty(json))
            {
                return false;
            }

            if (!TryDecode(json, out snapshot, out var error))
            {
                Debug.LogError($"QuantumMatchSetupSync: decode failed: {error}");
                return false;
            }

            if (TryReadInt(props, SessionConstants.QuantumRoomGameModeProperty, out var modeInt)
                && Enum.IsDefined(typeof(GameMode), modeInt))
            {
                snapshot.GameMode = (GameMode)modeInt;
            }

            isFirstSetup = lastAppliedRevision <= 0;
            lastAppliedRevision = revision;
            LocalRevision = revision;
            lastPublishedJson = json;
            LastAppliedSnapshot = snapshot;
            return true;
        }

        public bool TryHandleIncomingClientDraft(
            EventData eventData,
            out MatchSetupSnapshot snapshot)
        {
            snapshot = null;
            if (eventData == null
                || eventData.Code != SessionConstants.QuantumSetupDraftEventCode
                || eventData.CustomData is not PhotonHashtable table)
            {
                return false;
            }

            if (!table.TryGetValue(SessionConstants.QuantumSetupEventJsonKey, out var raw)
                || raw is not string json
                || string.IsNullOrEmpty(json))
            {
                return false;
            }

            if (!TryDecode(json, out snapshot, out var error))
            {
                Debug.LogError($"QuantumMatchSetupSync: client draft decode failed: {error}");
                return false;
            }

            return true;
        }

        public bool HasMatchingRevision(int expectedRevision)
        {
            return expectedRevision > 0 && lastAppliedRevision == expectedRevision;
        }

        void OnEvent(EventData eventData)
        {
            // Host handling is driven by SessionController via TryHandleIncomingClientDraft
            // after reading the same EventReceived subscription through PollIncomingDrafts.
            PendingClientDraftEvent = eventData;
        }

        public EventData PendingClientDraftEvent { get; private set; }

        public void ConsumePendingClientDraftEvent()
        {
            PendingClientDraftEvent = null;
        }

        void UnbindEvents()
        {
            if (client != null && eventHandler != null)
            {
                client.EventReceived -= eventHandler;
            }

            eventHandler = null;
            PendingClientDraftEvent = null;
        }

        static bool TryEncode(MatchSetupSnapshot snapshot, out string json, out string error)
        {
            json = null;
            error = null;
            try
            {
                // Codec path keeps network/payload shape aligned with UGS.
                var payload = MatchSetupCodec.ToPayload(snapshot, null);
                var file = MatchSetupPersistMapper.FromPayload(payload);
                json = JsonUtility.ToJson(file);
                if (string.IsNullOrEmpty(json))
                {
                    error = "Encoded setup JSON was empty.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        bool TryDecode(string json, out MatchSetupSnapshot snapshot, out string error)
        {
            snapshot = null;
            error = null;
            try
            {
                var file = JsonUtility.FromJson<MatchSetupPersistFile>(json);
                if (file == null)
                {
                    error = "Persist file was null.";
                    return false;
                }

                var payload = MatchSetupPersistMapper.ToPayload(file);
                return MatchSetupCodec.TryFromPayload(payload, registry, out snapshot, out error);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        static void WriteChunkedSetup(PhotonHashtable props, string json)
        {
            var chunkSize = SessionConstants.QuantumSetupPropertyChunkSize;
            var partCount = Mathf.Max(1, (json.Length + chunkSize - 1) / chunkSize);
            props[SessionConstants.QuantumRoomSetupPartCountProperty] = partCount;
            for (var i = 0; i < partCount; i++)
            {
                var start = i * chunkSize;
                var len = Mathf.Min(chunkSize, json.Length - start);
                props[SessionConstants.QuantumRoomSetupChunkPropertyPrefix + i] = json.Substring(start, len);
            }
        }

        static bool TryReadChunkedSetup(PhotonHashtable props, out string json)
        {
            json = null;
            if (!TryReadInt(props, SessionConstants.QuantumRoomSetupPartCountProperty, out var partCount)
                || partCount <= 0)
            {
                // Legacy single-key fallback.
                if (props.TryGetValue(SessionConstants.QuantumRoomSetupProperty, out var single)
                    && single is string s
                    && !string.IsNullOrEmpty(s))
                {
                    json = s;
                    return true;
                }

                return false;
            }

            var builder = new StringBuilder(partCount * SessionConstants.QuantumSetupPropertyChunkSize);
            for (var i = 0; i < partCount; i++)
            {
                var key = SessionConstants.QuantumRoomSetupChunkPropertyPrefix + i;
                if (!props.TryGetValue(key, out var raw) || raw is not string chunk)
                {
                    return false;
                }

                builder.Append(chunk);
            }

            json = builder.ToString();
            return !string.IsNullOrEmpty(json);
        }

        static bool TryReadInt(PhotonHashtable props, string key, out int value)
        {
            value = 0;
            if (props == null || !props.TryGetValue(key, out var raw) || raw == null)
            {
                return false;
            }

            switch (raw)
            {
                case int i:
                    value = i;
                    return true;
                case byte b:
                    value = b;
                    return true;
                case short s:
                    value = s;
                    return true;
                default:
                    return int.TryParse(raw.ToString(), out value);
            }
        }
    }
}
