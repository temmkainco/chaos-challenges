using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Lobby: the games tonight's show draws its rounds from. Everyone opens it with L to see the list (pictures, in or out);
/// the host also edits it (L or G): click a game to take it in or out, or use a ready-made pack
/// (<see cref="MatchSettings.IsMinigameEnabled"/>). At least one game always stays in. Clients see the host's pack
/// (<see cref="NetworkGameManager.PackMask"/>). Built from code with <see cref="UiKit"/>, like the rules card.
/// </summary>
public class GamePackPanel : MonoBehaviour
{
    private static readonly string[] ActionPack = { "tilefall", "bomb_rush", "hot_potato", "ring_out", "hex_drop", "prize_grab",
        "spin_sweep", "block_party", "log_roll", "lava_rise", "door_dash", "kart_kup" };
    private static readonly string[] BrainPack = { "mines", "key_without_lock", "color_code", "console_round", "prize_grab", "paint_party", "king_of_the_hill" };
    private static readonly string[] NewPack = { "spin_sweep", "block_party", "log_roll", "lava_rise", "king_of_the_hill", "paint_party", "tail_tag",
        "door_dash", "kart_kup", "sky_rings" };

    public static bool IsOpen { get; private set; }

    public static bool IsHost
    {
        get
        {
            var gm = NetworkGameManager.Instance;
            return gm != null && gm.Object != null && gm.Object.IsValid && gm.HasStateAuthority && gm.Phase == MatchPhase.Lobby;
        }
    }

    private CanvasGroup _group;
    private RectTransform _card;
    private TMP_Text _subtitle;
    private readonly List<(MinigameDefinitionSO def, Image tile, Image picture, TMP_Text badge, CanvasGroup fade)> _tiles = new();
    private readonly List<GameObject> _hostOnly = new();
    private bool _built;

    private static bool InLobby
    {
        get
        {
            var gm = NetworkGameManager.Instance;
            return gm != null && gm.Object != null && gm.Object.IsValid && gm.Phase == MatchPhase.Lobby;
        }
    }

    private void Awake()
    {
        var root = new GameObject("GamePack", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        Stretch((RectTransform)root.transform);
        _group = root.GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        var dim = MakeImage(root.transform, "Dim", null, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.45f));
        Stretch(dim.rectTransform);
        var card = MakeImage(root.transform, "Card", UiKit.Instance.Panel, UiKit.Cream);
        card.type = Image.Type.Sliced;
        _card = card.rectTransform;
        Place(_card, 0f, 0f, 1400f, 900f);
        MakeText(_card, "Title", 72f, UiKit.Orange, true, 0f, 385f, 1200f, 90f).text = "TONIGHT'S GAMES";
        _subtitle = MakeText(_card, "Subtitle", 28f, UiKit.Ink, false, 0f, 322f, 1200f, 40f);
    }

    private void OnDestroy() => IsOpen = false;

    /// <summary>Tiles are made on first open: the game manager (and its database) exists by then.</summary>
    private void Build()
    {
        var gm = NetworkGameManager.Instance;
        if (_built || gm == null || gm.Database == null) return;
        _built = true;
        var db = gm.Database;
        // The grid shrinks to fit however many games there are (three or four rows).
        int columns = Mathf.Clamp(Mathf.CeilToInt(db.Count / 3f), 4, 8);
        float tileW = Mathf.Min(300f, (1340f - (columns - 1) * 16f) / columns), tileH = tileW * 0.68f;
        float scale = tileW / 300f;
        int rows = Mathf.CeilToInt(db.Count / (float)columns);
        float top = 270f - tileH * 0.5f, step = Mathf.Min(tileH + 12f, (600f - tileH) / Mathf.Max(1, rows - 1));
        for (int i = 0; i < db.Count; i++)
        {
            var def = db.GetByIndex(i);
            int col = i % columns, row = i / columns;
            float x = (col - (columns - 1) * 0.5f) * (tileW + 16f), y = top - row * step;
            var tile = MakeImage(_card, def.Id, UiKit.Instance.Panel, Color.white);
            tile.type = Image.Type.Sliced;
            tile.raycastTarget = true;
            Place(tile.rectTransform, x, y, tileW, tileH);
            var fade = tile.gameObject.AddComponent<CanvasGroup>();
            var clip = new GameObject("Clip", typeof(RectTransform));
            clip.transform.SetParent(tile.transform, false);
            var clipSize = new Vector2(tileW - 20f * scale, tileH * 0.65f);
            Place((RectTransform)clip.transform, 0f, 26f * scale, clipSize.x, clipSize.y);
            Thumbnail.Round(clip);
            var picture = MakeImage(clip.transform, "Picture", def.PreviewImage, Color.white);
            Thumbnail.Cover(picture, clipSize);
            var name = MakeText(tile.transform, "Name", 24f * scale, UiKit.Ink, false, 0f, -tileH * 0.35f, tileW - 16f * scale, 40f * scale);
            name.text = def.DisplayName.ToUpperInvariant();
            name.enableAutoSizing = true;
            name.enableWordWrapping = false;
            name.fontSizeMin = 9f;
            name.fontSizeMax = 24f * scale;
            var badgeBack = MakeImage(tile.transform, "Badge", UiKit.Instance.Button, UiKit.Mint);
            badgeBack.type = Image.Type.Sliced;
            Place(badgeBack.rectTransform, tileW * 0.5f - 52f * scale, tileH * 0.5f - 24f * scale, 84f * scale, 40f * scale);
            var badge = MakeText(badgeBack.transform, "Label", 22f * scale, UiKit.Ink, false, 0f, 2f, 80f * scale, 36f * scale);
            var button = tile.gameObject.AddComponent<Button>();
            button.targetGraphic = tile;
            var captured = def;
            button.onClick.AddListener(() => Toggle(captured));
            tile.gameObject.AddComponent<ButtonJuice>();
            _tiles.Add((def, tile, picture, badge, fade));
        }

        _hostOnly.Add(PresetButton("ALL GAMES", UiKit.Yellow, -540f, () => SetPack(null)));
        _hostOnly.Add(PresetButton("ACTION PACK", UiKit.Pink, -270f, () => SetPack(ActionPack)));
        _hostOnly.Add(PresetButton("BRAIN PACK", UiKit.Lavender, 0f, () => SetPack(BrainPack)));
        _hostOnly.Add(PresetButton("NEW GAMES", UiKit.Orange, 270f, () => SetPack(NewPack)));
        PresetButton("DONE", UiKit.Mint, 540f, () => Show(false));
        Refresh();
    }

