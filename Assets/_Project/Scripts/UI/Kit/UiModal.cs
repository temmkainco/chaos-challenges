using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// A modal: the world dims behind a panel with a title, an optional line of text, any custom content and a row of
/// buttons. Opens with a pop, focuses its first button (gamepad ready), and Back (Esc / B) cancels it.
/// <see cref="Show"/> builds one on a top canvas.
/// </summary>
[DefaultExecutionOrder(-100)]   // on top of every screen: takes Back first
public class UiModal : MonoBehaviour
{
    private RectTransform _panel;
    private CanvasGroup _group;
    private Action _onCancel;
    private GameObject _restoreFocus;
    private GameObject _focus;

    public RectTransform Content { get; private set; }
    public RectTransform Buttons { get; private set; }
    /// <summary>False for a modal Back must not close (a "connecting" notice).</summary>
    public bool Cancelable { get; set; } = true;
    /// <summary>Modals on screen; screens underneath ignore Back while one is open.</summary>
    public static int OpenCount { get; private set; }

    /// <summary>A modal with the given buttons (label, variant, action); the last one is the default focus if it is Primary.</summary>
    public static UiModal Show(string title, string text, Action onCancel, params (string label, UiButton.Variant variant, Action action)[] buttons)
    {
        var safe = Ui.Canvas("Modal", 900);
        var canvas = safe.parent;
        var scrim = new GameObject("Scrim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        scrim.transform.SetParent(canvas, false);
        scrim.transform.SetAsFirstSibling();
        Ui.Stretch((RectTransform)scrim.transform);
        var theme = UiTheme.Instance;
        scrim.GetComponent<Image>().color = new Color(theme.Ink.r, theme.Ink.g, theme.Ink.b, theme.ScrimAlpha);

        var modal = canvas.gameObject.AddComponent<UiModal>();
        OpenCount++;
        modal._onCancel = onCancel;
        modal._restoreFocus = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        var box = Ui.Box(safe, "Panel", UiTheme.Tone.Studio);
        modal._panel = Ui.Place((RectTransform)box.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(880f, 0f));
        modal._group = box.gameObject.AddComponent<CanvasGroup>();
        var fit = box.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var column = Ui.Stack<VerticalLayoutGroup>(modal._panel, 4);
        column.padding = new RectOffset(56, 56, 48, 56);
        column.childControlHeight = true;

        var heading = Ui.Text(modal._panel, title, UiTheme.TextRole.DisplayM, UiTheme.Tone.Paper);
        Ui.Size(heading, 760f, 80f);
        if (!string.IsNullOrEmpty(text))
        {
            var body = Ui.Text(modal._panel, text, UiTheme.TextRole.Body, UiTheme.Tone.Haze);
            Ui.Size(body, 760f, -1f);
        }
        modal.Content = Ui.Rect(modal._panel, "Content");
        Ui.Stack<VerticalLayoutGroup>(modal.Content, 3);
        modal.Buttons = Ui.Rect(modal._panel, "Buttons");
        var row = Ui.Stack<HorizontalLayoutGroup>(modal.Buttons, 3, TextAnchor.MiddleCenter);
        Ui.Size(modal.Buttons, 760f, 100f);

        UiButton focus = null;
        var order = new System.Collections.Generic.List<Selectable>();
        foreach (var (label, variant, action) in buttons)
        {
            var b = Ui.Button(modal.Buttons, label, variant, null, () => { action?.Invoke(); modal.Close(); });
            Ui.Size(b, 340f, 88f);
            order.Add(b);
            if (focus == null || variant == UiButton.Variant.Primary) focus = b;
        }
        modal.Buttons.gameObject.SetActive(buttons.Length > 0);
        UiFocus.Chain(order.ToArray());   // focus stays inside the modal
        UiMotion.PanelIn(modal._panel, modal._group);
        modal._focus = focus != null ? focus.gameObject : null;
        if (focus != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(focus.gameObject);
        return modal;
    }

    private void Update()
    {
        UiFocus.Keep(_focus);   // e.g. the scene behind reloaded and took the selection with it
        if (UiInput.Back() && Cancelable)
        {
            _onCancel?.Invoke();
            Close();
        }
    }

    private void OnDestroy() => OpenCount--;

    public void Close()
    {
        if (!enabled) return;
        enabled = false;
        UiMotion.PanelOut(_panel, _group).OnComplete(() =>
        {
            if (_restoreFocus != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_restoreFocus);
            Destroy(gameObject);
        });
    }
}
