using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// A labelled row whose value steps left and right (quality, rounds, hats): the arrows are clickable, and with the
/// row focused (keyboard / gamepad) left and right step it while up and down move between rows. Focus shows as the
/// Brand ring.
/// </summary>
public class UiStepper : Selectable, IMoveHandler, ISubmitHandler, IPointerClickHandler
{
    /// <summary>Confirm (Enter / A) or a click on the row: e.g. opens a colour picker.</summary>
    public event Action Pressed;
    /// <summary>The value box between the arrows (room for a swatch).</summary>
    public RectTransform ValueBox { get; private set; }
    /// <summary>Called after every refresh (e.g. to tint a swatch).</summary>
    public Action Refreshed;
    private Action<int> _step;
    private Func<string> _value;
    private TextMeshProUGUI _valueText;
    private UiBox _ring;

    public static UiStepper Create(Transform parent, string label, Func<string> value, Action<int> step, float width = 860f)
    {
        var row = Ui.Rect(parent, "Stepper " + label);
        Ui.Size(row, width, 72f);
        var stepper = row.gameObject.AddComponent<UiStepper>();
        stepper._value = value;
        stepper._step = step;
        var theme = UiTheme.Instance;

        stepper._ring = Ui.Box(row, "FocusRing", UiTheme.Tone.Brand, -1f, false);
        stepper._ring.HasOutline = false;
        Ui.Stretch((RectTransform)stepper._ring.transform, -6f);
        stepper._ring.gameObject.SetActive(false);
        // An invisible full-row graphic so the whole row takes the pointer.
        var hit = row.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);
        stepper.targetGraphic = hit;

        var text = Ui.Text(row, label, UiTheme.TextRole.Body, UiTheme.Tone.Paper, TextAlignmentOptions.Left);
        Ui.Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(width * 0.42f, 64f));

        var box = Ui.Box(row, "Value", UiTheme.Tone.Ink, 18f, false);
        var boxRt = stepper.ValueBox = Ui.Place((RectTransform)box.transform, new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(width * 0.52f, 60f));
        stepper._valueText = Ui.Text(boxRt, "", UiTheme.TextRole.Body, UiTheme.Tone.Paper);
        Ui.Stretch(stepper._valueText.rectTransform, 0f);
        stepper._valueText.rectTransform.offsetMin = new Vector2(64f, 0f);
        stepper._valueText.rectTransform.offsetMax = new Vector2(-64f, 0f);
        Arrow(boxRt, "left", new Vector2(0f, 0.5f), () => stepper.Step(-1));
        Arrow(boxRt, "right", new Vector2(1f, 0.5f), () => stepper.Step(1));
        stepper.transition = Transition.None;
        stepper.Refresh();
        return stepper;
    }

    private static void Arrow(RectTransform parent, string icon, Vector2 anchor, Action click)
    {
        var image = Ui.Icon(parent, icon, 40f, UiTheme.Tone.Accent);
        image.raycastTarget = true;
        Ui.Place(image.rectTransform, anchor, new Vector2(anchor.x == 0f ? 12f : -12f, 0f), new Vector2(44f, 44f));
        var button = image.gameObject.AddComponent<Button>();
        button.transition = Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };   // the row is the focusable part
        button.onClick.AddListener(() => click());
    }

    public void Step(int delta)
    {
        if (!IsInteractable()) return;
        _step?.Invoke(delta);
        Sfx.Play(Sound.Click, 0.7f);
        Refresh();
        UiMotion.Punch(_valueText.rectTransform, 0.12f);
    }

    public void Refresh()
    {
        if (_valueText != null && _value != null) _valueText.text = _value();
        Refreshed?.Invoke();
    }

    /// <summary>A colour dot at the start of the value box (colour rows); returns the dot to tint.</summary>
    public Image AddSwatch()
    {
        var ring = Ui.Circle(ValueBox, "SwatchRing", 44f, UiTheme.Instance.Paper);
        Ui.Place(ring.rectTransform, new Vector2(0f, 0.5f), new Vector2(66f, 0f), new Vector2(44f, 44f));
        var dot = Ui.Circle(ring.transform, "Swatch", 34f, Color.white);
        _valueText.rectTransform.offsetMin = new Vector2(122f, 0f);
        return dot;
    }

    public void OnSubmit(BaseEventData eventData) { if (IsInteractable()) Pressed?.Invoke(); }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (IsInteractable() && eventData.button == PointerEventData.InputButton.Left) Pressed?.Invoke();
    }

    public override void OnMove(AxisEventData eventData)
    {
        if (eventData.moveDir == MoveDirection.Left) { Step(-1); eventData.Use(); return; }
        if (eventData.moveDir == MoveDirection.Right) { Step(1); eventData.Use(); return; }
        base.OnMove(eventData);
    }

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        base.DoStateTransition(state, instant);
        if (_ring != null) _ring.gameObject.SetActive(state == SelectionState.Selected);
    }
}
