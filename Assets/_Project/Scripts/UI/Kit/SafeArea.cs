using UnityEngine;

/// <summary>
/// Keeps a canvas's content inside the graphics-safe area: the theme's margin on every side (TV overscan, broadcast
/// convention) and the device's own safe area (notches, rounded corners). Anchor HUD corners to this, not the screen.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    private Rect _applied;
    private Vector2Int _screen;
    private float _margin = -1f;

    private void OnEnable() => Fit();
    private void Update()
    {
        if (Screen.safeArea != _applied || _screen.x != Screen.width || _screen.y != Screen.height || !Mathf.Approximately(_margin, UiTheme.Instance.SafeArea))
            Fit();
    }

    private void Fit()
    {
        var rt = (RectTransform)transform;
        var safe = Screen.safeArea;
        _applied = safe;
        _screen = new Vector2Int(Screen.width, Screen.height);
        _margin = UiTheme.Instance.SafeArea;
        if (Screen.width <= 0 || Screen.height <= 0) return;
        // The device's safe area, then the broadcast margin of the whole screen on each side.
        float mx = Screen.width * _margin, my = Screen.height * _margin;
        var min = new Vector2(Mathf.Max(safe.xMin, mx) / Screen.width, Mathf.Max(safe.yMin, my) / Screen.height);
        var max = new Vector2(Mathf.Min(safe.xMax, Screen.width - mx) / Screen.width, Mathf.Min(safe.yMax, Screen.height - my) / Screen.height);
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}
