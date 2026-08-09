namespace DiceGame.Session
{
    using Quantum;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    /// <summary>
    /// Ensures Quantum runtime components live in the product <see cref="SceneNames.Game"/> scene.
    /// Does not load <c>QuantumGameScene</c>.
    /// </summary>
    public static class QuantumGameHost
    {
        const string HostRootName = "QuantumRuntimeHost";
        const string MapAssetPath = "QuantumUser/Resources/QuantumMap";

        static GameObject hostRoot;
        static bool ownsHostRoot;

        public static void EnsureReady()
        {
            UnloadLegacyQuantumSceneIfPresent();

            var gameScene = SceneManager.GetSceneByName(SceneNames.Game);
            if (!gameScene.IsValid() || !gameScene.isLoaded)
            {
                throw new System.InvalidOperationException(
                    $"Scene '{SceneNames.Game}' must be loaded to host Quantum.");
            }

            var mapData = Object.FindAnyObjectByType<QuantumMapData>();
            var viewUpdater = Object.FindAnyObjectByType<QuantumEntityViewUpdater>();
            var inputPoller = Object.FindAnyObjectByType<DiceInputPoller>();
            var viewBinder = Object.FindAnyObjectByType<DiceBoardViewBinder>();

            if (mapData != null
                && viewUpdater != null
                && inputPoller != null
                && viewBinder != null
                && mapData.gameObject.scene == gameScene)
            {
                BindMapAsset(mapData);
                return;
            }

            if (hostRoot == null)
            {
                hostRoot = new GameObject(HostRootName);
                SceneManager.MoveGameObjectToScene(hostRoot, gameScene);
                ownsHostRoot = true;
            }

            if (mapData == null || mapData.gameObject.scene != gameScene)
            {
                var mapGo = new GameObject("QuantumMap");
                mapGo.transform.SetParent(hostRoot.transform, false);
                mapData = mapGo.AddComponent<QuantumMapData>();
            }

            BindMapAsset(mapData);

            if (viewUpdater == null || viewUpdater.gameObject.scene != gameScene)
            {
                var updaterGo = new GameObject("QuantumEntityViewUpdater");
                updaterGo.transform.SetParent(hostRoot.transform, false);
                updaterGo.AddComponent<QuantumEntityViewUpdater>();
            }

            if (inputPoller == null || inputPoller.gameObject.scene != gameScene)
            {
                var inputGo = new GameObject("DiceInputPoller");
                inputGo.transform.SetParent(hostRoot.transform, false);
                inputGo.AddComponent<DiceInputPoller>();
                inputGo.AddComponent<DiceBoardViewBinder>();
            }
            else if (viewBinder == null)
            {
                inputPoller.gameObject.AddComponent<DiceBoardViewBinder>();
            }

            var debugRunner = Object.FindAnyObjectByType<QuantumRunnerLocalDebug>();
            if (debugRunner != null)
            {
                debugRunner.enabled = false;
            }
        }

        public static void Teardown()
        {
            if (ownsHostRoot && hostRoot != null)
            {
                Object.Destroy(hostRoot);
            }

            hostRoot = null;
            ownsHostRoot = false;
        }

        static void BindMapAsset(QuantumMapData mapData)
        {
            if (mapData == null)
            {
                return;
            }

            if (mapData.AssetRef.Id.IsValid)
            {
                return;
            }

            if (QuantumUnityDB.TryGetGlobalAsset<Map>(MapAssetPath, out var map) && map != null)
            {
                mapData.AssetRef = map;
            }
            else
            {
                Debug.LogError(
                    $"QuantumGameHost: failed to resolve map asset at '{MapAssetPath}'.");
            }
        }

        static void UnloadLegacyQuantumSceneIfPresent()
        {
            var legacy = SceneManager.GetSceneByName(SessionConstants.QuantumGameSceneName);
            if (!legacy.IsValid() || !legacy.isLoaded)
            {
                return;
            }

            var op = SceneManager.UnloadSceneAsync(legacy);
            // Fire-and-forget unload; EnsureReady continues on Game.
            if (op == null)
            {
                Debug.LogWarning("QuantumGameHost: could not unload legacy QuantumGameScene.");
            }
        }
    }
}
