using Networking;
using Zenject;

public class MainMenuController
{
    [Inject] private INetworkRunner _networkRunner;

    public void OnHostButtonClicked()
    {
        if (_networkRunner != null)
        {
            _networkRunner.Host("MyRoom");
        }
    }

    public void OnJoinButtonClicked()
    {
        if (_networkRunner != null)
        {
            _networkRunner.Join("MyRoom");
        }
    }
}
