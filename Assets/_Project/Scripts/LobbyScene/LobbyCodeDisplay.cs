using Networking;
using TMPro;
using UnityEngine;
using Zenject;

/// <summary>Shows the lobby code on the lobby TV. Friends are invited through the Steam overlay.</summary>
public class LobbyCodeDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text _lobbyCodeText;
    [Inject] private INetworkRunner _networkRunner;

    private void Start()
    {
        _lobbyCodeText.text = _networkRunner.CurrentLobbyCode;
    }
}
