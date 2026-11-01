using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>
/// Lobby: the games tonight's show draws its rounds from, as a grid of picture tiles marked IN or OUT. Everyone opens
/// it (L / LB) to look; the host also edits it (L or G): pick a game to take it in or out, or use a ready-made pack
/// (<see cref="MatchSettings.IsMinigameEnabled"/>). At least one game always stays in. Clients see the host's pack
/// (<see cref="NetworkGameManager.PackMask"/>). Built with the UI kit on the canvas it is added to; keyboard,
/// mouse and gamepad all work, and Back closes it.
/// </summary>
[DefaultExecutionOrder(-50)]   // above the pause menu: takes Back first while open
public class GamePackPanel : MonoBehaviour
{
    private static readonly string[] ActionPack = { "tilefall", "bomb_rush", "hot_potato", "ring_out", "hex_drop", "prize_grab",
        "spin_sweep", "block_party", "log_roll", "lava_rise", "door_dash", "kart_kup" };
    private static readonly string[] BrainPack = { "mines", "key_without_lock", "color_code", "console_round", "prize_grab", "paint_party", "king_of_the_hill" };
    private static readonly string[] NewPack = { "spin_sweep", "block_party", "log_roll", "lava_rise", "king_of_the_hill", "paint_party", "tail_tag",
        "door_dash", "kart_kup", "sky_rings" };

    private const float WIDTH = 1500f, HEIGHT = 940f, GRID_W = 1420f, GRID_H = 600f, GAP = 16f;

    public static bool IsOpen { get; private set; }

    public static bool IsHost
    {
        get
        {
            var gm = NetworkGameManager.Instance;
            return gm != null && gm.Object != null && gm.Object.IsValid && gm.HasStateAuthority && gm.Phase == MatchPhase.Lobby;
        }
    }

    private static bool InLobby
    {
        get
        {
            var gm = NetworkGameManager.Instance;
            return gm != null && gm.Object != null && gm.Object.IsValid && gm.Phase == MatchPhase.Lobby;
        }
    }

    private GameObject _scrim;
    private RectTransform _panel;
    private CanvasGroup _group;
    private TextMeshProUGUI _subtitle;
    private RectTransform _grid;
    private readonly List<(MinigameDefinitionSO def, Button button, UiBox badge, Image badgeIcon, TextMeshProUGUI badgeText, CanvasGroup fade)> _tiles = new();
    private readonly List<GameObject> _hostOnly = new();
    private UiButton _done;
    private bool _built;
    private GameObject _restoreFocus;

    private void Awake()
    {
        var safe = transform.Find("Safe") as RectTransform ?? (RectTransform)transform;
        var theme = UiTheme.Instance;
        // The scrim covers the whole screen; the panel sits in the safe area.
        var scrim = new GameObject("Scrim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        scrim.transform.SetParent(transform, false);
        scrim.transform.SetAsFirstSibling();
        Ui.Stretch(scrim.rectTransform);
        scrim.color = new Color(theme.Ink.r, theme.Ink.g, theme.Ink.b, theme.ScrimAlpha);
        _scrim = scrim.gameObject;

        var box = Ui.Box(safe, "Panel", UiTheme.Tone.Studio);
        _panel = Ui.Place((RectTransform)box.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(WIDTH, HEIGHT));
        _group = box.gameObject.AddComponent<CanvasGroup>();

        var title = Ui.Text(_panel, "TONIGHT'S GAMES", UiTheme.TextRole.DisplayM, UiTheme.Tone.Paper, TextAlignmentOptions.Left, true);
        Ui.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -28f), new Vector2(WIDTH - 80f, 80f));
        _subtitle = Ui.Text(_panel, "", UiTheme.TextRole.Body, UiTheme.Tone.Haze, TextAlignmentOptions.Left);
        Ui.Place(_subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(40f, -108f), new Vector2(WIDTH - 80f, 44f));
        _grid = Ui.Place(Ui.Rect(_panel, "Grid"), new Vector2(0.5f, 1f), new Vector2(0f, -172f), new Vector2(GRID_W, GRID_H));

        var buttons = Ui.Place(Ui.Rect(_panel, "Buttons"), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(GRID_W, 88f));
        Ui.Stack<HorizontalLayoutGroup>(buttons, 2, TextAnchor.MiddleLeft);
        _hostOnly.Add(PackButton(buttons, "ALL GAMES", () => SetPack(null)));
        _hostOnly.Add(PackButton(buttons, "ACTION", () => SetPack(ActionPack)));
        _hostOnly.Add(PackButton(buttons, "BRAINS", () => SetPack(BrainPack)));
        _hostOnly.Add(PackButton(buttons, "NEW", () => SetPack(NewPack)));
        var spacer = Ui.Rect(buttons, "Spacer");
        spacer.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        _done = Ui.Button(buttons, "DONE", UiButton.Variant.Primary, "tick", () => Show(false));
        Ui.Size(_done, 260f, 80f);

