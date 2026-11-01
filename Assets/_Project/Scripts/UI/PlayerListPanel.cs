using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>
/// Hold Tab (gamepad: View): everyone in the show at a glance, as player chips (colour, number, name). You keep
/// moving; the camera holds still so the mouse can click. In a round each contestant is in or out (knocked out, in
/// the lounge); in the lobby ready or not. The host also gets KICK per player and, in the lobby, START NOW. Lives for
/// the whole session (DontDestroyOnLoad), built with the UI kit.
/// </summary>
public class PlayerListPanel : MonoBehaviour
{
    private const int MAX_ROWS = 8;
    private const float WIDTH = 840f, ROW = 72f;

    public static bool IsOpen { get; private set; }

    private RectTransform _card;
    private CanvasGroup _group;
    private TextMeshProUGUI _title, _subtitle;
    private readonly List<(GameObject root, PlayerChip chip, TextMeshProUGUI status, UiButton kick)> _rows = new();
    private readonly List<Player> _players = new();
    private UiButton _startButton;
    private float _nextRefresh;
    private BasePlayerSpawner _spawner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var go = new GameObject("PlayerList");
        DontDestroyOnLoad(go);
        go.AddComponent<PlayerListPanel>();
    }

    private void Awake()
    {
        var safe = Ui.Canvas("Player List", 150, transform);
        var box = Ui.Box(safe, "Card", UiTheme.Tone.Studio);
        _card = Ui.Place((RectTransform)box.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WIDTH, 300f));
        _group = box.gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;

        _title = Ui.Text(_card, "CONTESTANTS", UiTheme.TextRole.Title, UiTheme.Tone.Paper, TextAlignmentOptions.Left, true);
        Ui.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(36f, -24f), new Vector2(WIDTH - 260f, 56f));
        _subtitle = Ui.Text(_card, "", UiTheme.TextRole.Body, UiTheme.Tone.Haze, TextAlignmentOptions.Right);
        Ui.Place(_subtitle.rectTransform, new Vector2(1f, 1f), new Vector2(-36f, -28f), new Vector2(220f, 48f));

        for (int i = 0; i < MAX_ROWS; i++)
        {
            var row = Ui.Rect(_card, "Row " + i);
            Ui.Place(row, new Vector2(0.5f, 1f), new Vector2(0f, -100f - i * (ROW + 12f)), new Vector2(WIDTH - 72f, ROW));
            var chip = PlayerChip.Create(row, ROW);
            Ui.Place((RectTransform)chip.transform, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(400f, ROW));
            var status = Ui.Text(row, "", UiTheme.TextRole.Body, UiTheme.Tone.Haze, TextAlignmentOptions.Left);
            status.fontStyle = FontStyles.Bold;
            Ui.Place(status.rectTransform, new Vector2(0f, 0.5f), new Vector2(416f, 0f), new Vector2(160f, ROW));
            int index = i;
            var kick = Ui.Button(row, "KICK", UiButton.Variant.Danger, null, () => Kick(index));
            Ui.Place((RectTransform)kick.transform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(130f, 64f));
            _rows.Add((row.gameObject, chip, status, kick));
        }

        _startButton = Ui.Button(_card, "START NOW", UiButton.Variant.Primary, "play", ForceStart);
        Ui.Place((RectTransform)_startButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(360f, 80f));
    }

    private static bool IsHost =>
        NetworkGameManager.Instance != null && NetworkGameManager.Instance.Object != null && NetworkGameManager.Instance.Object.IsValid
        && NetworkGameManager.Instance.HasStateAuthority;

    private void Update()
    {
        bool inSession = Player.Local != null;
        // Held, not toggled (Shift+Tab is Steam's overlay).
        var keyboard = Keyboard.current;
        bool held = inSession && !PauseMenu.IsOpen && !GamePackPanel.IsOpen
                    && (UiInput.Held(Key.Tab) && (keyboard == null || !keyboard.shiftKey.isPressed) || UiInput.Held(Key.None, GamepadButton.Select));
        if (held != IsOpen)
            Show(held);
        if (IsOpen && Time.unscaledTime >= _nextRefresh)
        {
            _nextRefresh = Time.unscaledTime + 0.25f;
            Refresh();
        }
    }

    private Unity.Cinemachine.CinemachineInputAxisController _cameraInput;

    private void Show(bool open)
    {
        IsOpen = open;
        // The camera holds still while the list is up, so the freed mouse can click KICK / START NOW.
        if (open)
        {
            var player = Player.Local;
            _cameraInput = player != null && player.Camera != null ? player.Camera.GetComponentInChildren<Unity.Cinemachine.CinemachineInputAxisController>(true) : null;
            if (_cameraInput != null) _cameraInput.enabled = false;
        }
        else if (_cameraInput != null)
        {
            _cameraInput.enabled = true;
            _cameraInput = null;
        }
        _group.blocksRaycasts = open;
        _group.DOKill();
        _group.DOFade(open ? 1f : 0f, UiTheme.Instance.PanelOut).SetUpdate(true).SetLink(gameObject);
        if (open)
        {
            _nextRefresh = 0f;
            Refresh();
            if (!UiTheme.ReduceMotion)
            {
                _card.DOKill(true);
                _card.localScale = Vector3.one * 0.94f;
                _card.DOScale(1f, UiTheme.Instance.PanelIn).SetEase(Ease.OutBack).SetUpdate(true).SetLink(gameObject);
            }
            // Gamepad: Start Now is one press of A away while View is held.
            if (_startButton.gameObject.activeSelf && EventSystem.current != null && PromptBar.UsingGamepad)
                EventSystem.current.SetSelectedGameObject(_startButton.gameObject);
        }
        else if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null
                 && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(transform))
            EventSystem.current.SetSelectedGameObject(null);
    }

    private void Refresh()
    {
        if (_spawner == null || _spawner.Object == null) _spawner = FindFirstObjectByType<BasePlayerSpawner>();
        _players.Clear();
        if (_spawner != null && _spawner.Object != null && _spawner.Object.IsValid)
            foreach (var entry in _spawner.Players)
                if (entry.Value != null && entry.Value.Object != null) _players.Add(entry.Value);
        _players.Sort((a, b) => a.Object.InputAuthority.PlayerId.CompareTo(b.Object.InputAuthority.PlayerId));

        var lobby = FindFirstObjectByType<LobbyManager>();
        bool inLobby = lobby != null && lobby.Object != null && lobby.Object.IsValid;
        var minigame = MinigameController.Current;
        var match = NetworkGameManager.Instance;
        bool host = IsHost;
        _title.text = !inLobby && minigame != null && minigame.Definition != null ? minigame.Definition.DisplayName.ToUpperInvariant() : "CONTESTANTS";

        int good = 0;
        var theme = UiTheme.Instance;
        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            bool used = i < _players.Count;
            row.root.SetActive(used);
            if (!used) continue;
            var player = _players[i];
            var who = player.Object.InputAuthority;
            string name = string.IsNullOrEmpty(player.Nickname) ? $"Player {who.PlayerId}" : player.Nickname;
            if (who == player.Runner.LocalPlayer) name += " (you)";
            int colour = match != null && match.Object != null && match.Object.IsValid ? match.ColorIndexOf(who) : -1;
            bool on = inLobby ? lobby.ReadyStates.TryGet(who, out var ready) && ready : !player.IsSpectator;
            if (on) good++;
            row.chip.Set(colour >= 0 ? colour + 1 : who.PlayerId, player.PlayerColor, name, on ? "tick" : inLobby ? null : "cross");
            row.status.text = inLobby ? (on ? "Ready" : "Not ready") : (on ? "In" : "Out");
            row.status.color = theme.Color(on ? UiTheme.Tone.Good : inLobby ? UiTheme.Tone.Haze : UiTheme.Tone.Bad);
            row.kick.gameObject.SetActive(host && who != player.Runner.LocalPlayer);
        }
        _subtitle.text = _players.Count == 0 ? "" : inLobby ? $"{good}/{_players.Count} ready" : $"{good}/{_players.Count} in";
        _startButton.gameObject.SetActive(host && inLobby);
        float height = 100f + Mathf.Max(1, _players.Count) * (ROW + 12f) + (host && inLobby ? 120f : 24f);
        _card.sizeDelta = new Vector2(WIDTH, height);

        var order = new List<Selectable[]>();
        foreach (var row in _rows) if (row.root.activeSelf && row.kick.gameObject.activeSelf) order.Add(new Selectable[] { row.kick });
        order.Add(new Selectable[] { _startButton });
        UiFocus.Chain(order.ToArray());
    }

    private void Kick(int index)
    {
        if (!IsHost || index >= _players.Count) return;
        var player = _players[index];
        if (player == null || player.Object == null) return;
        Debug.Log($"[Host] kicking {player.Object.InputAuthority}");
        player.Runner.Disconnect(player.Object.InputAuthority);
        Sfx.Play(Sound.Nope);
    }

    private void ForceStart()
    {
        var lobby = FindFirstObjectByType<LobbyManager>();
        if (!IsHost || lobby == null) return;
        lobby.ForceStart();
        Sfx.Play(Sound.Go);
        Show(false);
    }
}
