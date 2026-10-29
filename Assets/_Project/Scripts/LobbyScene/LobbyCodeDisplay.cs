using Networking;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

/// <summary>Shows the lobby code and, when Steam is available, lets players open the Steam invite dialog.</summary>
public class LobbyCodeDisplay : MonoBehaviour
{
    private const Key INVITE_KEY = Key.I;

    [SerializeField] private TMP_Text _lobbyCodeText;
    [Inject] private INetworkRunner _networkRunner;

    private bool _canInvite;

    private void Start()
    {
        Refresh();
    }

    private void Update()
    {
        // The Steam lobby is created right after the session, so inviting can become available a moment later.
        if (_networkRunner.CanInviteFriends != _canInvite)
            Refresh();

        var keyboard = Keyboard.current;
        if (_canInvite && keyboard != null && keyboard[INVITE_KEY].wasPressedThisFrame)
            _networkRunner.InviteFriends();
    }

    private void Refresh()
    {
        _canInvite = _networkRunner.CanInviteFriends;
        string code = _networkRunner.CurrentLobbyCode;
        _lobbyCodeText.text = _canInvite
            ? $"{code}\n<size=35%>Press {INVITE_KEY} to invite Steam friends</size>"
            : code;
    }
}
