namespace DiceGame.Session
{
    /// <summary>
    /// Online session transport.
    /// Quantum is the default Phase D/E path; UgsNgo keeps dual-sim fallback.
    /// </summary>
    public enum SessionNetworkingBackend
    {
        UgsNgo = 0,
        Quantum = 1,
    }
}
