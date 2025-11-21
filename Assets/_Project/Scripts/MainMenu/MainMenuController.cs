using Networking;
using Platform;
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

    public void OnHostButtonClicked()
    {
        string roomName = _platformService.GetPlayerName() + "_Room";

        _networkRunner.Host(roomName);
    }

    public void OnJoinButtonClicked()
    {
        string roomName = _platformService.GetPlayerName() + "_Room";

        _networkRunner.Join(roomName);
    }
}
