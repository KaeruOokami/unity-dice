using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace DiceGame.Session
{
    public static class OnlineRelayFacade
    {
        public static async Task<(Allocation Allocation, string JoinCode)> CreateAllocationAsync(
            int maxConnections) {
            await UnityGamingServicesAuth.EnsureSignedInAsync();

            var allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);
            var joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            return (allocation, joinCode);
        }

        public static async Task<JoinAllocation> JoinAllocationAsync(string relayJoinCode) {
            await UnityGamingServicesAuth.EnsureSignedInAsync();
            return await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
        }

        public static void ConfigureHostTransport(UnityTransport transport, Allocation allocation) {
            if (transport == null) {
                throw new System.ArgumentNullException(nameof(transport));
            }

            transport.SetRelayServerData(ToRelayServerData(allocation));
        }

        public static void ConfigureClientTransport(UnityTransport transport, JoinAllocation allocation) {
            if (transport == null) {
                throw new System.ArgumentNullException(nameof(transport));
            }

            transport.SetRelayServerData(ToRelayServerData(allocation));
        }

        public static UnityTransport EnsureUnityTransport(NetworkManager networkManager) {
            if (networkManager == null) {
                throw new System.ArgumentNullException(nameof(networkManager));
            }

            var transport = networkManager.GetComponent<UnityTransport>();
            if (transport == null) {
                transport = networkManager.gameObject.AddComponent<UnityTransport>();
            }

            networkManager.NetworkConfig.NetworkTransport = transport;
            transport.MaxPayloadSize = Mathf.Max(transport.MaxPayloadSize, 16 * 1024);
            return transport;
        }

        static RelayServerData ToRelayServerData(Allocation allocation) {
            return new RelayServerData(allocation, OnlineSessionConstants.RelayConnectionType);
        }

        static RelayServerData ToRelayServerData(JoinAllocation allocation) {
            return new RelayServerData(allocation, OnlineSessionConstants.RelayConnectionType);
        }
    }
}
