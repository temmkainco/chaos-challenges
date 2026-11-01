using DG.Tweening;
using Fusion;
using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>
/// The show's rules card, shown over the arena during <see cref="MinigamePhase.Intro"/>: round tag and the game's name,
/// its picture, goal / watch out / score, the controls as key caps (keyboard or gamepad, whichever was used last) and
/// the contestants with who is ready. E, Space, Enter or gamepad A / Y (or the button) readies you up. Meanwhile an
/// orbiting camera reveals the arena behind the card. Built with the UI kit on its own canvas, so every minigame gets
/// it without per-scene setup.
/// </summary>
public class RulesCard : MonoBehaviour
{
    private const float ORBIT_RADIUS = 24f, ORBIT_HEIGHT = 13f, ORBIT_SPEED = 9f;
    private const float WIDTH = 1480f, HEIGHT = 860f, PICTURE_W = 680f, PICTURE_H = 383f;

    private CanvasGroup _group;
    private RectTransform _card, _controls, _chips;
    private TextMeshProUGUI _round, _map, _title, _objective, _danger, _scoring, _hint;
    private Image _illustration;
    private UiButton _readyButton;
    private GameObject _readyBadge, _scrim;
    private PromptBar _prompts;
    private readonly List<PlayerChip> _chipPool = new();
    private CinemachineCamera _orbit;
    /// <summary>The card is waiting for the local player: the pause menu frees the mouse so READY can be clicked.</summary>
    public static bool WantsCursor { get; private set; }

    private MinigameController _shownFor;
    private MinigameDefinitionSO _shownDefinition;
    private bool _sentReady, _visible, _shownPad;
    private float _orbitAngle, _introTime;
    private Vector3 _orbitCentre;
    private int _lastReadyMask = -1, _lastSeconds = -1;

    private void Awake()
    {
        var safe = Ui.Canvas("Rules Card", 30, transform);
        var theme = UiTheme.Instance;
        var scrim = new GameObject("Scrim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        scrim.transform.SetParent(safe.parent, false);
        scrim.transform.SetAsFirstSibling();
        Ui.Stretch(scrim.rectTransform);
        scrim.color = new Color(theme.Ink.r, theme.Ink.g, theme.Ink.b, 0.35f);
        scrim.raycastTarget = false;
        _scrim = scrim.gameObject;

        var box = Ui.Box(safe, "Card", UiTheme.Tone.Studio);
        _card = Ui.Place((RectTransform)box.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(WIDTH, HEIGHT));
        _group = box.gameObject.AddComponent<CanvasGroup>();

        // Round tag and the game's name.
        var tag = Ui.Box(_card, "Round", UiTheme.Tone.Brand, 22f);
        tag.ShadowDepth = theme.ShadowChip;
        tag.Apply();
        var tagRt = Ui.Place((RectTransform)tag.transform, new Vector2(0f, 1f), new Vector2(48f, -36f), new Vector2(240f, 46f));
        _round = Ui.Text(tagRt, "", UiTheme.TextRole.Label, UiTheme.Tone.Paper);
        _round.GetComponent<ThemedText>().SingleLine = true;
        _round.GetComponent<ThemedText>().Apply();
        Ui.Stretch(_round.rectTransform);
        _map = Ui.Text(_card, "", UiTheme.TextRole.Label, UiTheme.Tone.Haze, TextAlignmentOptions.Left);
        Ui.Place(_map.rectTransform, new Vector2(0f, 1f), new Vector2(306f, -36f), new Vector2(600f, 46f));
        _title = Ui.Text(_card, "", UiTheme.TextRole.DisplayL, UiTheme.Tone.Paper, TextAlignmentOptions.Left, true);
        Ui.Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(44f, -88f), new Vector2(WIDTH - 88f, 110f));

        // The picture, clipped to a rounded frame.
        var frame = Ui.Box(_card, "PictureFrame", UiTheme.Tone.Ink, 24f, false);
        Ui.Place((RectTransform)frame.transform, new Vector2(0f, 1f), new Vector2(40f, -212f), new Vector2(PICTURE_W + 16f, PICTURE_H + 16f));
        var clip = Ui.Place(Ui.Rect(frame.transform, "Clip"), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PICTURE_W, PICTURE_H));
        Thumbnail.Round(clip.gameObject);
        _illustration = new GameObject("Illustration", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
        _illustration.transform.SetParent(clip, false);
        _illustration.raycastTarget = false;

        // Goal / watch out / score: a tag (word, not colour alone) over its line.
        var rules = Ui.Place(Ui.Rect(_card, "Rules"), new Vector2(0f, 1f), new Vector2(PICTURE_W + 96f, -208f),
            new Vector2(WIDTH - PICTURE_W - 140f, PICTURE_H + 24f));
        Ui.Stack<VerticalLayoutGroup>(rules, 2, TextAnchor.UpperLeft);
        _objective = Rule(rules, "GOAL", UiTheme.Tone.Good);
        _danger = Rule(rules, "WATCH OUT", UiTheme.Tone.Warn);
        _scoring = Rule(rules, "SCORE", UiTheme.Tone.Accent);

        // Controls as key caps.
        _controls = Ui.Place(Ui.Rect(_card, "Controls"), new Vector2(0f, 1f), new Vector2(44f, -630f), new Vector2(WIDTH - 88f, 120f));

        // Contestants and the ready button.
        _chips = Ui.Place(Ui.Rect(_card, "Contestants"), new Vector2(0f, 0f), new Vector2(44f, 40f), new Vector2(WIDTH - 480f, 56f));
        Ui.Stack<HorizontalLayoutGroup>(_chips, 2, TextAnchor.MiddleLeft);
        _hint = Ui.Text(_card, "", UiTheme.TextRole.Label, UiTheme.Tone.Haze, TextAlignmentOptions.Right);
        Ui.Place(_hint.rectTransform, new Vector2(1f, 0f), new Vector2(-44f, 132f), new Vector2(380f, 36f));
        _readyButton = Ui.Button(_card, "READY", UiButton.Variant.Primary, "tick", SendReady);
        Ui.Place((RectTransform)_readyButton.transform, new Vector2(1f, 0f), new Vector2(-44f, 32f), new Vector2(320f, 92f));
        _readyButton.navigation = new Navigation { mode = Navigation.Mode.None };
        var badge = Ui.Box(_card, "YouAreReady", UiTheme.Tone.Good, 46f);
        var badgeRt = Ui.Place((RectTransform)badge.transform, new Vector2(1f, 0f), new Vector2(-44f, 32f), new Vector2(320f, 92f));
        var badgeRow = Ui.Rect(badgeRt, "Row");
        Ui.Stretch(badgeRow);
        Ui.Stack<HorizontalLayoutGroup>(badgeRow, 2, TextAnchor.MiddleCenter);
        Ui.Size(Ui.Icon(badgeRow, "tick", 44f, UiTheme.Tone.Ink), 44f, 44f);
        Ui.Text(badgeRow, "READY", UiTheme.TextRole.Button, UiTheme.Tone.Ink);
        _readyBadge = badge.gameObject;

        _group.alpha = 0f;
        _card.gameObject.SetActive(false);
        _scrim.SetActive(false);
    }

