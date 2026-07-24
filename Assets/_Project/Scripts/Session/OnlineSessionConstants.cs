namespace DiceGame.Session
{
    public static class OnlineSessionConstants
    {
        public const int MaxPlayers = 2;
        public const string LobbyDataRelayJoinCode = "RelayJoinCode";
        public const string LobbyDataRelayRegion = "RelayRegion";
        public const string LobbyDataGameMode = "GameMode";
        public const string MessageInput = "DiceOnlineInput";
        public const string MessageSnapshot = "DiceOnlineSnapshot";
        public const string MessageDiceMotion = "DiceOnlineDiceMotion";
        public const string MessageAttackQueue = "DiceOnlineAttackQueue";
        public const string MessageDiceSpawn = "DiceOnlineDiceSpawn";
        public const string MessageCharacterState = "DiceOnlineCharacterState";
        public const string MessageMatchStart = "DiceOnlineMatchStart";
        public const string MessageMatchStartAck = "DiceOnlineMatchStartAck";
        public const string MessageMatchSetupBroadcast = "DiceOnlineMatchSetupBroadcast";
        public const string MessageMatchSetupUpdate = "DiceOnlineMatchSetupUpdate";
        public const string MessagePlayerIdentity = "DiceOnlinePlayerIdentity";
        public const string MessagePlayerIdentityRequest = "DiceOnlinePlayerIdentityRequest";
        public const string MessageFlowCommand = "DiceOnlineFlowCommand";
        public const string MessageFlowRequest = "DiceOnlineFlowRequest";
        public const string RelayConnectionType = "dtls";

        public const byte FlowPause = 1;
        public const byte FlowResume = 2;
        public const byte FlowResetMatch = 3;
        public const byte FlowReturnToTitle = 4;
        public const float LobbyHeartbeatSeconds = 15f;
        /// <summary>
        /// Host → client character poses (Phase A). Full board is event-driven + one-shot initial dump.
        /// </summary>
        public const float SnapshotSendIntervalSeconds = 0.15f;
        public const float AttackQueueResyncIntervalSeconds = 1f;
        public const float InputSendIntervalSeconds = 0.05f;
        /// <summary>
        /// Host → clients: character pose corrections for rollback (not full board).
        /// </summary>
        public const float CharacterStateSendIntervalSeconds = 0.1f;
        /// <summary>
        /// Local input / pose history depth for character-only rollback resim.
        /// </summary>
        public const int CharacterRollbackHistorySize = 64;
        /// <summary>
        /// Soft warning for large board dumps (initial / rare resync). Fragmented delivery allows larger than MTU.
        /// </summary>
        public const int SnapshotReliableSoftBytes = 8000;
        /// <summary>
        /// Remote character only: SmoothDamp time toward latest snapshot targets.
        /// Local character uses prediction; dice use Play* + logical SnapTo.
        /// </summary>
        public const float SnapshotInterpSmoothTimeSeconds = 0.1f;
        /// <summary>
        /// Beyond this world distance, snap character instead of interpolating / soft reconcile.
        /// </summary>
        public const float SnapshotInterpSnapDistance = 2f;
        /// <summary>
        /// Soft blend toward host pose when the local predicted character is nearly idle (0..1).
        /// Kept low so moving prediction is not constantly tugged.
        /// </summary>
        public const float LocalCharacterReconcileBlend = 0.08f;
        /// <summary>
        /// Soft reconcile applies only while predicted move speed is at or below this.
        /// </summary>
        public const float LocalCharacterReconcileIdleSpeed = 0.2f;
        public const float OnlineSetupSyncIntervalSeconds = 0.35f;
        public const float OnlineIdentityRetryIntervalSeconds = 0.5f;
        /// <summary>
        /// Host resends MatchStart until the remote client acks presentation ready.
        /// </summary>
        public const float MatchStartAckRetryIntervalSeconds = 0.5f;
        /// <summary>
        /// Give up waiting for MatchStartAck and surface an error.
        /// </summary>
        public const float MatchStartAckTimeoutSeconds = 15f;
        public const string MatchSetupPersistDirectory = "MatchSetup";
        public const string MatchSetupOnlinePersistDirectory = "Online";
    }
}