    private GameObject PresetButton(string label, Color color, float x, UnityEngine.Events.UnityAction action)
    {
        var image = MakeImage(_card, label, UiKit.Instance.Button, color);
        image.type = Image.Type.Sliced;
        image.raycastTarget = true;
        Place(image.rectTransform, x, -385f, 250f, 76f);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        image.gameObject.AddComponent<ButtonJuice>();
        MakeText(image.transform, "Label", 28f, UiKit.Ink, false, 0f, 3f, 240f, 60f).text = label;
        return image.gameObject;
    }

    private void Toggle(MinigameDefinitionSO def)
    {
        if (!IsHost) return;   // guests look, only the host edits
        bool enabled = MatchSettings.IsMinigameEnabled(def.Id);
        if (enabled && EnabledCount() <= 1)
        {
            _card.DOKill(true);
            _card.DOShakeAnchorPos(0.3f, 12f, 30);
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
        Sfx.Play(Sound.Click);
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
            t.fade.alpha = on ? 1f : 0.45f;
            t.tile.color = on ? Color.white : new Color(0.85f, 0.83f, 0.9f);
            t.badge.text = on ? "IN" : "OUT";
            t.badge.transform.parent.GetComponent<Image>().color = on ? UiKit.Mint : UiKit.Tomato;
        }
        int count = EnabledCount();
        var gm = NetworkGameManager.Instance;
        int rounds = host ? MatchSettings.Rounds : gm != null ? gm.PackRounds : MatchSettings.Rounds;
        _subtitle.text = $"<b>{count}</b> of {_tiles.Count} games in the show - each match plays {rounds} of them" +
                         (count < rounds ? " (some twice)" : "") + (host ? "  -  click a game to take it in or out" : "");
    }

    private void Show(bool open)
    {
        if (open) Build();
        IsOpen = open;
        _group.blocksRaycasts = open;
        _group.DOKill();
        _group.DOFade(open ? 1f : 0f, 0.2f).SetUpdate(true).SetLink(gameObject);
        if (open)
        {
            Refresh();
            _card.DOKill(true);
            _card.localScale = Vector3.one * 0.9f;
            _card.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetLink(gameObject);
        }
    }

    private void Update()
    {
        if (IsOpen && !InLobby)
        {
            Show(false);
            return;
        }
        var keyboard = Keyboard.current;
        if (keyboard == null || PauseMenu.IsOpen || !InLobby) return;
        if (keyboard.lKey.wasPressedThisFrame || (IsHost && keyboard.gKey.wasPressedThisFrame))
            Show(!IsOpen);
        else if (IsOpen && !IsHost && Time.frameCount % 15 == 0)
            Refresh();   // the host may change the pack while a guest looks
    }

    #region UI building

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(w, h);
    }

    private static Image MakeImage(Transform parent, string name, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text MakeText(Transform parent, string name, float size, Color color, bool outline, float x, float y, float w, float h)
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
