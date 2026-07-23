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
        public const string MessageMatchStart = "DiceOnlineMatchStart";
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
        public const float SnapshotSendIntervalSeconds = 0.1f;
        public const float AttackQueueResyncIntervalSeconds = 1f;
        public const float InputSendIntervalSeconds = 0.05f;
        /// <summary>
        /// Soft warning threshold for one Reliable snapshot datagram (must remain a single packet).
        /// </summary>
        public const int SnapshotReliableSoftBytes = 3500;
        /// <summary>
        /// Character presentation only: SmoothDamp time toward latest snapshot targets.
        /// Dice use local Play* animations + logical SnapTo; no pose chase.
        /// </summary>
        public const float SnapshotInterpSmoothTimeSeconds = 0.06f;
        /// <summary>
        /// Beyond this world distance, snap character instead of interpolating.
        /// </summary>
        public const float SnapshotInterpSnapDistance = 2f;
        public const float OnlineSetupSyncIntervalSeconds = 0.35f;
        public const float OnlineIdentityRetryIntervalSeconds = 0.5f;
        public const string MatchSetupPersistDirectory = "MatchSetup";
        public const string MatchSetupOnlinePersistDirectory = "Online";
    }
}
