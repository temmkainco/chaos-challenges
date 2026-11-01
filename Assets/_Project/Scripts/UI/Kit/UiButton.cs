using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The kit's button: a <see cref="UiBox"/> with an optional icon and label, and five clearly different states that
/// don't rely on colour: hover lifts it, pressed sinks it into its shadow, focus (keyboard or gamepad) adds a thick
/// Brand ring and grows it slightly, disabled flattens and greys it with a lock icon. Works with the EventSystem's
/// navigation (gamepad / arrow keys) like any Selectable.
/// </summary>
public class UiButton : Selectable, IPointerClickHandler, ISubmitHandler
{
    public enum Variant { Primary, Secondary, Danger, Quiet }

    public UnityEvent onClick = new();
    [SerializeField] private Variant _variant = Variant.Secondary;

    private UiBox _box, _ring;
    private RectTransform _content;
    private TextMeshProUGUI _label;
    private Image _icon, _lock;
    private SelectionState _state;

    public TextMeshProUGUI Label => _label;
    public Variant Kind => _variant;

    /// <summary>Builds the visuals (from code). <paramref name="icon"/>: a <see cref="UiShapes.Icon"/> name.</summary>
    public void Build(string label, Variant variant, string icon = null)
    {
        _variant = variant;
        var rt = (RectTransform)transform;
        if (rt.sizeDelta == Vector2.zero) rt.sizeDelta = new Vector2(420f, 88f);

        // Focus ring behind everything: a Brand box a few units bigger than the button.
        _ring = Ui.Box(transform, "FocusRing", UiTheme.Tone.Brand, -1f, false);
        _ring.HasOutline = false;
        _box = Ui.Box(transform, "Box", Fill(variant));
        Ui.Stretch((RectTransform)_box.transform);

        _content = Ui.Rect(_box.transform, "Content");
        Ui.Stretch(_content);
        var row = Ui.Stack<HorizontalLayoutGroup>(_content, 2, TextAnchor.MiddleCenter);
        row.padding = new RectOffset(28, 28, 0, 0);
        if (!string.IsNullOrEmpty(icon))
        {
            _icon = Ui.Icon(_content, icon, 44f, TextTone(variant));
            Ui.Size(_icon, 44f, 44f);
        }
        if (!string.IsNullOrEmpty(label))
        {
            _label = Ui.Text(_content, label, UiTheme.TextRole.Button, TextTone(variant));
            _label.textWrappingMode = TextWrappingModes.NoWrap;
        }
        _lock = Ui.Icon(_box.transform, "lock", 34f, UiTheme.Tone.Haze);
        Ui.Place((RectTransform)_lock.transform, new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(34f, 34f));
        _lock.gameObject.SetActive(false);

        transition = Transition.None;
        targetGraphic = _box.FillImage;   // last: setting it re-runs the state transition
        Apply(SelectionState.Normal, true);
    }

    public void SetLabel(string text)
    {
        if (_label != null) _label.text = text;
    }

    public void SetVariant(Variant variant)
    {
        _variant = variant;
        if (_box == null) return;
        _box.SetFill(Fill(variant));
        if (_label != null) _label.GetComponent<ThemedText>().Set(UiTheme.TextRole.Button, TextTone(variant));
        if (_icon != null) _icon.color = UiTheme.Instance.Color(TextTone(variant));
    }

    private static UiTheme.Tone Fill(Variant v) => v switch
    {
        Variant.Primary => UiTheme.Tone.Accent,
        Variant.Danger => UiTheme.Tone.Bad,
        Variant.Quiet => UiTheme.Tone.Studio,
        _ => UiTheme.Tone.Riser,
    };

    private static UiTheme.Tone TextTone(Variant v) => v == Variant.Primary ? UiTheme.Tone.Ink : UiTheme.Tone.Paper;

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        base.DoStateTransition(state, instant);
        _state = state;
        Apply(state, instant);
    }

    private void Apply(SelectionState state, bool instant)
    {
        if (_box == null || _ring == null) return;
        var theme = UiTheme.Instance;
        float duration = instant ? 0f : theme.Feedback;
        bool disabled = state == SelectionState.Disabled;
        bool focused = state == SelectionState.Selected;
        bool pressed = state == SelectionState.Pressed;
        bool hover = state == SelectionState.Highlighted;

        // Depth: lift on hover, sink into the shadow when pressed.
        float shadow = disabled ? 0f : pressed ? 0f : hover ? theme.Shadow + 5f : theme.Shadow;
        float drop = pressed ? -theme.Shadow : hover ? 5f : 0f;
        _box.SetShadow(shadow);
        var boxRt = (RectTransform)_box.transform;
        boxRt.DOKill();
        boxRt.DOAnchorPosY(drop, duration).SetUpdate(true);

        // Focus: ring + slight growth (visible without colour).
        float ring = theme.FocusRing + theme.Outline;
        var ringRt = (RectTransform)_ring.transform;
        Ui.Stretch(ringRt, -ring);
        ringRt.offsetMin = new Vector2(-ring, -ring - theme.Shadow);
        _ring.gameObject.SetActive(focused);
        transform.DOKill();
        transform.DOScale(focused && !UiTheme.ReduceMotion ? theme.FocusScale : 1f, duration).SetUpdate(true);

        // Disabled: flat, greyed, lock icon.
        _box.SetFill(disabled ? UiTheme.Tone.Studio : Fill(_variant));
        var tone = disabled ? UiTheme.Tone.Haze : TextTone(_variant);
        if (_label != null) _label.color = theme.Color(tone);
        if (_icon != null) _icon.color = theme.Color(tone);
        if (_lock != null) _lock.gameObject.SetActive(disabled);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        Press();
    }

    public void OnSubmit(BaseEventData eventData)
    {
        Press();
        if (!IsActive() || !IsInteractable()) return;
        // Show the press on submit (keyboard / gamepad) too.
        DoStateTransition(SelectionState.Pressed, false);
        DOVirtual.DelayedCall(UiTheme.Instance.Feedback * 1.5f, () => { if (this != null) DoStateTransition(currentSelectionState, false); }).SetUpdate(true);
    }

    private void Press()
    {
        if (!IsActive() || !IsInteractable()) return;
        Sfx.Play(Sound.Click, 0.8f);
        onClick.Invoke();
    }
}
