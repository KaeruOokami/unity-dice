namespace Quantum
{
    /// <summary>
    /// Defaults for the Quantum board slice when <see cref="RuntimeConfig"/> fields are unset.
    /// Mirrors the project's common 4x6 player board sizing.
    /// </summary>
    public static class BoardDefaults
    {
        public const int BoardWidth = 4;
        public const int BoardHeight = 6;
        /// <summary>Matches <c>DiceSpawnSettings</c> default initial count.</summary>
        public const int InitialDiceCount = 3;
        public const int MinFaceValue = 1;
        public const int MaxFaceValue = 6;
    }
}
