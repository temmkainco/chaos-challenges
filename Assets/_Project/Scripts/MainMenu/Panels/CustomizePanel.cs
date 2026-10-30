using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main menu character customizer: body, hands and head colour plus a hat, with a turning 3D preview.
/// Choices are saved right away (<see cref="LocalLook"/>) and used in every match.
/// </summary>
public class CustomizePanel : PanelBase
{
    [Serializable]
    private struct Row
    {
        public Button Previous;
        public Button Next;
        public Image Swatch;
        public TMP_Text Value;
    }

    [SerializeField] private Row _body;
    [SerializeField] private Row _hands;
    [SerializeField] private Row _head;
    [SerializeField] private Row _hat;
    [SerializeField] private Button _randomButton;
    [SerializeField] private Button _doneButton;
    [SerializeField] private RawImage _preview;
    [SerializeField] private PlayerLook _previewPrefab;
    [SerializeField] private float _turnSpeed = 30f;

    private static readonly Vector3 StagePosition = new(0f, -2000f, 0f);

    private PlayerLook _model;
    private Camera _camera;
    private RenderTexture _texture;

    private void Awake()
    {
        Hook(_body, CosmeticsCatalog.Part.Body);
        Hook(_hands, CosmeticsCatalog.Part.Hands);
        Hook(_head, CosmeticsCatalog.Part.Head);
        _hat.Previous.onClick.AddListener(() => LocalLook.StepHat(-1));
        _hat.Next.onClick.AddListener(() => LocalLook.StepHat(1));
        _randomButton.onClick.AddListener(LocalLook.Randomize);
        _doneButton.onClick.AddListener(Close);
    }

    private static void Hook(Row row, CosmeticsCatalog.Part part)
    {
        row.Previous.onClick.AddListener(() => LocalLook.StepColor(part, -1));
        row.Next.onClick.AddListener(() => LocalLook.StepColor(part, 1));
    }

    private void OnEnable()
    {
        BuildStage();
        _camera.enabled = true;
        LocalLook.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        LocalLook.Changed -= Refresh;
        if (_camera != null) _camera.enabled = false;
    }

    private void Update()
    {
        if (_model != null)
            _model.transform.Rotate(Vector3.up, _turnSpeed * Time.unscaledDeltaTime, Space.World);
    }

    private void BuildStage()
    {
        if (_model != null)
            return;

        _model = Instantiate(_previewPrefab, StagePosition, Quaternion.Euler(0f, 160f, 0f));
        _texture = new RenderTexture(768, 1024, 24) { name = "CustomizePreview" };
        _preview.texture = _texture;

        var cameraGo = new GameObject("CustomizeCamera");
        cameraGo.transform.position = StagePosition + new Vector3(0f, 1.25f, 4.2f);
        cameraGo.transform.LookAt(StagePosition + Vector3.up * 1.05f);
        _camera = cameraGo.AddComponent<Camera>();
        _camera.fieldOfView = 32f;
        _camera.nearClipPlane = 0.1f;
        _camera.farClipPlane = 15f;
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = new Color(0.91f, 0.87f, 0.98f, 1f);
        _camera.targetTexture = _texture;

        var lightGo = new GameObject("CustomizeLight");
        lightGo.transform.SetParent(cameraGo.transform, false);
        lightGo.transform.localRotation = Quaternion.Euler(20f, -25f, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 0.6f;
    }

    private void Refresh()
    {
        var catalog = CosmeticsCatalog.Instance;
        if (catalog == null)
            return;

        Show(_body, catalog.GetColor(LocalLook.Body));
        Show(_hands, catalog.GetColor(LocalLook.Hands));
        Show(_head, catalog.GetColor(LocalLook.Head));
        _hat.Value.text = catalog.GetHat(LocalLook.Hat).Name;

        if (_model != null)
            _model.Apply(LocalLook.Body, LocalLook.Hands, LocalLook.Head, LocalLook.Hat);
    }

    private static void Show(Row row, CosmeticsCatalog.Swatch swatch)
    {
        row.Value.text = swatch.Name;
        if (row.Swatch != null)
            row.Swatch.color = swatch.Color;
    }

    private void OnDestroy()
    {
        if (_model != null) Destroy(_model.gameObject);
        if (_camera != null) Destroy(_camera.gameObject);
        if (_texture != null) _texture.Release();
    }
}
