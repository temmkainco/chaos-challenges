using UnityEngine;
using Steamworks;

namespace Platform
{
    public class SteamPlatformService : IPlatformService
    {
        public bool IsAvailable => SteamManager.Initialized;

        public void Init()
        {
            if (!IsAvailable)
            {
                Debug.LogWarning("Steam is not initialized! Running without Steam features.");
                return;
            }

            Debug.Log($"SteamPlatformService initialized: {SteamFriends.GetPersonaName()}");
        }

        public void Shutdown()
        {
            // SteamManager handles shutdown
        }

        public string GetPlayerName()
        {
            return IsAvailable ? SteamFriends.GetPersonaName() : "SteamUser";
        }
    }
}
