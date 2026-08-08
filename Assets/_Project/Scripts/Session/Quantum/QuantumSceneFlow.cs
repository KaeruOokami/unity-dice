namespace DiceGame.Session
{
    using System.Threading.Tasks;
    using UnityEngine.SceneManagement;

    /// <summary>
    /// Loads / unloads <see cref="SessionConstants.QuantumGameSceneName"/> for Quantum sessions.
    /// </summary>
    public static class QuantumSceneFlow
    {
        public static bool IsQuantumSceneLoaded
        {
            get
            {
                var scene = SceneManager.GetSceneByName(SessionConstants.QuantumGameSceneName);
                return scene.IsValid() && scene.isLoaded;
            }
        }

        public static async Task EnsureLoadedAsync()
        {
            if (IsQuantumSceneLoaded)
            {
                return;
            }

            var op = SceneManager.LoadSceneAsync(
                SessionConstants.QuantumGameSceneName,
                LoadSceneMode.Additive);
            if (op == null)
            {
                throw new System.InvalidOperationException(
                    $"Failed to load scene '{SessionConstants.QuantumGameSceneName}'. " +
                    "Enable it in Editor Build Settings.");
            }

            while (!op.isDone)
            {
                await Task.Yield();
            }
        }

        public static async Task UnloadAsync()
        {
            if (!IsQuantumSceneLoaded)
            {
                return;
            }

            var op = SceneManager.UnloadSceneAsync(SessionConstants.QuantumGameSceneName);
            if (op == null)
            {
                return;
            }

            while (!op.isDone)
            {
                await Task.Yield();
            }
        }
    }
}
