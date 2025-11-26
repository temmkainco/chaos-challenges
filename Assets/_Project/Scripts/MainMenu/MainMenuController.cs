using Cysharp.Threading.Tasks;
using Networking;
using Platform;
using Zenject;

public class MainMenuController
{
    private readonly INetworkRunner _networkRunner;

    [Inject]
    public MainMenuController(INetworkRunner networkRunner)
    {
        _networkRunner = networkRunner;
    }

    public async UniTask<string> OnHostLobbyButtonClicked(bool isPublic)
    {
        int maxPlayers = 8;
        string code = await _networkRunner.CreateLobbyAsync(maxPlayers, isPublic);
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
}
