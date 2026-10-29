using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Applies the mouse sensitivity and invert-Y settings to the local player's orbit camera, and freezes the
/// camera while the pause menu is open.
/// </summary>
public class CameraSensitivity : MonoBehaviour
{
    private CinemachineInputAxisController _input;
    private readonly List<float> _baseGains = new();

    public static void Attach(Player player)
    {
        if (player.Camera == null) return;
        var input = player.Camera.GetComponentInChildren<CinemachineInputAxisController>(true);
        if (input == null) return;
        var sensitivity = input.gameObject.GetComponent<CameraSensitivity>();
        if (sensitivity == null) sensitivity = input.gameObject.AddComponent<CameraSensitivity>();
        sensitivity.Bind(input);
    }

    private void Bind(CinemachineInputAxisController input)
    {
        _input = input;
        _baseGains.Clear();
        foreach (var controller in _input.Controllers)
            _baseGains.Add(controller.Input.Gain);

        GameSettings.Changed += Apply;
        Apply();
    }

    private void OnDestroy() => GameSettings.Changed -= Apply;

    private void Update()
    {
        if (_input != null)
            _input.enabled = !PauseMenu.IsOpen;
    }

    private void Apply()
    {
        if (_input == null) return;
        for (int i = 0; i < _input.Controllers.Count && i < _baseGains.Count; i++)
        {
            var controller = _input.Controllers[i];
            bool vertical = controller.Name != null && controller.Name.Contains("Y");
            bool zoom = controller.Name != null && (controller.Name.Contains("Scale") || controller.Name.Contains("Zoom"));
            float gain = _baseGains[i];
            if (!zoom) gain *= GameSettings.MouseSensitivity;
            if (vertical && GameSettings.InvertY) gain = -gain;
            controller.Input.Gain = gain;
        }
    }
}
