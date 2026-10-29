using Cysharp.Threading.Tasks;
using Networking;
using Platform;
using UnityEngine;
using Zenject;

public class MainMenuController
{
    private readonly INetworkRunner _networkRunner;

    [Inject]
    public MainMenuController(INetworkRunner networkRunner)
    {
        _networkRunner = networkRunner;
    }

    /// <summary>User-facing reason of the last failed host or join.</summary>
    public string LastError => _networkRunner.LastError;

    public async UniTask<string> OnHostLobbyButtonClicked(bool isPublic, int maxPlayers)
    {
        var settings = SessionSettings.Default(isPublic);
        settings.MaxPlayers = Mathf.Clamp(maxPlayers, SessionSettings.MIN_PLAYERS, SessionSettings.MAX_PLAYERS);
        string code = await _networkRunner.CreateLobbyAsync(settings);
        return code;
    }

    public async UniTask<bool> OnJoinRandomButtonClicked()
    {
        bool ok = await _networkRunner.JoinRandomPublicAsync();
        return ok;
    }

    public async UniTask<bool> OnJoinByCodeButtonClicked(string code)
    {
        if (string.IsNullOrEmpty(code))
            return false;

        bool ok = await _networkRunner.JoinByCodeAsync(code);
        return ok;
    }

    public void OnExitButtonClicked()
    {
        _networkRunner.Shutdown();
        Application.Quit();
    }
}
