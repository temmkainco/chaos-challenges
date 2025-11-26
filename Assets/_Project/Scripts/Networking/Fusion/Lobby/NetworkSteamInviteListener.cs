using Steamworks;
using UnityEngine;
using Networking;
using Zenject;
using Cysharp.Threading.Tasks;

public class NetworkSteamInviteListener : MonoBehaviour
{
    private INetworkRunner _networkRunner;
    private Callback<GameLobbyJoinRequested_t> _gameLobbyJoinRequested;
    private Callback<LobbyDataUpdate_t> _lobbyDataUpdate;

    private CSteamID _pendingLobby;

    [Inject]
    public void Construct(INetworkRunner runner)
    {
        _networkRunner = runner;
    }

    private void OnEnable()
    {
        if (SteamManager.Initialized)
        {
            _gameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
            _lobbyDataUpdate = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
        }
    }

    private void OnDisable()
    {
        _gameLobbyJoinRequested?.Unregister();
        _lobbyDataUpdate?.Unregister();
        _pendingLobby = default;
    }

    private async void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t data)
    {
        Debug.LogError("ON GAME LOBBY JOIN REQUESTED CALLBACK TRIGGERED");

        CSteamID lobby = data.m_steamIDLobby;
        var runner = _networkRunner as FusionNetworkRunner;

        if (lobby == runner.CurrentSteamLobby)
        {
            Debug.Log("[Steam] Host entered own lobby, ignoring invite callback.");
            return;
        }

        string fusionCode = SteamMatchmaking.GetLobbyData(lobby, "FusionCode");

        if (!string.IsNullOrEmpty(fusionCode))
        {
            await JoinFusionSession(fusionCode);
        }
        else
        {
            _pendingLobby = lobby;
            SteamMatchmaking.RequestLobbyData(lobby);
            Debug.LogError("[Steam] FusionCode not yet available, waiting for LobbyDataUpdate...");
        }
    }

    private async void OnLobbyDataUpdate(LobbyDataUpdate_t data)
    {
        CSteamID lobby = new CSteamID(data.m_ulSteamIDLobby);

        if (_pendingLobby != lobby) return;

        string fusionCode = SteamMatchmaking.GetLobbyData(lobby, "FusionCode");
        if (string.IsNullOrEmpty(fusionCode)) return;

        Debug.LogError($"[Steam] LobbyDataUpdate received. Steam Lobby ID: {lobby} | FusionCode: {fusionCode}");
        _pendingLobby = default;
        await JoinFusionSession(fusionCode);
    }

    private async UniTask JoinFusionSession(string fusionCode)
    {
        Debug.LogError("[Steam] Joining Fusion session via invite: " + fusionCode);

        if (!_networkRunner.IsConnected)
            await _networkRunner.InitAsync();

        await _networkRunner.JoinByCodeAsync(fusionCode);
    }
}
