using Networking;
using TMPro;
using UnityEngine;
using Zenject;

public class LobbyCodeDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text _lobbyCodeText;
    [Inject] private INetworkRunner _networkRunner;
    void Start()
    {
        string lobbyCode = _networkRunner.CurrentLobbyCode;
        _lobbyCodeText.text = lobbyCode;
    }
}
