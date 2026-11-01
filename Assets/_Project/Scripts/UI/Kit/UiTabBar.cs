using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// A row of tabs (customize: body / head / items ...). The selected tab is the Primary button and sits raised; the
/// others are Quiet. Gamepad shoulders and Q / E switch tabs while the bar is on screen.
/// </summary>
public class UiTabBar : MonoBehaviour
{
    public event Action<int> Selected;
    private readonly List<UiButton> _tabs = new();
    public int Index { get; private set; } = -1;
    public IReadOnlyList<UiButton> Tabs => _tabs;

    public static UiTabBar Create(Transform parent, params (string label, string icon)[] tabs)
    {
        var rt = Ui.Rect(parent, "TabBar");
        var bar = rt.gameObject.AddComponent<UiTabBar>();
        Ui.Stack<HorizontalLayoutGroup>(rt, 2, TextAnchor.MiddleCenter);
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            var b = Ui.Button(rt, tabs[i].label, UiButton.Variant.Quiet, tabs[i].icon, () => bar.Select(index));
            Ui.Size(b, 260f, 76f);
            bar._tabs.Add(b);
        }
        bar.Select(0);
        return bar;
    }

    public void Select(int index)
    {
        index = (index % _tabs.Count + _tabs.Count) % _tabs.Count;
        if (index == Index) return;
        Index = index;
        for (int i = 0; i < _tabs.Count; i++)
            _tabs[i].SetVariant(i == index ? UiButton.Variant.Primary : UiButton.Variant.Quiet);
        Selected?.Invoke(index);
    }

    private void Update()
    {
        var pad = Gamepad.current;
        var keys = Keyboard.current;
        if ((pad != null && pad.leftShoulder.wasPressedThisFrame) || (keys != null && keys.qKey.wasPressedThisFrame)) Select(Index - 1);
        if ((pad != null && pad.rightShoulder.wasPressedThisFrame) || (keys != null && keys.eKey.wasPressedThisFrame)) Select(Index + 1);
    }
}
