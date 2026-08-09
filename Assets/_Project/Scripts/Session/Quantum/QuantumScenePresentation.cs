namespace DiceGame.Session
{
    using DiceGame.Gameplay;
    using UnityEngine;

    /// <summary>
    /// Presentation helpers when Quantum runs inside the product Game scene.
    /// </summary>
    public static class QuantumScenePresentation
    {
        public static void ApplyForActiveSession(GameBootstrap gameBootstrap)
        {
            if (gameBootstrap == null)
            {
                return;
            }

            // Use the same camera contract as local/UGS BeginSession.
            gameBootstrap.ApplyCameraSetup();
        }

        public static void RestoreAfterQuantumUnload()
        {
        }
    }
}
