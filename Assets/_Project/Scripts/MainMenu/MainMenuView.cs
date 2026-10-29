using Zenject;
using UnityEngine;
using UnityEngine.UI;
using Platform;
using TMPro;
using System;

public class MainMenuView : MonoBehaviour
{
    [SerializeField] private TMP_InputField _playerNameInput;
    [SerializeField] private Button _hostButton;
    [SerializeField] private Button _joinRandomButton;
    [SerializeField] private Button _joinByCodeButton;
    [SerializeField] private Button _exitButton;

    [SerializeField] private PanelBase _joinLobbyByCodePanel;
    [SerializeField] private PanelBase _hostLobbyPanel;

    [Inject] private LoadingPanel _loadingPanel;
    [Inject] private SteamInviteListener _steamInviteListener;
    [Inject] private Networking.INetworkRunner _networkRunner;
    [Inject] private MessagePanel _messagePanel;

    private MainMenuController _controller;
    private IPlatformService _platformService;

    [Inject]
    public void Construct(MainMenuController controller, IPlatformService platformService)
    {
        _controller = controller;
        _platformService = platformService;
    }

    private void OnEnable()
    {
        _joinRandomButton.onClick.AddListener(OnJoinRandomButtonClicked);
        _hostButton.onClick.AddListener(_hostLobbyPanel.Open);
        _joinByCodeButton.onClick.AddListener(_joinLobbyByCodePanel.Open);
        _exitButton.onClick.AddListener(_controller.OnExitButtonClicked);

        _platformService.Init();
        _playerNameInput.interactable = _platformService is LocalPlatformService;
        _playerNameInput.text = _platformService.GetPlayerName();
        _playerNameInput.onEndEdit.AddListener(OnPlayerNameInputFieldChanged);

        // The menu can become active before or after the disconnect is reported, so handle both.
        _networkRunner.Disconnected += OnDisconnected;
        ShowDisconnectReason();
    }

    private void OnDisable()
    {
        if (_networkRunner != null)
            _networkRunner.Disconnected -= OnDisconnected;
    }

    private void OnDisconnected(string reason) => ShowDisconnectReason();

    private void ShowDisconnectReason()
    {
        string disconnectReason = _networkRunner.ConsumeDisconnectReason();
        if (disconnectReason != null)
        {
            Debug.LogWarning($"Returned to menu: {disconnectReason}");
            _messagePanel.Show(disconnectReason);
        }
    }

    private void OnPlayerNameInputFieldChanged(string newName)
    {
        if (_platformService is LocalPlatformService local)
        {
            PlayerPrefs.SetString("LocalPlayerName", newName);
        }
    }

    private async void OnJoinRandomButtonClicked()
    {
        _loadingPanel.Open();

        try
        {
            bool success = await _controller.OnJoinRandomButtonClicked();

            if (!success)
            {
                Debug.LogWarning("Failed to join random lobby.");
                _messagePanel.Show(_controller.LastError ?? "Could not find a game to join.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error joining random lobby: {e.Message}");
            _messagePanel.Show("Could not join a game. Check your connection.");
        }
        finally
        {
            _loadingPanel.Close();
        }
    }

    private void OnDestroy()
    {
        _playerNameInput.onEndEdit.RemoveListener(OnPlayerNameInputFieldChanged);

        _hostButton.onClick.RemoveListener(_hostLobbyPanel.Open);
        _joinRandomButton.onClick.RemoveListener(OnJoinRandomButtonClicked);
        _joinByCodeButton.onClick.RemoveListener(_joinLobbyByCodePanel.Open);
        _exitButton.onClick.RemoveListener(_controller.OnExitButtonClicked);
    }
}
