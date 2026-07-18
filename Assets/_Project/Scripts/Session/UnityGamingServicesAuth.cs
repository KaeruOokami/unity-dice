using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace DiceGame.Session
{
    public static class UnityGamingServicesAuth
    {
        public static async Task EnsureSignedInAsync() {
            if (UnityServices.State == ServicesInitializationState.Uninitialized) {
                await UnityServices.InitializeAsync();
            }

            if (AuthenticationService.Instance.IsSignedIn) {
                return;
            }

            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            Debug.Log($"OnlineSession: Signed in anonymously as {AuthenticationService.Instance.PlayerId}");
        }
    }
}
