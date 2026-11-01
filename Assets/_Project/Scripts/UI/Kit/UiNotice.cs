using DG.Tweening;
using UnityEngine;

/// <summary>
/// The game's two notices, as kit modals that survive scene loads (a failed join reloads the menu; a join lands in
/// the lobby): a busy one ("TUNING IN") that Back cannot close, and an error with OK. One of each at a time: a newer
/// error replaces the one on screen (a retried join reports more than once).
/// </summary>
public static class UiNotice
{
    private static UiModal _busy, _error;

    /// <summary>A host / join is in progress (its own result explains any failure).</summary>
    public static bool IsBusy => _busy != null;

    public static UiModal Busy(string title)
    {
        EndBusy();
        _busy = UiModal.Show(title, null, null);
        _busy.Cancelable = false;
        var dots = Ui.Text(_busy.Content, "• • •", UiTheme.TextRole.Title, UiTheme.Tone.Accent);
        Ui.Size(dots, 200f, 56f);
        if (!UiTheme.ReduceMotion)
            dots.DOFade(0.2f, 0.5f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true).SetLink(dots.gameObject);
        Object.DontDestroyOnLoad(_busy.gameObject);
        return _busy;
    }

    public static void EndBusy()
    {
        if (_busy != null) _busy.Close();
        _busy = null;
    }

    public static void Error(string title, string text)
    {
        if (_error != null) _error.Close();
        _error = UiModal.Show(title, text, null, ("OK", UiButton.Variant.Primary, null));
        Object.DontDestroyOnLoad(_error.gameObject);
    }
}
