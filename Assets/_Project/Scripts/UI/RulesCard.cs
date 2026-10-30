using DG.Tweening;
using Fusion;
using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The show's rules card, shown over the arena during <see cref="MinigamePhase.Intro"/>: round and name, the minigame's
/// illustration, objective / danger / scoring, the controls as key caps, and a ready strip with every contestant.
/// E, Space or the button readies you up. Meanwhile an orbiting camera reveals the arena behind the card.
/// Built from code under the minigame HUD canvas, so every minigame gets it without per-scene setup.
/// </summary>
public class RulesCard : MonoBehaviour
{
    private const float ORBIT_RADIUS = 24f, ORBIT_HEIGHT = 13f, ORBIT_SPEED = 9f;

    private CanvasGroup _group;
    private TMP_Text _round, _title, _objective, _danger, _scoring, _controls, _readyList, _readyHint;
    private Image _illustration;
    private Button _readyButton;
    private CinemachineCamera _orbit;
    private MinigameController _shownFor;
    private MinigameDefinitionSO _shownDefinition;
    private bool _sentReady;
    private float _orbitAngle, _introTime;
    private Vector3 _orbitCentre;
    private readonly StringBuilder _sb = new();
    private int _lastReadyMask = -1, _lastSeconds = -1;

    private void Awake()
    {
        var kit = UiKit.Instance;
        var root = new GameObject("RulesCard", typeof(RectTransform), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        Stretch((RectTransform)root.transform);
        _group = root.GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;

        var dim = MakeImage(root.transform, "Dim", null, new Color(UiKit.Ink.r, UiKit.Ink.g, UiKit.Ink.b, 0.35f));
        Stretch(dim.rectTransform);

        var card = MakeImage(root.transform, "Card", kit.Panel, UiKit.Cream);
        card.type = Image.Type.Sliced;
        Place(card.rectTransform, 0f, 10f, 1320f, 780f);

        _round = MakeText(card.transform, "Round", 30f, UiKit.Ink, false, 0f, 330f, 1200f, 40f);
        _title = MakeText(card.transform, "Title", 84f, UiKit.Orange, true, 0f, 262f, 1200f, 100f);

        // Picture clipped inside the frame, with the rounded frame drawn on top so nothing pokes over its edge.
        var back = MakeImage(card.transform, "IllustrationBack", kit.Panel, UiKit.Ink);
        back.type = Image.Type.Sliced;
        Place(back.rectTransform, -300f, 40f, 640f, 340f);
        var clip = new GameObject("Clip", typeof(RectTransform), typeof(RectMask2D));
        clip.transform.SetParent(card.transform, false);
        Place((RectTransform)clip.transform, -300f, 40f, 612f, 312f);
        _illustration = MakeImage(clip.transform, "Illustration", null, Color.white);
        Place(_illustration.rectTransform, 0f, 0f, 612f, 312f);
        var frame = MakeImage(card.transform, "IllustrationFrame", kit.Frame != null ? kit.Frame : kit.Panel, UiKit.Ink);
        frame.type = Image.Type.Sliced;
        frame.pixelsPerUnitMultiplier = 2.2f;
        Place(frame.rectTransform, -300f, 40f, 644f, 344f);

        _objective = Row(card.transform, "GOAL", UiKit.Mint, 330f, 150f);
        _danger = Row(card.transform, "WATCH OUT", UiKit.Tomato, 330f, 40f);
        _scoring = Row(card.transform, "SCORE", UiKit.Yellow, 330f, -70f);

        _controls = MakeText(card.transform, "Controls", 30f, UiKit.Ink, false, 0f, -195f, 1240f, 60f);

        var strip = MakeImage(card.transform, "ReadyStrip", kit.Panel, UiKit.Lavender);
        strip.type = Image.Type.Sliced;
        Place(strip.rectTransform, -140f, -300f, 960f, 110f);
        _readyList = MakeText(strip.transform, "Ready", 30f, UiKit.Ink, false, 0f, 14f, 920f, 46f);
        _readyHint = MakeText(strip.transform, "Hint", 24f, UiKit.Ink, false, 0f, -28f, 920f, 36f);

        var button = MakeImage(card.transform, "ReadyButton", kit.Button, UiKit.Yellow);
        button.type = Image.Type.Sliced;
        Place(button.rectTransform, 470f, -300f, 280f, 96f);
        _readyButton = button.gameObject.AddComponent<Button>();
        _readyButton.targetGraphic = button;
        _readyButton.onClick.AddListener(SendReady);
        button.gameObject.AddComponent<ButtonJuice>();
        MakeText(button.transform, "Label", 44f, UiKit.Ink, false, 0f, 4f, 260f, 80f).text = "READY!";
    }

    private void Update()
    {
        var controller = MinigameController.Current;
        bool intro = controller != null && controller.Object != null && controller.Object.IsValid && controller.Phase == MinigamePhase.Intro;
        _group.alpha = Mathf.MoveTowards(_group.alpha, intro ? 1f : 0f, Time.deltaTime * 4f);
        _group.blocksRaycasts = intro;
        UpdateOrbit(intro);
        if (!intro)
            return;

        if (controller != _shownFor || controller.Definition != _shownDefinition)
            Fill(controller);

        bool ready = controller.IsReady(controller.Runner.LocalPlayer) || _sentReady;
        _readyButton.interactable = !ready;
        if (!ready && !PauseMenu.IsOpen && Keyboard.current != null
            && (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame))
            SendReady();

        int seconds = Mathf.CeilToInt(controller.TimeRemaining);
        if (controller.ReadyMask != _lastReadyMask || seconds != _lastSeconds)
        {
            _lastReadyMask = controller.ReadyMask;
            _lastSeconds = seconds;
            RefreshReady(controller, ready, seconds);
        }
    }

    private void Fill(MinigameController controller)
    {
        if (controller != _shownFor) _sentReady = false;
        _shownFor = controller;
        _shownDefinition = controller.Definition;
        _lastReadyMask = -1;
        var def = controller.Definition;
        var gm = NetworkGameManager.Instance;
        _round.text = gm != null && gm.RoundCount > 0 ? $"ROUND {gm.RoundIndex + 1} OF {gm.RoundCount}" : "NEXT ROUND";
        _title.text = def != null ? def.DisplayName.ToUpperInvariant() : "";
        _objective.text = def != null && !string.IsNullOrEmpty(def.Objective) ? def.Objective : def?.Description;
        _danger.text = def?.Danger ?? "";
        _scoring.text = def?.Scoring ?? "";
        _illustration.sprite = def != null ? def.PreviewImage : null;
        _illustration.enabled = _illustration.sprite != null;

        _sb.Clear();
        if (controller.UsesDefaultControls)
        {
            AppendKey("WASD", "Move");
            AppendKey("SPACE", "Jump");
        }
        if (def != null && def.Controls != null)
            foreach (var pair in def.Controls)
            {
                int bar = pair.IndexOf('|');
                if (bar > 0) AppendKey(pair.Substring(0, bar), pair.Substring(bar + 1));
            }
        _controls.text = _sb.ToString();

        var root = (RectTransform)_group.transform.GetChild(1);
        root.DOKill(true);
        root.localScale = Vector3.one * 0.85f;
        root.DOScale(1f, 0.35f).SetEase(Ease.OutBack);
    }

    private void AppendKey(string key, string action)
    {
        if (_sb.Length > 0) _sb.Append("     ");
        _sb.Append("<mark=#2E1A4722 padding=\"10,10,4,4\"><b><color=#E8590C>").Append(key).Append("</color></b></mark> ").Append(action);
    }

    private void RefreshReady(MinigameController controller, bool localReady, int seconds)
    {
        _sb.Clear();
        var spawner = FindFirstObjectByType<BasePlayerSpawner>();
        if (spawner != null && spawner.Object != null)
        {
            foreach (var entry in spawner.Players)
            {
                var player = entry.Value;
                if (player == null || player.Object == null) continue;
                string name = string.IsNullOrEmpty(player.Nickname) ? $"Player {entry.Key.PlayerId}" : player.Nickname;
                bool ready = controller.IsReady(entry.Key);
                if (_sb.Length > 0) _sb.Append("   ");
                _sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(Color.Lerp(player.OutfitColor, UiKit.Ink, 0.3f))).Append('>')
                   .Append(name).Append("</color> ").Append(ready ? "<color=#2FA36B><b>READY</b></color>" : "<color=#9A8FB0>...</color>");
            }
        }
        _readyList.text = _sb.ToString();
        _readyHint.text = localReady
            ? $"Waiting for the others - starting in {seconds}s"
            : $"Press <b>E</b> when you're ready - starting in {seconds}s";
    }

