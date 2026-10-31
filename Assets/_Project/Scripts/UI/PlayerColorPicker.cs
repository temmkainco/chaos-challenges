using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Lobby: the player colours as a row of swatches. Yours has a ring, colours other players have are dimmed and
/// marked with their initial. C switches to the next free colour (or click a free swatch when the cursor is out).
/// Your last pick is remembered and asked for again when you join a lobby. Colours are kept by
/// <see cref="NetworkGameManager"/> (unique per player, for the whole session).
/// </summary>
public class PlayerColorPicker : MonoBehaviour
{
    private const string PREF = "PlayerColor";
    private const float SPACING = 64f;

    private Image[] _swatches, _rings;
    private TMP_Text[] _marks;
    private TMP_Text _label;
    private bool _askedForPreferred;

    public static void Create(Transform canvas)
    {
        var go = new GameObject("PlayerColorPicker", typeof(RectTransform));
        go.transform.SetParent(canvas, false);
        go.AddComponent<PlayerColorPicker>();
    }

    private void Awake()
    {
        var root = (RectTransform)transform;
        root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = new Vector2(24f, -24f);
        int count = NetworkGameManager.PlayerColors.Length;
        root.sizeDelta = new Vector2(SPACING * count + 40f, 130f);

        var kit = UiKit.Instance;
        var card = Make(transform, "Card", kit.Panel, new Color(UiKit.Cream.r, UiKit.Cream.g, UiKit.Cream.b, 0.92f));
        Stretch(card.rectTransform);
        _label = Text(transform, 24f, new Vector2(0f, 38f), new Vector2(root.sizeDelta.x, 40f));

        _swatches = new Image[count];
        _rings = new Image[count];
        _marks = new TMP_Text[count];
        for (int i = 0; i < count; i++)
        {
            float x = (i - (count - 1) * 0.5f) * SPACING;
            _rings[i] = Make(transform, "Ring " + i, kit.Circle, UiKit.Ink);
            Place(_rings[i].rectTransform, x, -14f, 62f, 62f);
            _swatches[i] = Make(transform, "Colour " + i, kit.Circle, NetworkGameManager.PlayerColors[i]);
            Place(_swatches[i].rectTransform, x, -14f, 50f, 50f);
            _swatches[i].raycastTarget = true;
            int index = i;
            var button = _swatches[i].gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => Request(index));
            _marks[i] = Text(_swatches[i].transform, 26f, Vector2.zero, new Vector2(50f, 50f));
        }
    }

    private void Update()
    {
        var match = NetworkGameManager.Instance;
        if (match == null || match.Object == null || !match.Object.IsValid || match.Phase != MatchPhase.Lobby)
        {
            SetVisible(false);
            return;
        }
        SetVisible(true);
        var me = match.Runner.LocalPlayer;
        int mine = match.ColorIndexOf(me);

        // Ask once per lobby for the colour picked last time (the host keeps it if nobody else has it).
        if (!_askedForPreferred && mine >= 0)
        {
            _askedForPreferred = true;
            int preferred = PlayerPrefs.GetInt(PREF, -1);
            if (preferred >= 0 && preferred != mine) match.RPC_RequestColor((byte)preferred);
        }

        if (!PauseMenu.IsOpen && Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
            Request(NextFree(match, mine));

        for (int i = 0; i < _swatches.Length; i++)
        {
            var owner = match.OwnerOfColor(i);
            bool yours = i == mine;
            bool taken = owner != PlayerRefNone && !yours;
            _rings[i].enabled = yours;
            var c = NetworkGameManager.PlayerColors[i];
            _swatches[i].color = taken ? Color.Lerp(c, UiKit.Ink, 0.55f) : c;
            _marks[i].text = taken ? Initial(owner) : "";
        }
        _label.text = mine >= 0
            ? $"YOUR COLOUR: <color=#{ColorUtility.ToHtmlStringRGB(Color.Lerp(NetworkGameManager.PlayerColors[mine], UiKit.Ink, 0.25f))}>{NetworkGameManager.PlayerColorNames[mine]}</color>   <size=80%>C: change</size>"
            : "YOUR COLOUR";
    }

    private static readonly Fusion.PlayerRef PlayerRefNone = Fusion.PlayerRef.None;

    private static int NextFree(NetworkGameManager match, int mine)
    {
        int count = NetworkGameManager.PlayerColors.Length;
        for (int step = 1; step < count; step++)
        {
            int index = ((mine < 0 ? 0 : mine) + step) % count;
            if (match.OwnerOfColor(index) == Fusion.PlayerRef.None) return index;
        }
        return mine;
    }

    private void Request(int index)
    {
        var match = NetworkGameManager.Instance;
        if (match == null || index < 0 || match.OwnerOfColor(index) != Fusion.PlayerRef.None) return;
        PlayerPrefs.SetInt(PREF, index);
        match.RPC_RequestColor((byte)index);
        Sfx.Play(Sound.Click);
    }

    private static string Initial(Fusion.PlayerRef owner)
    {
        var spawner = FindFirstObjectByType<BasePlayerSpawner>();
        if (spawner != null && spawner.Players.TryGet(owner, out var player) && player != null && !string.IsNullOrEmpty(player.Nickname))
            return player.Nickname.Substring(0, 1).ToUpperInvariant();
        return "•";
    }

    private void SetVisible(bool visible)
    {
        foreach (Transform child in transform)
            if (child.gameObject.activeSelf != visible) child.gameObject.SetActive(visible);
    }

    private static Image Make(Transform parent, string name, Sprite sprite, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TMP_Text Text(Transform parent, float size, Vector2 position, Vector2 boxSize)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = UiKit.Instance.Font;
        text.fontSize = size;
        text.color = UiKit.Ink;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        Place(text.rectTransform, position.x, position.y, boxSize.x, boxSize.y);
        return text;
    }

    private static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, y);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
