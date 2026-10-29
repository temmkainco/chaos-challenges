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
    [Inject] private LoadingPanel _loadingPanel;
    [Inject] private MessagePanel _messagePanel;

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

    private async void OnHostButtonClicked()
    {
        bool isPublic = _isPublicToggle.isOn;
        Close();

        _loadingPanel.Open();

        try
        {
            string code = await _controller.OnHostLobbyButtonClicked(isPublic);

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
