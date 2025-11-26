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
                Debug.LogError("Failed to host lobby.");
                // TODO: Show an error message (e.g., ErrorPanel.Open("Failed to create lobby."))
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error hosting lobby: {e.Message}");
            // TODO: Show a connection error message
        }
        finally
        {
            _loadingPanel.Close();
        }
    }
}
