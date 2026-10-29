using Cysharp.Threading.Tasks;
using Networking;
using Steamworks;
using System;
using UnityEngine;
using Zenject;

/// <summary>
/// Routes every way of joining a friend through Steam into <see cref="FusionNetworkRunner.JoinSteamLobbyAsync"/>:
/// accepting an overlay invite or "Join Game" while the game runs, and launching the game from either
/// (Steam passes "+connect_lobby &lt;id&gt;" on the command line).
/// </summary>
public class SteamInviteListener : MonoBehaviour
{
    private FusionNetworkRunner _networkRunner;
    private LoadingPanel _loadingPanel;
    private MessagePanel _messagePanel;

    private Callback<GameLobbyJoinRequested_t> _lobbyJoinRequested;
    private Callback<GameRichPresenceJoinRequested_t> _richPresenceJoinRequested;
    private Callback<GameOverlayActivated_t> _overlayActivated;

    private bool _isJoining;
    private bool _cursorWasVisible;
    private CursorLockMode _cursorLockMode;

    [Inject]
    public void Construct(INetworkRunner runner, LoadingPanel loadingPanel, MessagePanel messagePanel)
    {
        _networkRunner = runner as FusionNetworkRunner;
        _loadingPanel = loadingPanel;
        _messagePanel = messagePanel;
    }

    private void OnEnable()
    {
        if (!SteamManager.Initialized)
            return;

        _lobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(data => JoinAsync(data.m_steamIDLobby).Forget());
        _richPresenceJoinRequested = Callback<GameRichPresenceJoinRequested_t>.Create(OnRichPresenceJoinRequested);
        _overlayActivated = Callback<GameOverlayActivated_t>.Create(OnOverlayActivated);
    }

    private void OnDisable()
    {
        _lobbyJoinRequested?.Unregister();
        _richPresenceJoinRequested?.Unregister();
        _overlayActivated?.Unregister();
    }

    /// <summary>Called once the main menu is up: joins the lobby if the game was launched from an invite.</summary>
    public void HandleLaunchArguments()
    {
        if (SteamManager.Initialized && SteamLobby.TryParseConnect(Environment.GetCommandLineArgs(), out var lobby))
        {
            Debug.Log($"[Steam] Launched to join lobby {lobby}");
            JoinAsync(lobby).Forget();
        }
    }

    private void OnRichPresenceJoinRequested(GameRichPresenceJoinRequested_t data)
    {
        if (SteamLobby.TryParseConnect(data.m_rgchConnect, out var lobby))
            JoinAsync(lobby).Forget();
        else
            Debug.LogWarning($"[Steam] Ignoring join request with unknown connect string '{data.m_rgchConnect}'");
    }

    private async UniTaskVoid JoinAsync(CSteamID lobby)
    {
        if (_isJoining || _networkRunner == null)
            return;

        _isJoining = true;
        _messagePanel.Close();
        _loadingPanel.Open();
        try
        {
            if (!await _networkRunner.JoinSteamLobbyAsync(lobby))
                _messagePanel.Show(_networkRunner.LastError ?? "Could not join your friend's game.");
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            _messagePanel.Show("Could not join your friend's game.");
        }
        finally
        {
            _loadingPanel.Close();
            _isJoining = false;
        }
    }

    /// <summary>The overlay needs a free cursor; gameplay hides and locks it.</summary>
    private void OnOverlayActivated(GameOverlayActivated_t data)
    {
        if (data.m_bActive != 0)
        {
            _cursorWasVisible = Cursor.visible;
            _cursorLockMode = Cursor.lockState;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            Cursor.visible = _cursorWasVisible;
            Cursor.lockState = _cursorLockMode;
        }
    }
}
