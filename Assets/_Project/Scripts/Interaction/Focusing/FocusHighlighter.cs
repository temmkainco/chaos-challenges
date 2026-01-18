using UnityEngine;
using UnityEngine.UI;

public class FocusHighlighter
{
    private Outline _currentOutline;

    public void Highlight(IInteractable target)
    {
        if (_currentOutline != null)
            return;
        _currentOutline = target.Outline;
        _currentOutline.enabled = true;
    }

    public void Clear()
    {
        if (_currentOutline == null) return;
        _currentOutline.enabled = false;
        _currentOutline = null;
    }
}
