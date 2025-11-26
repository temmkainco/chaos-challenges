using Networking;
using Platform;
using Steamworks;
using UnityEngine;
using Zenject;

public class MainMenuController
{
    private readonly INetworkRunner _networkRunner;
    private readonly IPlatformService _platformService;

    [Inject]
    public MainMenuController(INetworkRunner networkRunner, IPlatformService platformService)
    {
        _networkRunner = networkRunner;
        _platformService = platformService;
    }

    public async void OnHostLobbyButtonClicked(bool isPublic)
    {
        int maxPlayers = 8;
        string code = await _networkRunner.CreateLobbyAsync(maxPlayers, isPublic);

        Debug.LogError($"HOST CREATED LOBBY WITH CODE: {code}, Public: {isPublic}");
        var runner = _networkRunner as NetworkRunner;
        Debug.LogError($"CURRENT STEAM LOBBY: {runner.CurrentSteamLobby} | Fusion code: {SteamMatchmaking.GetLobbyData(runner.CurrentSteamLobby, "FusionCode")}");

    }

    public async void OnJoinRandomButtonClicked()
    {
        bool ok = await _networkRunner.JoinRandomPublicAsync();
        Debug.Log("JOIN RANDOM = " + ok);
    }

    public async void OnJoinByCodeButtonClicked(string code)
    {
        if (string.IsNullOrEmpty(code))
            return;

        bool ok = await _networkRunner.JoinByCodeAsync(code);

        Debug.Log($"JOIN BY CODE ({code}) RESULT = {ok}");
    }
}
