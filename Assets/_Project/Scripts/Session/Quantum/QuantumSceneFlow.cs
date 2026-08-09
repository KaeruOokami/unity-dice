namespace DiceGame.Session
{
    using System.Threading.Tasks;
    using UnityEngine.SceneManagement;

    /// <summary>
    /// Hosts Quantum on the product <see cref="SceneNames.Game"/> scene (no Additive QuantumGameScene).
    /// </summary>
    public static class QuantumSceneFlow
    {
        public static bool IsGameSceneLoaded
        {
            get
            {
                var scene = SceneManager.GetSceneByName(SceneNames.Game);
                return scene.IsValid() && scene.isLoaded;
            }
        }

        public static async Task EnsureLoadedAsync()
        {
            if (!IsGameSceneLoaded)
            {
                var op = SceneManager.LoadSceneAsync(SceneNames.Game, LoadSceneMode.Additive);
                if (op == null)
                {
                    throw new System.InvalidOperationException(
                        $"Failed to load scene '{SceneNames.Game}'.");
                }

                while (!op.isDone)
                {
                    await Task.Yield();
                }
            }

            // Drop legacy QuantumGameScene if it was left loaded from older builds.
            var legacy = SceneManager.GetSceneByName(SessionConstants.QuantumGameSceneName);
            if (legacy.IsValid() && legacy.isLoaded)
            {
                var unload = SceneManager.UnloadSceneAsync(legacy);
                if (unload != null)
                {
                    while (!unload.isDone)
                    {
                        await Task.Yield();
                    }
                }
            }

            QuantumGameHost.EnsureReady();
            await Task.Yield();
        }

        public static async Task UnloadAsync()
        {
            QuantumGameHost.Teardown();

            var legacy = SceneManager.GetSceneByName(SessionConstants.QuantumGameSceneName);
            if (legacy.IsValid() && legacy.isLoaded)
            {
                var unload = SceneManager.UnloadSceneAsync(legacy);
                if (unload != null)
                {
                    while (!unload.isDone)
                    {
                        await Task.Yield();
                    }
                }
            }

            await Task.Yield();
        }
    }
}
