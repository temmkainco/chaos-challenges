using Networking;
using TMPro;
using UnityEngine;
using Zenject;

/// <summary>Lobby screen HUD: room code and how to start.</summary>
public class LobbyHud : MonoBehaviour
{
    [SerializeField] private TMP_Text _code;
    [SerializeField] private TMP_Text _help;

    [Inject] private INetworkRunner _networkRunner;

    private string _last;

    private void Update()
    {
        string help = "Everyone in? Press <b>E</b> on the big red button to get ready.\n" +
                      "<size=80%>The show starts when all contestants are ready. Invite friends through the Steam overlay (Shift+Tab).</size>";
        if (help != _last)
        {
            _last = help;
            _help.text = help;
        }

        string code = _networkRunner?.CurrentLobbyCode;
        _code.text = string.IsNullOrEmpty(code) ? "" : $"<size=55%>ROOM CODE</size>\n<color=#E8590C>{code}</color>";
    }
}
