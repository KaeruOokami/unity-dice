namespace Quantum
{
    /// <summary>
    /// Defaults for the Phase B vertical slice when <see cref="RuntimeConfig"/> fields are unset.
    /// Mirrors the project's common 4x6 player board sizing.
    /// </summary>
    public static class PhaseBSimDefaults
    {
        public const int BoardWidth = 4;
        public const int BoardHeight = 6;
        public const int InitialDiceCount = 3;
        public const int MinFaceValue = 1;
        public const int MaxFaceValue = 6;
    }
}
