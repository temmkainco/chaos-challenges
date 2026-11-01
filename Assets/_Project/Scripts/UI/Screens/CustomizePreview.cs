using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// The customizer's turning 3D contestant: a preview model on a hidden stage, filmed into this RawImage. It turns by
/// itself; drag it with the mouse or turn it with the right stick, and it resumes shortly after.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class CustomizePreview : MonoBehaviour
{
    private static readonly Vector3 StagePosition = new(0f, -2000f, 0f);
    private const float TURN_SPEED = 30f;

    private PlayerLook _prefab;
    private PlayerLook _model;
    private Camera _camera;
    private RenderTexture _texture;
    private RawImage _image;
    private bool _dragging;
    private float _resumeTurnAt;

    public PlayerLook Model => _model;

    public static CustomizePreview Create(RawImage image, PlayerLook prefab)
    {
        var preview = image.gameObject.AddComponent<CustomizePreview>();
        preview._prefab = prefab;
        preview._image = image;
        return preview;
    }

    private void OnEnable()
    {
        Build();
        if (_camera != null) _camera.enabled = true;
        LocalLook.Changed += Apply;
        Apply();
    }

    private void OnDisable()
    {
        LocalLook.Changed -= Apply;
        if (_camera != null) _camera.enabled = false;
    }

    private void Apply()
    {
        if (_model != null)
            _model.Apply(LocalLook.Body, LocalLook.Hands, LocalLook.Head, LocalLook.Hat, LocalLook.Face, LocalLook.Neck, LocalLook.Back);
    }

    private void Build()
    {
        if (_model != null || _prefab == null) return;
        _model = Instantiate(_prefab, StagePosition, Quaternion.Euler(0f, 160f, 0f));
        _texture = new RenderTexture(768, 1024, 24) { name = "CustomizePreview" };
        _image.texture = _texture;
        var cameraGo = new GameObject("CustomizeCamera");
        cameraGo.transform.position = StagePosition + new Vector3(0f, 1.25f, 4.2f);
        cameraGo.transform.LookAt(StagePosition + Vector3.up * 1.05f);
        _camera = cameraGo.AddComponent<Camera>();
        _camera.fieldOfView = 32f;
        _camera.nearClipPlane = 0.1f;
        _camera.farClipPlane = 15f;
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = UiTheme.Instance.Riser;
        _camera.targetTexture = _texture;
        var lightGo = new GameObject("CustomizeLight");
        lightGo.transform.SetParent(cameraGo.transform, false);
        lightGo.transform.localRotation = Quaternion.Euler(20f, -25f, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.6f;
    }

    private void Update()
    {
        if (_model == null) return;
        var mouse = Mouse.current;
        if (mouse != null)
        {
            var position = mouse.position.ReadValue();
            var canvas = _image.canvas;
            var eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (mouse.leftButton.wasPressedThisFrame && RectTransformUtility.RectangleContainsScreenPoint(_image.rectTransform, position, eventCamera))
                _dragging = true;
            if (!mouse.leftButton.isPressed) _dragging = false;
            if (_dragging)
            {
                _model.transform.Rotate(Vector3.up, -mouse.delta.ReadValue().x * 0.5f, Space.World);
                _resumeTurnAt = Time.unscaledTime + 2.5f;
            }
        }
        var pad = Gamepad.current;
        if (pad != null && Mathf.Abs(pad.rightStick.ReadValue().x) > 0.2f)
        {
            _model.transform.Rotate(Vector3.up, -pad.rightStick.ReadValue().x * 180f * Time.unscaledDeltaTime, Space.World);
            _resumeTurnAt = Time.unscaledTime + 2.5f;
        }
        if (!_dragging && Time.unscaledTime >= _resumeTurnAt)
            _model.transform.Rotate(Vector3.up, TURN_SPEED * Time.unscaledDeltaTime, Space.World);
    }

    private void OnDestroy()
    {
        if (_model != null) Destroy(_model.gameObject);
        if (_camera != null) Destroy(_camera.gameObject);
        if (_texture != null) _texture.Release();
    }
}
