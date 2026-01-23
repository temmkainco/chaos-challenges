using Steamworks;
using UnityEngine;
using Networking;
using Zenject;
using Cysharp.Threading.Tasks;

public class SteamInviteListener : MonoBehaviour
{
    private INetworkRunner _networkRunner;
    private Callback<GameLobbyJoinRequested_t> _gameLobbyJoinRequested;
    private Callback<LobbyDataUpdate_t> _lobbyDataUpdate;
    private CSteamID _pendingLobby;

    private LoadingPanel _loadingPanel;

    [Inject]
    public void Construct(INetworkRunner runner, LoadingPanel loadingPanel)
    {
        _networkRunner = runner;
        _loadingPanel = loadingPanel;
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
        _loadingPanel.Open();
        CSteamID lobby = data.m_steamIDLobby;

        var runner = _networkRunner as FusionNetworkRunner;
        if (lobby == runner.CurrentSteamLobby)
        {
            _loadingPanel.Close();
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
        }
    }

    private async void OnLobbyDataUpdate(LobbyDataUpdate_t data)
    {
        CSteamID lobby = new CSteamID(data.m_ulSteamIDLobby);

        if (_pendingLobby != lobby) return;

        string fusionCode = SteamMatchmaking.GetLobbyData(lobby, "FusionCode");
        if (string.IsNullOrEmpty(fusionCode))
        {
            _loadingPanel.Close();
            return;
        }

        _pendingLobby = default;
        await JoinFusionSession(fusionCode);
    }

    private async UniTask JoinFusionSession(string fusionCode)
    {
        if (!_networkRunner.IsConnected)
            await _networkRunner.InitAsync();

        await _networkRunner.JoinByCodeAsync(fusionCode);
    }
}
