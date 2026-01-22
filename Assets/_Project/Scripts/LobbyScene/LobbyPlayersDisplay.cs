using Fusion;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class LobbyPlayersDisplay : NetworkBehaviour
{
    [SerializeField] private Transform _playersDisplayItemsParent;
    [Inject] private LobbyPlayerSpawner _spawner;
    [Inject] private LobbyManager _manager;

    private LobbyPlayerDisplayItem[] _items;
    private readonly List<Player> _currentPlayers = new();

    private const string DEFAULT_NAME = "Some Dude";
    private const string READY_TEXT = "READY";
    private const string NOT_READY_TEXT = "NOT READY";
    [SerializeField] private Color _readyTextColor = Color.green;
    [SerializeField] private Color _notReadyTextColor = Color.red;

    private void Awake()
    {
        _items = _playersDisplayItemsParent.GetComponentsInChildren<LobbyPlayerDisplayItem>(true);
        _spawner.OnPlayersChangedEvent += On_PlayersChanged;
        _manager.PlayerReadyChangedEvent += On_PlayerReadyChanged;
    }

    private void OnDestroy()
    {
        _spawner.OnPlayersChangedEvent -= On_PlayersChanged;
        _manager.PlayerReadyChangedEvent -= On_PlayerReadyChanged;
    }

    private void On_PlayersChanged()
    {
        _currentPlayers.Clear();

        foreach (var kv in _spawner.Players)
        {
            var player = kv.Value;
            _currentPlayers.Add(player);
            player.OnNicknameUpdated += On_PlayerNicknameUpdated;
        }

        Refresh();
    }

    private void On_PlayerReadyChanged(PlayerRef playerRef, bool isReady)
    {
        for (int i = 0; i < _currentPlayers.Count && i < _items.Length; i++)
        {
            Debug.Log($"Checking player at index {i}: {_currentPlayers[i].Object.InputAuthority} against {playerRef}");
            if (_currentPlayers[i].Object.InputAuthority == playerRef)
            {
                _items[i].PlayerReady_TMP.text = isReady ? READY_TEXT : NOT_READY_TEXT;
                _items[i].PlayerReady_TMP.color = isReady ? _readyTextColor : _notReadyTextColor;
                break;
            }
        }
    }

    private void Refresh()
    {
        foreach (var item in _items)
        {
            item.PlayerNickname_TMP.text = DEFAULT_NAME;
            item.PlayerAvatar_Image.gameObject.SetActive(false);
        }

        for (int i = 0; i < _currentPlayers.Count && i < _items.Length; i++)
        {
            _items[i].PlayerNickname_TMP.text = _currentPlayers[i].Nickname;
            _items[i].PlayerAvatar_Image.gameObject.SetActive(true);
        }
    }

    private void On_PlayerNicknameUpdated(string newNickname)
    {
        Refresh(); 
    }
}