    private static TextMeshProUGUI Rule(RectTransform parent, string label, UiTheme.Tone tone)
    {
        var row = Ui.Rect(parent, label);
        Ui.Size(row, parent.sizeDelta.x, 124f);
        var tag = Ui.Box(row, "Tag", tone, 16f);
        tag.ShadowDepth = UiTheme.Instance.ShadowChip;
        tag.Apply();
        var word = Ui.Text(tag.transform, label, UiTheme.TextRole.Label, UiTheme.Tone.Ink);
        word.GetComponent<ThemedText>().SingleLine = true;
        word.GetComponent<ThemedText>().Apply();
        Ui.Stretch(word.rectTransform);
        Ui.Place((RectTransform)tag.transform, new Vector2(0f, 1f), Vector2.zero, new Vector2(word.GetPreferredValues(label).x + 36f, 40f));
        var text = Ui.Text(row, "", UiTheme.TextRole.Body, UiTheme.Tone.Paper, TextAlignmentOptions.TopLeft);
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = Vector2.zero; text.rectTransform.offsetMax = new Vector2(0f, -50f);
        return text;
    }

    private void Update()
    {
        var controller = MinigameController.Current;
        bool intro = controller != null && controller.Object != null && controller.Object.IsValid && controller.Phase == MinigamePhase.Intro;
        Show(intro);
        UpdateOrbit(intro);
        if (!intro)
        {
            WantsCursor = false;
            return;
        }

        if (controller != _shownFor || controller.Definition != _shownDefinition)
            Fill(controller);

        bool ready = controller.IsReady(controller.Runner.LocalPlayer) || _sentReady;
        _readyButton.gameObject.SetActive(!ready);
        _readyBadge.SetActive(ready);
        WantsCursor = !ready;
        if (!ready && !PauseMenu.IsOpen && UiModal.OpenCount == 0
            && (UiInput.Pressed(Key.E, GamepadButton.North) || UiInput.Pressed(Key.Space, GamepadButton.South) || UiInput.Pressed(Key.Enter)))
            SendReady();
        if (_shownPad != PromptBar.UsingGamepad) FillControls(controller);

        int seconds = Mathf.CeilToInt(controller.TimeRemaining);
        if (controller.ReadyMask != _lastReadyMask || seconds != _lastSeconds)
        {
            _lastReadyMask = controller.ReadyMask;
            _lastSeconds = seconds;
            RefreshReady(controller, ready, seconds);
        }
    }

    private void OnDestroy() => WantsCursor = false;

