/// <summary>Outlines the interactable the local player is looking at.</summary>
public class FocusHighlighter
{
    private Outline _currentOutline;

    public void Highlight(IInteractable target)
    {
        var outline = target != null && target.CanBeInteractedWith ? target.Outline : null;
        if (outline == _currentOutline)
            return;

        Clear();
        if (outline == null)
            return;

        _currentOutline = outline;
        _currentOutline.enabled = true;
    }

    public void Clear()
    {
        if (_currentOutline != null)
            _currentOutline.enabled = false;
        _currentOutline = null;
    }
}
