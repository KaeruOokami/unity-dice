namespace DiceGame.Session
{
    static class ControlsPanelLabels
    {
        public const string Title = "Controls";
        public const string Player = "Player";
        public const string OnlineNote = "Online: this machine uses 1P bindings for local control.";
        public const string ResetDefaults = "Reset to Defaults";
        public const string WaitingForInput = "Press a key...";
        public const string RebindCancelled = "Rebind cancelled.";
        public const string PlayerSlotDropdown = "ControlsPlayerSlotDropdown";

        public const string MoveUp = "Move Up";
        public const string MoveDown = "Move Down";
        public const string MoveLeft = "Move Left";
        public const string MoveRight = "Move Right";
        public const string Lift = "Lift";
        public const string Jump = "Jump";

        public static readonly string[] PlayerOptions = { "1P", "2P" };
    }
}
