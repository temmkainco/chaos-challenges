using Networking;
using Platform;
using System;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

public class HostLobbyPanel : PanelBase
{
    [SerializeField] private Button _closeButton;
    [SerializeField] private Button _hostButton;
    [SerializeField] private Toggle _isPublicToggle;
    [SerializeField] private Button _fewerPlayers;
    [SerializeField] private Button _morePlayers;
    [SerializeField] private TMPro.TMP_Text _maxPlayersText;
    [Inject] private LoadingPanel _loadingPanel;
    [Inject] private MessagePanel _messagePanel;

    private MainMenuController _controller;
    private int _maxPlayers = SessionSettings.MAX_PLAYERS;

    [Inject]
    public void Construct(MainMenuController controller)
    {
        _controller = controller;
    }

    private void OnEnable()
    {
        _closeButton.onClick.AddListener(Close);
        _hostButton.onClick.AddListener(OnHostButtonClicked);
        if (_fewerPlayers != null) _fewerPlayers.onClick.AddListener(Fewer);
        if (_morePlayers != null) _morePlayers.onClick.AddListener(More);
        ShowMaxPlayers();
    }

    private void Fewer() { _maxPlayers = Mathf.Max(SessionSettings.MIN_PLAYERS, _maxPlayers - 1); ShowMaxPlayers(); }
    private void More() { _maxPlayers = Mathf.Min(SessionSettings.MAX_PLAYERS, _maxPlayers + 1); ShowMaxPlayers(); }

    private void ShowMaxPlayers()
    {
        if (_maxPlayersText != null) _maxPlayersText.text = $"Max players: {_maxPlayers}";
    }

    private void OnDisable()
    {
        _closeButton.onClick.RemoveListener(Close);
        _hostButton.onClick.RemoveListener(OnHostButtonClicked);
        if (_fewerPlayers != null) _fewerPlayers.onClick.RemoveListener(Fewer);
        if (_morePlayers != null) _morePlayers.onClick.RemoveListener(More);
    }

    private async void OnHostButtonClicked()
    {
        bool isPublic = _isPublicToggle.isOn;
        Close();

        _loadingPanel.Open();

        try
        {
            string code = await _controller.OnHostLobbyButtonClicked(isPublic, _maxPlayers);

            if (string.IsNullOrEmpty(code))
            {
                Debug.LogWarning("Failed to host lobby.");
                _messagePanel.Show(_controller.LastError ?? "Could not create a lobby.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error hosting lobby: {e.Message}");
            _messagePanel.Show("Could not create a lobby. Check your connection.");
        }
        finally
        {
            _loadingPanel.Close();
        }
    }
}
