using UnityEngine;
using Steamworks;
using System;

namespace Platform
{
    public class SteamPlatformService : IPlatformService
    {
        //public event Action<CSteamID> OnInviteReceived;
        public bool IsAvailable => SteamManager.Initialized;

        public void Init()
        {
            if (!IsAvailable)
                Debug.LogWarning("Steam not initialized");
        }

        public void Shutdown() { }

        public string GetPlayerName() => IsAvailable ? SteamFriends.GetPersonaName() : "SteamUser";

        // TODO:
        public void CreateFriendsLobby() { /* SteamMatchmaking.CreateLobby */ }
        public void JoinLobby(CSteamID lobbyId) { /* SteamMatchmaking.JoinLobby */ }
    }
}
