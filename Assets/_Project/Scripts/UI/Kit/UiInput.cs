using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Shared menu input. Back (Esc / gamepad B) goes to one screen per press: the first open screen that asks takes
/// it, and screens that sit on top run earlier (<see cref="DefaultExecutionOrder"/>: modals, then panels, then the
/// pause menu), so one press closes one thing.
/// </summary>
public static class UiInput
{
    private static int _backFrame = -1;

    /// <summary>Back was pressed this frame and nobody took it yet; the caller takes it. Ask only while open.</summary>
    public static bool Back()
    {
        if (_backFrame == Time.frameCount || !BackDown) return false;
        _backFrame = Time.frameCount;
        return true;
    }

    /// <summary>A screen already took this frame's Back press.</summary>
    public static bool BackTaken => _backFrame == Time.frameCount;

    private static bool BackDown =>
        (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

    /// <summary>The pause button: Esc or the gamepad's Start (Menu) button.</summary>
    public static bool Pause =>
        (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);

    /// <summary>A key or a gamepad button pressed this frame (either may be null).</summary>
    public static bool Pressed(Key key, GamepadButton? pad = null)
    {
        if (key != Key.None && Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame) return true;
        return pad.HasValue && Gamepad.current != null && Gamepad.current[pad.Value].wasPressedThisFrame;
    }

    /// <summary>A key or a gamepad button held down.</summary>
    public static bool Held(Key key, GamepadButton? pad = null)
    {
        if (key != Key.None && Keyboard.current != null && Keyboard.current[key].isPressed) return true;
        return pad.HasValue && Gamepad.current != null && Gamepad.current[pad.Value].isPressed;
    }
}
