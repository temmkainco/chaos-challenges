using UnityEngine;
using Steamworks;

public class SteamTest : MonoBehaviour
{
    void Start()
    {
        // Check if SteamManager initialized Steam successfully
        if (!SteamManager.Initialized)
        {
            Debug.LogError("Steam is not initialized! Make sure Steam is running and steam_appid.txt is correct.");
            return;
        }

        // Get and log the player's Steam nickname
        string playerName = SteamFriends.GetPersonaName();
        Debug.Log($"Steam initialized! Player Nickname: {playerName}");
    }
}