    private void Fill(MinigameController controller)
    {
        if (controller != _shownFor) _sentReady = false;
        _shownFor = controller;
        _shownDefinition = controller.Definition;
        _lastReadyMask = -1;
        var def = controller.Definition;
        var gm = NetworkGameManager.Instance;
        _round.text = gm != null && gm.RoundCount > 0 ? $"ROUND {gm.RoundIndex + 1}/{gm.RoundCount}" : "NEXT ROUND";
        _map.text = controller.VariantName ?? "";
        _title.text = def != null ? def.DisplayName.ToUpperInvariant() : "";
        _objective.text = def != null && !string.IsNullOrEmpty(def.Objective) ? def.Objective : def?.Description;
        _danger.text = def?.Danger ?? "";
        _scoring.text = def?.Scoring ?? "";
        _danger.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(_danger.text));
        _scoring.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(_scoring.text));
        _illustration.sprite = def != null ? def.PreviewImage : null;
        _illustration.enabled = _illustration.sprite != null;
        Thumbnail.Cover(_illustration, new Vector2(PICTURE_W, PICTURE_H));
        FillControls(controller);
        UiMotion.PanelIn(_card, _group);
    }

    /// <summary>The controls as caps in the device used last (rebuilt when it changes).</summary>
    private void FillControls(MinigameController controller)
    {
        _shownPad = PromptBar.UsingGamepad;
        for (int i = _controls.childCount - 1; i >= 0; i--) Destroy(_controls.GetChild(i).gameObject);
        var def = controller.Definition;
        if (controller.UsesDefaultControls)
        {
            AddKey(PromptBar.Cap("WASD"), "Move");
            AddKey(PromptBar.Cap("SPACE"), "Jump");
        }
        if (def != null && def.Controls != null)
            foreach (var pair in def.Controls)
            {
                int bar = pair.IndexOf('|');
                if (bar > 0) AddKey(PromptBar.Cap(pair.Substring(0, bar)), pair.Substring(bar + 1));
            }

        // Flow the caps into rows that fit the card (long lists take two lines).
        float x = 0f, y = 0f, gap = UiTheme.Instance.Gap(4), row = 58f;
        foreach (RectTransform item in _controls)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(item);
            float width = LayoutUtility.GetPreferredWidth(item);
            if (x > 0f && x + width > _controls.sizeDelta.x) { x = 0f; y -= row; }
            Ui.Place(item, new Vector2(0f, 1f), new Vector2(x, y), new Vector2(width, 50f));
            x += width + gap;
        }
    }

    private void AddKey(string key, string action)
    {
        var item = Ui.Rect(_controls, action);
        Ui.Stack<HorizontalLayoutGroup>(item, 1, TextAnchor.MiddleLeft);
        var cap = Ui.Box(item, "Cap", UiTheme.Tone.Paper, 10f);
        cap.ShadowDepth = UiTheme.Instance.ShadowChip;
        cap.Apply();
        var capText = Ui.Text(cap.transform, key, UiTheme.TextRole.Label, UiTheme.Tone.Ink);
        var themed = capText.GetComponent<ThemedText>();
        themed.SingleLine = true;
        themed.ShrinkToFit = false;
        themed.Apply();
        Ui.Stretch(capText.rectTransform, 4f);
        Ui.Size(cap, capText.GetPreferredValues(key).x + 28f, 46f);
        var verb = Ui.Text(item, action, UiTheme.TextRole.Body, UiTheme.Tone.Paper, TextAlignmentOptions.Left);
        verb.GetComponent<ThemedText>().SingleLine = true;
        verb.GetComponent<ThemedText>().Apply();
        verb.fontStyle = FontStyles.Bold;
    }

    private void RefreshReady(MinigameController controller, bool localReady, int seconds)
    {
        var spawner = FindFirstObjectByType<BasePlayerSpawner>();
        var match = NetworkGameManager.Instance;
        int shown = 0;
        if (spawner != null && spawner.Object != null)
        {
            foreach (var entry in spawner.Players)
            {
                var player = entry.Value;
                if (player == null || player.Object == null) continue;
                if (shown >= _chipPool.Count)
                {
                    var chip = PlayerChip.Create(_chips, 56f);
                    Ui.Size(chip, 250f, 56f);
                    _chipPool.Add(chip);
                }
                string name = string.IsNullOrEmpty(player.Nickname) ? $"Player {entry.Key.PlayerId}" : player.Nickname;
                int colour = match != null && match.Object != null && match.Object.IsValid ? match.ColorIndexOf(entry.Key) : -1;
                _chipPool[shown].gameObject.SetActive(true);
                _chipPool[shown].Set(colour >= 0 ? colour + 1 : entry.Key.PlayerId, player.PlayerColor, name, controller.IsReady(entry.Key) ? "tick" : null);
                shown++;
            }
        }
        for (int i = shown; i < _chipPool.Count; i++) _chipPool[i].gameObject.SetActive(false);
        _hint.text = $"Starting in {seconds}";
    }

    private void Show(bool visible)
    {
        if (visible == _visible) return;
        _visible = visible;
        _scrim.SetActive(visible);
        _group.blocksRaycasts = visible;
        if (_prompts != null) { Destroy(_prompts.gameObject); _prompts = null; }
        if (visible)
        {
            _card.gameObject.SetActive(true);
            _prompts = PromptBar.Create(_card.parent, (PromptBar.Action.Confirm, "Ready"));
        }
        else
            UiMotion.PanelOut(_card, _group).OnComplete(() => { if (!_visible) _card.gameObject.SetActive(false); });
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
}
