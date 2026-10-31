using System.Collections.Generic;
using DG.Tweening;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Hold Tab: everyone in the show at a glance (you keep moving; the camera holds still so the mouse can click). In a round each contestant is IN or OUT (knocked out, in the lounge); in the
/// lobby READY or NOT READY. The host also gets a KICK button per player and, in the lobby, START NOW. Lives for the
/// whole session (DontDestroyOnLoad), built from code with <see cref="UiKit"/>.
/// </summary>
public class PlayerListPanel : MonoBehaviour
{
    private const int MAX_ROWS = 8;

    public static bool IsOpen { get; private set; }

    private CanvasGroup _group;
    private RectTransform _card;
    private TMP_Text _title;
    private readonly List<(GameObject root, TMP_Text name, TMP_Text status, Button kick)> _rows = new();
    private readonly List<Player> _players = new();
    private Button _startButton;
    private float _nextRefresh;
    private BasePlayerSpawner _spawner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var go = new GameObject("PlayerList", typeof(RectTransform));
        DontDestroyOnLoad(go);
        go.AddComponent<PlayerListPanel>();
    }

    private void Awake()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        var root = new GameObject("Root", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        var rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = Vector2.zero; rootRect.anchorMax = Vector2.one; rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
        _group = root.GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;

        var card = Image(root.transform, "Card", UiKit.Instance.Panel, UiKit.Cream);
        card.type = UnityEngine.UI.Image.Type.Sliced;
        _card = card.rectTransform;
        Place(_card, 0f, 0f, 760f, 140f + MAX_ROWS * 64f);
        _title = Text(_card, "Title", 48f, UiKit.Orange, true, 0f, _card.sizeDelta.y * 0.5f - 52f, 700f, 64f);
        _title.text = "CONTESTANTS";

        for (int i = 0; i < MAX_ROWS; i++)
        {
            float y = _card.sizeDelta.y * 0.5f - 120f - i * 64f;
            var row = new GameObject("Row " + i, typeof(RectTransform));
            row.transform.SetParent(_card, false);
            Place((RectTransform)row.transform, 0f, y, 700f, 56f);
            var name = Text(row.transform, "Name", 30f, UiKit.Ink, false, -150f, 0f, 380f, 50f);
            name.alignment = TextAlignmentOptions.Left;
            name.enableWordWrapping = false;
            name.enableAutoSizing = true;
            name.fontSizeMin = 14f;
            name.fontSizeMax = 30f;
            var status = Text(row.transform, "Status", 26f, UiKit.Ink, false, 110f, 0f, 160f, 50f);
            var kickImage = Image(row.transform, "Kick", UiKit.Instance.Button, UiKit.Tomato);
            kickImage.type = UnityEngine.UI.Image.Type.Sliced;
            kickImage.raycastTarget = true;
            Place(kickImage.rectTransform, 280f, 0f, 120f, 48f);
            Text(kickImage.transform, "Label", 22f, UiKit.Ink, false, 0f, 2f, 110f, 40f).text = "KICK";
            var kick = kickImage.gameObject.AddComponent<Button>();
            kick.targetGraphic = kickImage;
            int index = i;
            kick.onClick.AddListener(() => Kick(index));
            _rows.Add((row, name, status, kick));
        }

        var startImage = Image(_card, "StartNow", UiKit.Instance.Button, UiKit.Mint);
        startImage.type = UnityEngine.UI.Image.Type.Sliced;
        startImage.raycastTarget = true;
        Place(startImage.rectTransform, 0f, -_card.sizeDelta.y * 0.5f + 46f, 300f, 64f);
        Text(startImage.transform, "Label", 30f, UiKit.Ink, false, 0f, 2f, 290f, 56f).text = "START NOW";
        _startButton = startImage.gameObject.AddComponent<Button>();
        _startButton.targetGraphic = startImage;
        _startButton.onClick.AddListener(ForceStart);
    }

    private static bool IsHost =>
        NetworkGameManager.Instance != null && NetworkGameManager.Instance.Object != null && NetworkGameManager.Instance.Object.IsValid
        && NetworkGameManager.Instance.HasStateAuthority;

    private void Update()
    {
        var keyboard = Keyboard.current;
        bool inSession = Player.Local != null;
        // Held, not toggled (Shift+Tab is Steam's overlay).
        bool held = keyboard != null && keyboard.tabKey.isPressed && !keyboard.shiftKey.isPressed && inSession && !PauseMenu.IsOpen;
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
        _group.DOFade(open ? 1f : 0f, 0.15f).SetUpdate(true).SetLink(gameObject);
        if (open)
        {
            _nextRefresh = 0f;
            _card.DOKill(true);
            _card.localScale = Vector3.one * 0.92f;
            _card.DOScale(1f, 0.2f).SetEase(Ease.OutBack).SetLink(gameObject);
        }
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
        bool host = IsHost;
        _title.text = inLobby ? "CONTESTANTS" : minigame != null && minigame.Definition != null ? minigame.Definition.DisplayName.ToUpperInvariant() : "CONTESTANTS";

        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            bool used = i < _players.Count;
            row.root.SetActive(used);
            if (!used) continue;
            var player = _players[i];
            var who = player.Object.InputAuthority;
            string name = string.IsNullOrEmpty(player.Nickname) ? $"Player {who.PlayerId}" : player.Nickname;
            row.name.text = who == player.Runner.LocalPlayer ? $"{name} <size=70%>(you)</size>" : name;
            row.name.color = Color.Lerp(player.PlayerColor, UiKit.Ink, 0.35f);
            if (inLobby)
            {
                bool ready = lobby.ReadyStates.TryGet(who, out var r) && r;
                row.status.text = ready ? "<color=#2FA36B><b>READY</b></color>" : "<color=#E03131>NOT READY</color>";
            }
            else
                row.status.text = player.IsSpectator ? "<color=#E03131><b>OUT</b></color>" : "<color=#2FA36B><b>IN</b></color>";
            row.kick.gameObject.SetActive(host && who != player.Runner.LocalPlayer);
        }
        _startButton.gameObject.SetActive(host && inLobby);
        Layout(Mathf.Max(1, _players.Count), host && inLobby);
    }

    /// <summary>The card fits the players listed (and the START NOW button when it shows).</summary>
    private void Layout(int rows, bool startButton)
    {
        float height = 130f + rows * 64f + (startButton ? 90f : 20f);
        _card.sizeDelta = new Vector2(760f, height);
        float top = height * 0.5f;
        _title.rectTransform.anchoredPosition = new Vector2(0f, top - 52f);
        for (int i = 0; i < _rows.Count; i++)
            ((RectTransform)_rows[i].root.transform).anchoredPosition = new Vector2(0f, top - 120f - i * 64f);
        ((RectTransform)_startButton.transform).anchoredPosition = new Vector2(0f, -top + 52f);
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

    #region UI building

    private static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static Image Image(Transform parent, string name, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text Text(Transform parent, string name, float size, Color color, bool outline, float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        var kit = UiKit.Instance;
        text.font = kit.Font;
        if (outline && kit.OutlineThick != null) text.fontSharedMaterial = kit.OutlineThick;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        Place(text.rectTransform, x, y, w, h);
        return text;
    }

    #endregion
}
