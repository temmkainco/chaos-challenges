using Fusion;
using System.Collections.Generic;
using UnityEngine;
using Zenject;

public class LobbyPlayersDisplay : NetworkBehaviour
{
    [SerializeField] private Transform _playersDisplayItemsParent;
    // Also used outside the lobby (e.g. the TV in the Mines scene), where there are no ready states.
    [Inject] private BasePlayerSpawner _spawner;
    [InjectOptional] private LobbyManager _manager;

    private LobbyPlayerDisplayItem[] _items;
    private readonly List<Player> _currentPlayers = new();

    private const string DEFAULT_NAME = "Waiting for";
    private const string READY_TEXT = "READY";
    private const string NOT_READY_TEXT = "NOT READY";
    [SerializeField] private Color _readyTextColor = Color.green;
    [SerializeField] private Color _notReadyTextColor = Color.red;

    private void Awake()
    {
        _items = _playersDisplayItemsParent.GetComponentsInChildren<LobbyPlayerDisplayItem>(true);
        // Long nicknames shrink to fit instead of running off the TV.
        foreach (var item in _items)
            if (item != null && item.PlayerNickname_TMP != null)
            {
                item.PlayerNickname_TMP.fontSizeMax = item.PlayerNickname_TMP.fontSize;
                item.PlayerNickname_TMP.fontSizeMin = 10f;
                item.PlayerNickname_TMP.enableWordWrapping = false;
                item.PlayerNickname_TMP.enableAutoSizing = true;
            }
    }

    private void OnEnable()
    {
        _spawner.OnPlayersChangedEvent += On_PlayersChanged;
        if (_manager != null)
            _manager.PlayerReadyChangedEvent += On_PlayerReadyChanged;
    }

    private void OnDisable()
    {
        if (_spawner != null)
            _spawner.OnPlayersChangedEvent -= On_PlayersChanged;
        if (_manager != null)
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
            var item = _items[i];
            if (item == null || item.PlayerReady_TMP == null)
                continue;

            var player = _currentPlayers[i];
            if (player == null)
                continue;

            var playerObject = player.Object;
            if (playerObject == null)
                continue;

            if (playerObject.InputAuthority == playerRef)
            {
                item.PlayerReady_TMP.text = isReady ? READY_TEXT : NOT_READY_TEXT;
                item.PlayerReady_TMP.color = isReady ? _readyTextColor : _notReadyTextColor;
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

    private float _nextReadySync;

    /// <summary>The ready labels follow the replicated states (the change events alone missed late joiners and reorders).</summary>
    private void Update()
    {
        if (_manager == null || _manager.Object == null || !_manager.Object.IsValid || Time.unscaledTime < _nextReadySync) return;
        _nextReadySync = Time.unscaledTime + 0.25f;
        for (int i = 0; i < _items.Length; i++)
        {
            var item = _items[i];
            if (item == null || item.PlayerReady_TMP == null) continue;
            var player = i < _currentPlayers.Count ? _currentPlayers[i] : null;
            if (player == null || player.Object == null || !player.Object.IsValid)
            {
                if (item.PlayerReady_TMP.text.Length > 0) item.PlayerReady_TMP.text = "";
                continue;
            }
            bool ready = _manager.ReadyStates.TryGet(player.Object.InputAuthority, out var value) && value;
            string text = ready ? READY_TEXT : NOT_READY_TEXT;
            if (item.PlayerReady_TMP.text != text)
            {
                item.PlayerReady_TMP.text = text;
                item.PlayerReady_TMP.color = ready ? _readyTextColor : _notReadyTextColor;
            }
        }
    }

    private void On_PlayerNicknameUpdated(string newNickname)
    {
        Refresh(); 
    }
}
