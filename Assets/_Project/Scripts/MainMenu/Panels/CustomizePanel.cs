using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Main menu character customizer: body, hands and head colour plus hat, face, neck and back items, with a turning
/// 3D preview (drag it to turn it yourself). The arrows step through the preset colours; clicking a part's colour opens
/// the colour picker (<see cref="ColorPickerPopup"/>) for any colour.
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
    private ColorPickerPopup _picker;
    private Camera _camera;
    private RenderTexture _texture;

    private void Awake()
    {
        Hook(_body, CosmeticsCatalog.Part.Body);
        Hook(_hands, CosmeticsCatalog.Part.Hands);
        Hook(_head, CosmeticsCatalog.Part.Head);
        _hat.Previous.onClick.AddListener(() => LocalLook.StepItem(CosmeticsCatalog.Slot.Hat, -1));
        _hat.Next.onClick.AddListener(() => LocalLook.StepItem(CosmeticsCatalog.Slot.Hat, 1));
        BuildExtraRows();
        _picker = ColorPickerPopup.Create((RectTransform)_hat.Value.transform.parent, PickerArea());
        OpensPicker(_body, CosmeticsCatalog.Part.Body);
        OpensPicker(_hands, CosmeticsCatalog.Part.Hands);
        OpensPicker(_head, CosmeticsCatalog.Part.Head);
        _randomButton.onClick.AddListener(LocalLook.Randomize);
        _doneButton.onClick.AddListener(Close);
    }

    private readonly Row[] _extra = new Row[3];
    private static readonly CosmeticsCatalog.Slot[] ExtraSlots = { CosmeticsCatalog.Slot.Face, CosmeticsCatalog.Slot.Neck, CosmeticsCatalog.Slot.Back };

    /// <summary>
    /// Face, neck and back rows, cloned from the hat row at runtime; the original four rows move up a little to make
    /// room (the scene layout has fixed positions: 190 / 70 / -50 / -170).
    /// </summary>
    private void BuildExtraRows()
    {
        var parent = (RectTransform)_hat.Value.transform.parent;
        float[] oldY = { 190f, 70f, -50f, -170f };
        float[] newY = { 250f, 170f, 90f, 10f };
        RectTransform hatLabel = null;
        foreach (RectTransform child in parent)
        {
            for (int k = 0; k < oldY.Length; k++)
                if (Mathf.Abs(child.anchoredPosition.y - oldY[k]) < 1f)
                    child.anchoredPosition = new Vector2(child.anchoredPosition.x, newY[k]);
            if (child.TryGetComponent<TMP_Text>(out var text) && text.text == "Hat") hatLabel = child;
            if (child.GetComponent<Button>() != null && Mathf.Abs(child.anchoredPosition.y + 300f) < 1f)
                child.anchoredPosition = new Vector2(child.anchoredPosition.x, -318f);
        }
        for (int k = 0; k < ExtraSlots.Length; k++)
        {
            float y = -70f - 80f * k;
            var slot = ExtraSlots[k];
            if (hatLabel != null)
            {
                var label = Clone(hatLabel, y);
                label.GetComponent<TMP_Text>().text = slot.ToString();
            }
            var previous = Clone((RectTransform)_hat.Previous.transform, y).GetComponent<Button>();
            var next = Clone((RectTransform)_hat.Next.transform, y).GetComponent<Button>();
            var value = Clone((RectTransform)_hat.Value.transform, y).GetComponent<TMP_Text>();
            previous.onClick.AddListener(() => LocalLook.StepItem(slot, -1));
            next.onClick.AddListener(() => LocalLook.StepItem(slot, 1));
            _extra[k] = new Row { Previous = previous, Next = next, Value = value };
        }

        RectTransform Clone(RectTransform source, float y)
        {
            var copy = (RectTransform)Instantiate(source.gameObject, parent).transform;
            copy.anchoredPosition = new Vector2(source.anchoredPosition.x, y);
            return copy;
        }
    }

    /// <summary>The rows' area, labels to arrows (where the picker opens, leaving the preview in view).</summary>
    private Rect PickerArea()
    {
        var parent = (RectTransform)_hat.Value.transform.parent;
        var corners = new Vector3[4];
        float xMin = float.MaxValue, xMax = float.MinValue, yMin = float.MaxValue, yMax = float.MinValue;
        foreach (RectTransform child in parent)
        {
            // The rows only: not the Random / Done buttons below them, the preview or the title.
            if (child.anchoredPosition.y < -300f || child.anchoredPosition.y > 300f || child == _preview.rectTransform) continue;
            if (child.rect.width > 400f || child.rect.height > 150f || child.GetComponent<ColorPickerPopup>() != null) continue;   // backgrounds
            child.GetWorldCorners(corners);
            foreach (var c in corners)
            {
                var local = parent.InverseTransformPoint(c);
                xMin = Mathf.Min(xMin, local.x); xMax = Mathf.Max(xMax, local.x);
                yMin = Mathf.Min(yMin, local.y); yMax = Mathf.Max(yMax, local.y);
            }
        }
        return Rect.MinMaxRect(xMin - 10f, yMin - 10f, xMax + 10f, yMax + 10f);
    }

    /// <summary>Clicking a colour row's swatch or name opens the picker for that part.</summary>
    private void OpensPicker(Row row, CosmeticsCatalog.Part part)
    {
        foreach (var graphic in new Graphic[] { row.Swatch, row.Value })
        {
            if (graphic == null) continue;
            graphic.raycastTarget = true;
            var button = graphic.gameObject.GetComponent<Button>() ?? graphic.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => _picker.Open(part));
        }
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
        if (_picker != null) _picker.Close();
        if (_camera != null) _camera.enabled = false;
    }

    private bool _dragging;
    private float _resumeTurnAt;

    /// <summary>Drag the preview with the left mouse button to turn it; it starts turning by itself again shortly after.</summary>
    private void Update()
    {
        if (_model == null) return;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null)
        {
            var position = mouse.position.ReadValue();
            var canvas = _preview.canvas;
            var eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            if (mouse.leftButton.wasPressedThisFrame && RectTransformUtility.RectangleContainsScreenPoint(_preview.rectTransform, position, eventCamera))
                _dragging = true;
            if (!mouse.leftButton.isPressed) _dragging = false;
            if (_dragging)
            {
                _model.transform.Rotate(Vector3.up, -mouse.delta.ReadValue().x * 0.5f, Space.World);
                _resumeTurnAt = Time.unscaledTime + 2.5f;
            }
        }
        if (!_dragging && Time.unscaledTime >= _resumeTurnAt)
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

        Show(_body, LocalLook.Body);
        Show(_hands, LocalLook.Hands);
        Show(_head, LocalLook.Head);
        _hat.Value.text = catalog.GetItem(CosmeticsCatalog.Slot.Hat, LocalLook.Hat).Name;
        for (int k = 0; k < ExtraSlots.Length; k++)
            if (_extra[k].Value != null)
                _extra[k].Value.text = catalog.GetItem(ExtraSlots[k], LocalLook.GetItem(ExtraSlots[k])).Name;

        if (_model != null)
            _model.Apply(LocalLook.Body, LocalLook.Hands, LocalLook.Head, LocalLook.Hat, LocalLook.Face, LocalLook.Neck, LocalLook.Back);
    }

    private static void Show(Row row, Color color)
    {
        row.Value.text = LocalLook.NameOf(color);
        if (row.Swatch != null)
            row.Swatch.color = color;
    }

    private void OnDestroy()
    {
        if (_model != null) Destroy(_model.gameObject);
        if (_camera != null) Destroy(_camera.gameObject);
        if (_texture != null) _texture.Release();
    }
}
