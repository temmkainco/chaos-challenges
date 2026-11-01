using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// The focus ring for selectables that aren't <see cref="UiButton"/>s (sliders, toggles): a Brand ring around
/// <see cref="Target"/> while the EventSystem has this object selected (keyboard / gamepad), so focus never relies on
/// colour alone.
/// </summary>
public class UiFocus : MonoBehaviour, ISelectHandler, IDeselectHandler
{
    public RectTransform Target;
    private UiBox _ring;

    private void Awake() => Build();

    private void Build()
    {
        if (_ring != null || Target == null) return;
        _ring = Ui.Box(Target, "FocusRing", UiTheme.Tone.Brand, -1f, false);
        _ring.HasOutline = false;
        _ring.transform.SetAsFirstSibling();
        float ring = UiTheme.Instance.FocusRing + UiTheme.Instance.Outline;
        Ui.Stretch((RectTransform)_ring.transform, -ring);
        _ring.gameObject.SetActive(false);
    }

    /// <summary>
    /// Keeps keyboard / gamepad focus alive: when nothing usable is selected (a mouse click on empty space, a closed
    /// view) and the player reaches for the arrows, the stick or the d-pad, focus goes to <paramref name="fallback"/>.
    /// </summary>
    public static void Keep(GameObject fallback)
    {
        var events = EventSystem.current;
        if (events == null || fallback == null) return;
        var current = events.currentSelectedGameObject;
        if (current != null && current.activeInHierarchy) return;
        var keys = UnityEngine.InputSystem.Keyboard.current;
        var pad = UnityEngine.InputSystem.Gamepad.current;
        bool reach = (keys != null && (keys.upArrowKey.wasPressedThisFrame || keys.downArrowKey.wasPressedThisFrame || keys.tabKey.wasPressedThisFrame
                                      || keys.enterKey.wasPressedThisFrame || keys.spaceKey.wasPressedThisFrame))
                     || (pad != null && (pad.dpad.ReadValue().sqrMagnitude > 0.25f || pad.leftStick.ReadValue().sqrMagnitude > 0.25f
                                         || pad.buttonSouth.wasPressedThisFrame));
        if (reach || current != null) events.SetSelectedGameObject(fallback);
    }

    /// <summary>
    /// Explicit keyboard / gamepad order for a screen: rows top to bottom, each row's items left to right (a code
    /// field and its button, Random and Done). Up / down reach the nearest item of the next row; left / right stay in
    /// the row (and are left free on single items, so sliders and steppers use them). Nulls are skipped.
    /// </summary>
    public static void Chain(params Selectable[][] rows)
    {
        var list = new System.Collections.Generic.List<Selectable[]>();
        foreach (var row in rows)
        {
            var items = System.Array.FindAll(row ?? System.Array.Empty<Selectable>(), s => s != null);
            if (items.Length > 0) list.Add(items);
        }
        for (int r = 0; r < list.Count; r++)
            for (int i = 0; i < list[r].Length; i++)
            {
                Selectable Pick(int row) => row < 0 || row >= list.Count ? null : list[row][Mathf.Min(i, list[row].Length - 1)];
                list[r][i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = Pick(r - 1),
                    selectOnDown = Pick(r + 1),
                    selectOnLeft = i > 0 ? list[r][i - 1] : null,
                    selectOnRight = i < list[r].Length - 1 ? list[r][i + 1] : null,
                };
            }
    }

    public void OnSelect(BaseEventData eventData) { Build(); if (_ring != null) _ring.gameObject.SetActive(true); }
    public void OnDeselect(BaseEventData eventData) { if (_ring != null) _ring.gameObject.SetActive(false); }
}
