using Networking;
using Platform;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class JoinLobbyByCodePanel : PanelBase
{
    [SerializeField] private TMP_InputField _lobbyCodeInput;
    [SerializeField] private Button _joinButton;
    [SerializeField] private Button _closeButton;

    private MainMenuController _controller;

    private string _lobbyCode = string.Empty;
    private bool _suppressCallback;

    [Inject]
    public void Construct(MainMenuController controller)
    {
        _controller = controller;
    }

    private void OnEnable()
    {
        _lobbyCodeInput.onSubmit.AddListener(OnLobbyCodeInputFieldSubmit);
        _lobbyCodeInput.onValueChanged.AddListener(OnLobbyCodeInputFieldChanged);

        _closeButton.onClick.AddListener(Close);
        _joinButton.onClick.AddListener(OnJoinButtonClicked);
        _joinButton.interactable = false;
    }

    private void OnDisable()
    {
        _lobbyCodeInput.onSubmit.RemoveListener(OnLobbyCodeInputFieldSubmit);
        _lobbyCodeInput.onValueChanged.RemoveListener(OnLobbyCodeInputFieldChanged);

        _closeButton.onClick.RemoveListener(Close);
        _joinButton.onClick.RemoveListener(OnJoinButtonClicked);
    }

    private void OnLobbyCodeInputFieldChanged(string value)
    {
        if (_suppressCallback)
            return;

        string newValue = value.ToUpper();

        if (newValue.Length > 6)
            newValue = newValue.Substring(0, 6);

        _lobbyCode = newValue;

        if (newValue != value)
        {
            _suppressCallback = true;
            _lobbyCodeInput.text = newValue;
            _suppressCallback = false;
        }

        _joinButton.interactable = newValue.Length >= 2 && newValue.Length <= 6;
    }

    private void OnLobbyCodeInputFieldSubmit(string value)
    {
        _lobbyCode = value.ToUpper();
    }

    private void OnJoinButtonClicked()
    {
        _joinButton.interactable = false;
        _lobbyCodeInput.interactable = false;

        _controller.OnJoinByCodeButtonClicked(_lobbyCode);
    }
}
