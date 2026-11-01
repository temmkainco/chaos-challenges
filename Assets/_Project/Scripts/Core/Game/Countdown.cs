using DG.Tweening;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The 3-2-1 (lobby start, round start) as a numbered disc that stamps in on each second, then GO!; and a small
/// version up top for the last seconds of a timed round. Built with the UI kit on its own canvas in this object's scene.
/// </summary>
public class Countdown : MonoBehaviour
{
    private const float HERO = 1.9f, CORNER = 0.6f;

    private RectTransform _canvas, _disc;
    private CanvasGroup _group;
    private UiBox _box;
    private TMPro.TextMeshProUGUI _number;
    private bool _shown;

    /// <summary>Built on first use: the prefab's own bare Countdown never shows, only the scene's lobby / round one.</summary>
    private void Build()
    {
        if (_canvas != null) return;
        var safe = Ui.Canvas("Countdown", 60);
        _canvas = (RectTransform)safe.parent;
        SceneManager.MoveGameObjectToScene(_canvas.gameObject, gameObject.scene);
        _box = Ui.Box(safe, "Disc", UiTheme.Tone.Brand, 110f);
        _disc = Ui.Place((RectTransform)_box.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 220f));
        _group = _disc.gameObject.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        _number = Ui.Text(_disc, "", UiTheme.TextRole.DisplayXL, UiTheme.Tone.Paper, TMPro.TextAlignmentOptions.Center, true);
        _number.GetComponent<ThemedText>().SingleLine = true;
        _number.GetComponent<ThemedText>().Apply();
        Ui.Stretch(_number.rectTransform);
        _number.rectTransform.offsetMin = new Vector2(-60f, 0f);
        _number.rectTransform.offsetMax = new Vector2(60f, 6f);
        _disc.gameObject.SetActive(false);
    }

    protected virtual void OnDestroy()
    {
        if (_canvas != null) Destroy(_canvas.gameObject);
    }

    /// <summary>Big, in the middle (<paramref name="corner"/>: small, top centre, so the arena stays in view).</summary>
    protected void ShowCountdown(bool corner = false)
    {
        Build();
        _shown = true;
        _disc.DOKill();
        _disc.anchorMin = _disc.anchorMax = corner ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0.5f);
        _disc.pivot = corner ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0.5f);
        _disc.anchoredPosition = corner ? new Vector2(0f, -150f) : new Vector2(0f, 40f);
        _disc.localScale = Vector3.one * (corner ? CORNER : HERO);
        _box.SetFill(corner ? UiTheme.Tone.Bad : UiTheme.Tone.Brand);
        _number.text = "";
        _disc.sizeDelta = new Vector2(220f, 220f);
        _disc.gameObject.SetActive(true);
        _group.alpha = 1f;
    }

    protected void ShowCountdown() => ShowCountdown(false);

    protected virtual void HideCountdown()
    {
        if (!_shown || _canvas == null) return;
        _shown = false;
        _group.DOKill();
        _group.DOFade(0f, UiTheme.Instance.PanelOut).SetUpdate(true).SetLink(_disc.gameObject)
            .OnComplete(() => { if (!_shown) _disc.gameObject.SetActive(false); });
    }

    protected virtual void UpdateCountdown(int seconds)
    {
        if (!_shown) ShowCountdown();
        Sfx.Play(Sound.Tick, 0.8f, 0f);
        _number.text = seconds.ToString();
        Stamp();
    }

    /// <summary>GO! in Good, then away.</summary>
    protected void ShowGo()
    {
        if (!_shown || _canvas == null || _disc.localScale.x < 1f) { HideCountdown(); return; }
        _box.SetFill(UiTheme.Tone.Good);
        _disc.sizeDelta = new Vector2(330f, 220f);   // a pill: GO! is wider than a digit
        _number.text = "GO!";
        Stamp();
        _shown = false;
        DOVirtual.DelayedCall(0.55f, () =>
        {
            if (_shown || _disc == null) return;
            _group.DOFade(0f, UiTheme.Instance.PanelOut).SetUpdate(true).SetLink(_disc.gameObject)
                .OnComplete(() => { if (!_shown) _disc.gameObject.SetActive(false); });
        }, true).SetLink(_disc.gameObject);
    }

    private void Stamp()
    {
        float scale = _disc.localScale.x < 1f ? CORNER : HERO;
        _disc.DOKill();
        _group.alpha = 1f;
        _disc.localScale = Vector3.one * scale;
        if (UiTheme.ReduceMotion) return;
        _disc.localScale = Vector3.one * scale * 1.35f;
        _disc.DOScale(scale, UiTheme.Instance.Stamp).SetEase(Ease.OutBack, UiTheme.Instance.Overshoot * 1.3f).SetUpdate(true);
    }
}
