using System;
using UnityEngine;

[Serializable]
public struct FocusHint
{
    [SerializeField] private string _text;
    [SerializeField] private InputButtons _button;

    public string Text => _text;
    public InputButtons Button => _button;

    public FocusHint(string text, InputButtons button)
    {
        _text = text;
        _button = button;
    }
}