        _scrim.SetActive(false);
        _panel.gameObject.SetActive(false);
    }

    private GameObject PackButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        var b = Ui.Button(parent, label, UiButton.Variant.Secondary, null, action);
        Ui.Size(b, 250f, 80f);
        return b.gameObject;
    }

    private void OnDestroy() => IsOpen = false;

    /// <summary>Tiles are made on first open: the game manager (and its database) exists by then.</summary>
    private void Build()
    {
        var gm = NetworkGameManager.Instance;
        if (_built || gm == null || gm.Database == null) return;
        _built = true;
        var db = gm.Database;
        var theme = UiTheme.Instance;
        // The grid shrinks to fit however many games there are.
        int columns = Mathf.Clamp(Mathf.CeilToInt(db.Count / 4f), 4, 8);
        int rows = Mathf.CeilToInt(db.Count / (float)columns);
        float tileW = (GRID_W - (columns - 1) * GAP) / columns;
        float tileH = Mathf.Min(tileW * 0.72f, (GRID_H - (rows - 1) * GAP) / rows);
        var grid = _grid.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(tileW, tileH);
        grid.spacing = new Vector2(GAP, GAP);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.childAlignment = TextAnchor.UpperCenter;

        float nameH = Mathf.Clamp(tileH * 0.26f, 30f, 44f);
        for (int i = 0; i < db.Count; i++)
        {
            var def = db.GetByIndex(i);
            var tile = Ui.Box(_grid, def.Id, UiTheme.Tone.Riser, 16f);
            var rt = (RectTransform)tile.transform;
            var fade = tile.gameObject.AddComponent<CanvasGroup>();

            var clip = Ui.Rect(rt, "Clip");
            clip.anchorMin = Vector2.zero; clip.anchorMax = Vector2.one;
            clip.offsetMin = new Vector2(10f, nameH + 6f); clip.offsetMax = new Vector2(-10f, -10f);
            Thumbnail.Round(clip.gameObject);
            var picture = new GameObject("Picture", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            picture.transform.SetParent(clip, false);
            picture.sprite = def.PreviewImage;
            picture.raycastTarget = false;
            Thumbnail.Cover(picture, new Vector2(tileW - 20f, tileH - nameH - 16f));

            var name = Ui.Text(rt, def.DisplayName.ToUpperInvariant(), UiTheme.TextRole.Label, UiTheme.Tone.Paper);
            name.GetComponent<ThemedText>().SingleLine = true;
            name.GetComponent<ThemedText>().Apply();
            name.fontStyle = FontStyles.Bold;
            name.rectTransform.anchorMin = Vector2.zero; name.rectTransform.anchorMax = new Vector2(1f, 0f);
            name.rectTransform.pivot = new Vector2(0.5f, 0f);
            name.rectTransform.offsetMin = new Vector2(8f, 4f); name.rectTransform.offsetMax = new Vector2(-8f, nameH + 4f);

            // IN / OUT: a word and an icon, not colour alone.
            var badge = Ui.Box(rt, "Badge", UiTheme.Tone.Good, 14f);
            badge.ShadowDepth = theme.ShadowChip;
            badge.Apply();
            Ui.Place((RectTransform)badge.transform, new Vector2(1f, 1f), new Vector2(-4f, -4f), new Vector2(104f, 40f));
            var icon = Ui.Icon(badge.transform, "tick", 24f, UiTheme.Tone.Ink);
            Ui.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(24f, 24f));
            var word = Ui.Text(badge.transform, "IN", UiTheme.TextRole.Label, UiTheme.Tone.Ink);
            word.GetComponent<ThemedText>().ShrinkToFit = false;
            word.fontStyle = FontStyles.Bold;
            Ui.Stretch(word.rectTransform);
            word.rectTransform.offsetMin = new Vector2(34f, 0f);

            var button = tile.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = tile.FillImage;
            var captured = def;
            button.onClick.AddListener(() => Toggle(captured));
            tile.gameObject.AddComponent<UiFocus>().Target = rt;
            _tiles.Add((def, button, badge, icon, word, fade));
        }
        Refresh();
    }

    private void Toggle(MinigameDefinitionSO def)
    {
        if (!IsHost) return;   // guests look, only the host edits
        bool enabled = MatchSettings.IsMinigameEnabled(def.Id);
        if (enabled && EnabledCount() <= 1)
        {
            _panel.DOKill(true);
            _panel.DOShakeAnchorPos(0.3f, 12f, 30).SetUpdate(true);
            Sfx.Play(Sound.Nope);
            return;
        }
        MatchSettings.SetMinigameEnabled(def.Id, !enabled);
        Sfx.Play(Sound.Click);
        Refresh();
    }

    /// <summary>Null = every game.</summary>
    private void SetPack(string[] ids)
    {
        foreach (var t in _tiles)
            MatchSettings.SetMinigameEnabled(t.def.Id, ids == null || System.Array.IndexOf(ids, t.def.Id) >= 0);
        if (EnabledCount() == 0)
            foreach (var t in _tiles) MatchSettings.SetMinigameEnabled(t.def.Id, true);
        Refresh();
    }

    private int EnabledCount()
    {
        int count = 0;
        for (int i = 0; i < _tiles.Count; i++) if (IsIn(i)) count++;
        return count;
    }

    /// <summary>The host's own settings on the host; the host's published pack everywhere else.</summary>
    private bool IsIn(int index) => IsHost ? MatchSettings.IsMinigameEnabled(_tiles[index].def.Id)
        : NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsInPack(index);

    private void Refresh()
    {
        bool host = IsHost;
        foreach (var button in _hostOnly) button.SetActive(host);
        for (int i = 0; i < _tiles.Count; i++)
        {
            var t = _tiles[i];
            bool on = IsIn(i);
            t.fade.alpha = on ? 1f : 0.5f;
            t.badge.SetFill(on ? UiTheme.Tone.Good : UiTheme.Tone.Bad);
            t.badgeIcon.sprite = UiShapes.Icon(on ? "tick" : "cross");
            t.badgeIcon.color = UiTheme.Instance.Color(on ? UiTheme.Tone.Ink : UiTheme.Tone.Paper);
            t.badgeText.text = on ? "IN" : "OUT";
            t.badgeText.color = UiTheme.Instance.Color(on ? UiTheme.Tone.Ink : UiTheme.Tone.Paper);
            t.button.interactable = host;
        }
        int count = EnabledCount();
        var gm = NetworkGameManager.Instance;
        int rounds = host ? MatchSettings.Rounds : gm != null ? gm.PackRounds : MatchSettings.Rounds;
        _subtitle.text = $"{count} of {_tiles.Count} in  ·  {rounds} rounds a match" + (count < rounds ? " (some twice)" : "");
        UiFocus.Chain(Rows(host));
    }

    /// <summary>Keyboard / gamepad order: the grid's rows (the tiles a host can pick), then the button row.</summary>
    private Selectable[][] Rows(bool host)
    {
        var rows = new List<Selectable[]>();
        if (host)
        {
            int columns = _grid.GetComponent<GridLayoutGroup>().constraintCount;
            for (int start = 0; start < _tiles.Count; start += columns)
            {
                var row = new List<Selectable>();
                for (int i = start; i < Mathf.Min(start + columns, _tiles.Count); i++) row.Add(_tiles[i].button);
                rows.Add(row.ToArray());
            }
        }
        var bottom = new List<Selectable>();
        if (host) foreach (var b in _hostOnly) bottom.Add(b.GetComponent<Selectable>());
        bottom.Add(_done);
        rows.Add(bottom.ToArray());
        return rows.ToArray();
    }

    private void Show(bool open)
    {
        if (open) Build();
        if (open == IsOpen) return;
        IsOpen = open;
        var events = EventSystem.current;
        if (open)
        {
            _restoreFocus = events != null ? events.currentSelectedGameObject : null;
            _scrim.SetActive(true);
            _panel.gameObject.SetActive(true);
            Refresh();
            UiMotion.PanelIn(_panel, _group);
            var first = IsHost && _tiles.Count > 0 ? _tiles[0].button.gameObject : _done.gameObject;
            if (events != null) events.SetSelectedGameObject(first);
        }
        else
        {
            _scrim.SetActive(false);
            UiMotion.PanelOut(_panel, _group).OnComplete(() => { if (!IsOpen) _panel.gameObject.SetActive(false); });
            if (events != null) events.SetSelectedGameObject(_restoreFocus);
        }
    }

    private void Update()
    {
        if (IsOpen && !InLobby)
        {
            Show(false);
            return;
        }
        if (IsOpen)
        {
            UiFocus.Keep(_done.gameObject);
            if (UiModal.OpenCount == 0 && UiInput.Back()) { Show(false); return; }
            if (!IsHost && Time.frameCount % 15 == 0) Refresh();   // the host may change the pack while a guest looks
        }
        if (PauseMenu.IsOpen || !InLobby || UiModal.OpenCount > 0) return;
        if (UiInput.Pressed(Key.L, GamepadButton.LeftShoulder) || (IsHost && UiInput.Pressed(Key.G)))
            Show(!IsOpen);
    }
}
