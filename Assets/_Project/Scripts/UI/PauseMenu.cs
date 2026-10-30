using Networking;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Zenject;

/// <summary>
/// Esc menu while connected (Resume / Settings / Leave), plus cursor handling for the whole game:
/// the cursor is captured while you control a character and free in menus, the intermission and results.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    [SerializeField] private GameObject _root;
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _leaveButton;
    [SerializeField] private PanelBase _settingsPanel;

    [Inject] private INetworkRunner _networkRunner;
    [InjectOptional] private MessagePanel _messagePanel;

    private void Awake()
    {
        _resumeButton.onClick.AddListener(Close);
        _settingsButton.onClick.AddListener(() => _settingsPanel.Open());
        _leaveButton.onClick.AddListener(Leave);
        _root.SetActive(false);
    }

    private void Update()
    {
        var keyboard = Keyboard.current;
        bool connected = _networkRunner != null && _networkRunner.IsConnected;
        if (!connected && IsOpen)
            Close();

        if (connected && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            if (_settingsPanel.gameObject.activeSelf) _settingsPanel.Close();
            else if (IsOpen) Close();
            else Open();
        }

        UpdateCursor();
    }

    private void UpdateCursor()
    {
        bool messageOpen = _messagePanel != null && _messagePanel.gameObject.activeSelf;
        bool playing = Player.Local != null && !IsOpen && !messageOpen && Application.isFocused && !SteamInviteListener.OverlayActive
                       && SceneManager.GetActiveScene().path != GameScenes.Intermission && !RulesCard.WantsCursor && !GamePackPanel.IsOpen;
        var lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
        if (Cursor.lockState != lockState) Cursor.lockState = lockState;
        if (Cursor.visible == playing) Cursor.visible = !playing;
    }

    private void Open()
    {
        IsOpen = true;
        _root.SetActive(true);
    }

    private void Close()
    {
        IsOpen = false;
        _settingsPanel.Close();
        _root.SetActive(false);
    }

    private void Leave()
    {
        Close();
        _networkRunner.Leave();
    }
}
