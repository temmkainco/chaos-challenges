using UnityEngine;
using UnityEngine.EventSystems;

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

    public void OnSelect(BaseEventData eventData) { Build(); if (_ring != null) _ring.gameObject.SetActive(true); }
    public void OnDeselect(BaseEventData eventData) { if (_ring != null) _ring.gameObject.SetActive(false); }
}