    private void SendReady()
    {
        var controller = MinigameController.Current;
        if (_sentReady || controller == null || controller.Phase != MinigamePhase.Intro) return;
        _sentReady = true;
        controller.RPC_Ready();
        Sfx.Play(Sound.Click);
    }

    /// <summary>Slow orbit around the arena while the card is up: the arena reveal of a TV round intro.</summary>
    private void UpdateOrbit(bool intro)
    {
        if (!intro)
        {
            if (_orbit != null) { Destroy(_orbit.gameObject); _orbit = null; }
            return;
        }
        if (_orbit == null)
        {
            var go = new GameObject("IntroOrbitCamera");
            _orbit = go.AddComponent<CinemachineCamera>();
            _orbit.Priority = 40;
            _orbitCentre = Vector3.zero;
            _orbitAngle = 200f;
            _introTime = 0f;
        }
        var controller = MinigameController.Current;
        _introTime += Time.deltaTime;
        if (controller != null && controller.TryGetIntroShot(_introTime, out var shotPosition, out var shotLookAt))
        {
            _orbit.transform.position = shotPosition;
            _orbit.transform.LookAt(shotLookAt);
            return;
        }
        // Players spawn a moment after the scene loads: snap to them once they exist, then follow smoothly.
        if (TryArenaCentre(out var centre))
            _orbitCentre = _orbitCentre == Vector3.zero ? centre : Vector3.Lerp(_orbitCentre, centre, Time.deltaTime * 2f);
        _orbitAngle += ORBIT_SPEED * Time.deltaTime;
        var offset = Quaternion.Euler(0f, _orbitAngle, 0f) * new Vector3(0f, ORBIT_HEIGHT, -ORBIT_RADIUS);
        _orbit.transform.position = _orbitCentre + offset;
        _orbit.transform.LookAt(_orbitCentre + Vector3.up * 1.5f);
    }

