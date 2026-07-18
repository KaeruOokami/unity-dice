using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace DiceGame.Session
{
    public static class OnlineNetworkHost
    {
        public static NetworkManager EnsureNetworkManager() {
            if (NetworkManager.Singleton != null) {
                return NetworkManager.Singleton;
            }

            var go = new GameObject("OnlineNetworkManager");
            Object.DontDestroyOnLoad(go);

            var networkManager = go.AddComponent<NetworkManager>();
            var transport = go.AddComponent<UnityTransport>();

            if (networkManager.NetworkConfig == null) {
                networkManager.NetworkConfig = new NetworkConfig();
            }

            networkManager.NetworkConfig.NetworkTransport = transport;
            networkManager.NetworkConfig.TickRate = 30;
            networkManager.NetworkConfig.EnableSceneManagement = false;
            networkManager.NetworkConfig.PlayerPrefab = null;
            return networkManager;
        }

        public static void Shutdown() {
            if (NetworkManager.Singleton == null) {
                return;
            }

            if (NetworkManager.Singleton.IsListening) {
                NetworkManager.Singleton.Shutdown();
            }

            Object.Destroy(NetworkManager.Singleton.gameObject);
        }
    }
}
