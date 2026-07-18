namespace DiceGame.Session
{
    public static class OnlineSessionConstants
    {
        public const int MaxPlayers = 2;
        public const string LobbyDataRelayJoinCode = "RelayJoinCode";
        public const string LobbyDataRelayRegion = "RelayRegion";
        public const string MessageInput = "DiceOnlineInput";
        public const string MessageSnapshot = "DiceOnlineSnapshot";
        public const string MessageMatchStart = "DiceOnlineMatchStart";
        public const string MessageFlowCommand = "DiceOnlineFlowCommand";
        public const string MessageFlowRequest = "DiceOnlineFlowRequest";
        public const string RelayConnectionType = "dtls";

        public const byte FlowPause = 1;
        public const byte FlowResume = 2;
        public const byte FlowResetMatch = 3;
        public const byte FlowReturnToTitle = 4;
        public const float LobbyHeartbeatSeconds = 15f;
        public const float SnapshotSendIntervalSeconds = 0.05f;
        public const float InputSendIntervalSeconds = 0.05f;
    }
}