    private BasePlayerSpawner _spawner;

    private bool TryArenaCentre(out Vector3 centre)
    {
        centre = Vector3.zero;
        if (_spawner == null) _spawner = FindFirstObjectByType<BasePlayerSpawner>();
        var spawner = _spawner;
        if (spawner == null || spawner.Object == null) return false;
        int count = 0;
        foreach (var entry in spawner.Players)
            if (entry.Value != null) { centre += entry.Value.transform.position; count++; }
        if (count == 0) return false;
        centre /= count;
        return true;
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

    /// <summary>One "badge + line" row on the right of the card (goal, danger, scoring).</summary>
    private static TMP_Text Row(Transform parent, string label, Color color, float x, float y)
    {
        var badge = MakeImage(parent, label, UiKit.Instance.Button, color);
        badge.type = Image.Type.Sliced;
        Place(badge.rectTransform, x - 205f, y, 190f, 60f);
        MakeText(badge.transform, "Label", 24f, UiKit.Ink, false, 0f, 3f, 180f, 50f).text = label;
        var text = MakeText(parent, label + "Text", 26f, UiKit.Ink, false, x + 110f, y, 420f, 100f);
        text.alignment = TextAlignmentOptions.Left;
        text.enableWordWrapping = true;
        text.enableAutoSizing = true;
        text.fontSizeMin = 16f;
        text.fontSizeMax = 26f;
        return text;
    }

    #endregion
}
