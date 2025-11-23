using Networking;
using Platform;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class HostLobbyPanel : PanelBase
{
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _hostButton;
    [SerializeField] private Toggle _publicToggle;

    private MainMenuController _controller;

    [Inject]
    public void Construct(MainMenuController controller)
    {
        _controller = controller;
    }

    private void OnEnable()
    {
        _closeButton.onClick.AddListener(Close);
        _hostButton.onClick.AddListener(OnHostButtonClicked);
    }

    private void OnDisable()
    {
        _closeButton.onClick.RemoveListener(Close);
        _hostButton.onClick.RemoveListener(OnHostButtonClicked);
    }

    private void OnHostButtonClicked()
    {
        bool isPublic = _publicToggle.isOn;

        _controller.OnHostLobbyButtonClicked(isPublic);
    }
}
