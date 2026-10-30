using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Lobby, host only: pick which minigames the show draws its rounds from (<see cref="MatchSettings.IsMinigameEnabled"/>).
/// G opens it; click a game to take it in or out, or use a ready-made pack. At least one game always stays in.
/// Built from code with <see cref="UiKit"/>, like the rules card.
/// </summary>
public class GamePackPanel : MonoBehaviour
{
    private const int COLUMNS = 4;
    private const float TILE_W = 300f, TILE_H = 200f;

    private static readonly string[] ActionPack = { "tilefall", "bomb_rush", "hot_potato", "ring_out", "cannon_barrage", "hex_drop", "prize_grab" };
    private static readonly string[] BrainPack = { "mines", "key_without_lock", "color_code", "console_round", "prize_grab" };

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
    private bool _built;

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
        MakeText(_card, "Title", 72f, UiKit.Orange, true, 0f, 385f, 1200f, 90f).text = "GAME PACK";
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
        for (int i = 0; i < db.Count; i++)
        {
            var def = db.GetByIndex(i);
            int col = i % COLUMNS, row = i / COLUMNS;
            float x = (col - (COLUMNS - 1) * 0.5f) * (TILE_W + 20f), y = 175f - row * (TILE_H + 12f);
            var tile = MakeImage(_card, def.Id, UiKit.Instance.Panel, Color.white);
            tile.type = Image.Type.Sliced;
            tile.raycastTarget = true;
            Place(tile.rectTransform, x, y, TILE_W, TILE_H);
            var fade = tile.gameObject.AddComponent<CanvasGroup>();
            var clip = new GameObject("Clip", typeof(RectTransform), typeof(RectMask2D));
            clip.transform.SetParent(tile.transform, false);
            Place((RectTransform)clip.transform, 0f, 26f, TILE_W - 20f, 130f);
            var picture = MakeImage(clip.transform, "Picture", def.PreviewImage, Color.white);
            Place(picture.rectTransform, 0f, 0f, TILE_W - 20f, (TILE_W - 20f) * 0.51f);
            var name = MakeText(tile.transform, "Name", 24f, UiKit.Ink, false, 0f, -70f, TILE_W - 20f, 40f);
            name.text = def.DisplayName.ToUpperInvariant();
            name.enableAutoSizing = true;
            name.fontSizeMin = 14f;
            name.fontSizeMax = 24f;
            var badgeBack = MakeImage(tile.transform, "Badge", UiKit.Instance.Button, UiKit.Mint);
            badgeBack.type = Image.Type.Sliced;
            Place(badgeBack.rectTransform, TILE_W * 0.5f - 52f, TILE_H * 0.5f - 24f, 84f, 40f);
            var badge = MakeText(badgeBack.transform, "Label", 22f, UiKit.Ink, false, 0f, 2f, 80f, 36f);
            var button = tile.gameObject.AddComponent<Button>();
            button.targetGraphic = tile;
            var captured = def;
            button.onClick.AddListener(() => Toggle(captured));
            tile.gameObject.AddComponent<ButtonJuice>();
            _tiles.Add((def, tile, picture, badge, fade));
        }

        PresetButton("ALL GAMES", UiKit.Yellow, -480f, () => SetPack(null));
        PresetButton("ACTION PACK", UiKit.Pink, -160f, () => SetPack(ActionPack));
        PresetButton("BRAIN PACK", UiKit.Lavender, 160f, () => SetPack(BrainPack));
        PresetButton("DONE", UiKit.Mint, 480f, () => Show(false));
        Refresh();
    }

    private void PresetButton(string label, Color color, float x, UnityEngine.Events.UnityAction action)
    {
        var image = MakeImage(_card, label, UiKit.Instance.Button, color);
        image.type = Image.Type.Sliced;
        image.raycastTarget = true;
        Place(image.rectTransform, x, -385f, 290f, 76f);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        image.gameObject.AddComponent<ButtonJuice>();
        MakeText(image.transform, "Label", 30f, UiKit.Ink, false, 0f, 3f, 280f, 60f).text = label;
    }

    private void Toggle(MinigameDefinitionSO def)
    {
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
        foreach (var t in _tiles) if (MatchSettings.IsMinigameEnabled(t.def.Id)) count++;
        return count;
    }

    private void Refresh()
    {
        foreach (var t in _tiles)
        {
            bool on = MatchSettings.IsMinigameEnabled(t.def.Id);
            t.fade.alpha = on ? 1f : 0.45f;
            t.tile.color = on ? Color.white : new Color(0.85f, 0.83f, 0.9f);
            t.badge.text = on ? "IN" : "OUT";
            t.badge.transform.parent.GetComponent<Image>().color = on ? UiKit.Mint : UiKit.Tomato;
        }
        int count = EnabledCount();
        int rounds = MatchSettings.Rounds;
        _subtitle.text = $"<b>{count}</b> of {_tiles.Count} games in the show - each match plays {rounds} of them" +
                         (count < rounds ? " (some twice)" : "");
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
        bool host = IsHost;
        if (IsOpen && !host)
        {
            Show(false);
            return;
        }
        var keyboard = Keyboard.current;
        if (host && keyboard != null && keyboard.gKey.wasPressedThisFrame && !PauseMenu.IsOpen)
            Show(!IsOpen);
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
