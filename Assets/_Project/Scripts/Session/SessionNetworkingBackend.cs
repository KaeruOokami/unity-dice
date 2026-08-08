namespace DiceGame.Session
{
    /// <summary>
    /// Online session transport. Quantum is the Phase D path; UgsNgo keeps dual-sim fallback.
    /// </summary>
    public enum SessionNetworkingBackend
    {
        UgsNgo = 0,
        Quantum = 1,
    }
}
