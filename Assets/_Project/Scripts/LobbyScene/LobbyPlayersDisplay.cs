using Fusion;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class LobbyPlayersDisplay : NetworkBehaviour
{
    [SerializeField] private Transform _playersDisplayItemsParent;
    [Inject] private NetworkPlayerSpawner _spawner;

    private LobbyPlayerDisplayItem[] _items;
    private readonly List<Player> _currentPlayers = new();

    private const string DEFAULT_NAME = "Some Dude";

    private void Awake()
    {
        _items = _playersDisplayItemsParent.GetComponentsInChildren<LobbyPlayerDisplayItem>(true);
        _spawner.OnPlayersChangedEvent += RefreshFromDictionary;
    }

    private void OnDestroy()
    {
        _spawner.OnPlayersChangedEvent -= RefreshFromDictionary;
    }

    private void RefreshFromDictionary()
    {
        _currentPlayers.Clear();

        foreach (var kv in _spawner.Players)
        {
            var player = kv.Value;
            _currentPlayers.Add(player);
            player.OnNicknameUpdated += OnPlayerNicknameUpdated;
        }

        RefreshUI();
    }

    private void RefreshUI()
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
    private void OnPlayerNicknameUpdated(string newNickname)
    {
        RefreshUI(); 
    }
}
