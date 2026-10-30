using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The Console Round's first-person view (GDD: "seeing hands with gamepad"): a camera at the contestant's eyes
/// looking at their TV, with low-poly hands holding a gamepad whose thumbs press the d-pad and buttons as you play.
/// Only exists on the local peer.
/// </summary>
public class ConsoleViewmodel : MonoBehaviour
{
    private const int PRIORITY = 15;

    private Transform _pad, _leftThumb, _rightThumb, _dpad, _buttonA;
    private Vector3 _leftThumbRest, _rightThumbRest, _padRest;
    private int _x, _y;
    private bool _a;
    private float _kick;

    public static ConsoleViewmodel Create(Material material, Color hands, Color sleeves)
    {
        var go = new GameObject("ConsoleFirstPerson");
        var camera = go.AddComponent<CinemachineCamera>();
        camera.Priority = PRIORITY;
        var lens = camera.Lens;
        lens.FieldOfView = 56f;
        lens.NearClipPlane = 0.03f;
        camera.Lens = lens;
        var view = go.AddComponent<ConsoleViewmodel>();
        view.Build(material, hands, sleeves);
        return view;
    }

    private Transform Part(PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Color color, Material material, Vector3 euler = default)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(block);
        return go.transform;
    }

    private void Build(Material material, Color hands, Color sleeves)
    {
        // Pad space: face up (+y), width along x, the grips toward the player (-z).
        _pad = new GameObject("Gamepad").transform;
        _pad.SetParent(transform, false);
        _padRest = new Vector3(0f, -0.17f, 0.36f);
        _pad.localPosition = _padRest;
        _pad.localRotation = Quaternion.Euler(-58f, 0f, 0f);

        var shell = new Color(0.2f, 0.18f, 0.28f);
        Part(PrimitiveType.Cube, _pad, Vector3.zero, new Vector3(0.2f, 0.035f, 0.085f), shell, material);
        Part(PrimitiveType.Sphere, _pad, new Vector3(-0.085f, -0.004f, -0.035f), new Vector3(0.075f, 0.05f, 0.12f), shell, material, new Vector3(0f, -18f, 0f));
        Part(PrimitiveType.Sphere, _pad, new Vector3(0.085f, -0.004f, -0.035f), new Vector3(0.075f, 0.05f, 0.12f), shell, material, new Vector3(0f, 18f, 0f));
        var dark = new Color(0.08f, 0.08f, 0.1f);
        _dpad = new GameObject("DPad").transform;
        _dpad.SetParent(_pad, false);
        _dpad.localPosition = new Vector3(-0.055f, 0.019f, 0.004f);
        Part(PrimitiveType.Cube, _dpad, Vector3.zero, new Vector3(0.036f, 0.008f, 0.012f), dark, material);
        Part(PrimitiveType.Cube, _dpad, Vector3.zero, new Vector3(0.012f, 0.008f, 0.036f), dark, material);
        _buttonA = Part(PrimitiveType.Cylinder, _pad, new Vector3(0.058f, 0.02f, -0.006f), new Vector3(0.018f, 0.005f, 0.018f), new Color(1f, 0.37f, 0.36f), material);
        Part(PrimitiveType.Cylinder, _pad, new Vector3(0.078f, 0.02f, 0.013f), new Vector3(0.018f, 0.005f, 0.018f), new Color(0.36f, 0.75f, 0.92f), material);
        Part(PrimitiveType.Cylinder, _pad, new Vector3(0.04f, 0.02f, 0.016f), new Vector3(0.016f, 0.005f, 0.016f), new Color(1f, 0.79f, 0.24f), material);
        Part(PrimitiveType.Cube, _pad, new Vector3(-0.012f, 0.019f, 0.012f), new Vector3(0.014f, 0.006f, 0.006f), new Color(0.5f, 0.5f, 0.55f), material);
        Part(PrimitiveType.Cube, _pad, new Vector3(0.012f, 0.019f, 0.012f), new Vector3(0.014f, 0.006f, 0.006f), new Color(0.5f, 0.5f, 0.55f), material);

        // Hands wrap the grips; thumbs rest on the d-pad and the A button.
        Part(PrimitiveType.Sphere, _pad, new Vector3(-0.098f, -0.01f, -0.05f), new Vector3(0.085f, 0.075f, 0.12f), hands, material, new Vector3(0f, -18f, 0f));
        Part(PrimitiveType.Sphere, _pad, new Vector3(0.098f, -0.01f, -0.05f), new Vector3(0.085f, 0.075f, 0.12f), hands, material, new Vector3(0f, 18f, 0f));
        _leftThumbRest = new Vector3(-0.058f, 0.034f, -0.018f);
        _rightThumbRest = new Vector3(0.056f, 0.034f, -0.02f);
        _leftThumb = Part(PrimitiveType.Capsule, _pad, _leftThumbRest, new Vector3(0.024f, 0.032f, 0.024f), hands, material, new Vector3(90f, 0f, -25f));
        _rightThumb = Part(PrimitiveType.Capsule, _pad, _rightThumbRest, new Vector3(0.024f, 0.032f, 0.024f), hands, material, new Vector3(90f, 0f, 25f));
        // Sleeves run back out of view.
        Part(PrimitiveType.Cylinder, _pad, new Vector3(-0.13f, -0.03f, -0.19f), new Vector3(0.075f, 0.12f, 0.075f), sleeves, material, new Vector3(80f, 20f, 0f));
        Part(PrimitiveType.Cylinder, _pad, new Vector3(0.13f, -0.03f, -0.19f), new Vector3(0.075f, 0.12f, 0.075f), sleeves, material, new Vector3(80f, -20f, 0f));
    }

    /// <summary>Camera at <paramref name="eye"/> looking at the screen.</summary>
    public void Place(Vector3 eye, Vector3 screen)
    {
        transform.position = eye;
        transform.rotation = Quaternion.LookRotation(screen - eye);
    }

    public void SetPressed(int x, int y, bool a)
    {
        if (a && !_a) _kick = 1f;
        _x = x;
        _y = y;
        _a = a;
    }

    private void LateUpdate()
    {
        if (_pad == null) return;
        float t = Time.time;
        _kick = Mathf.MoveTowards(_kick, 0f, Time.deltaTime * 8f);
        // Breathing sway, a little lean with the d-pad and a jolt on the button.
        _pad.localPosition = _padRest + new Vector3(Mathf.Sin(t * 1.3f) * 0.004f + _x * 0.008f, Mathf.Sin(t * 2.1f) * 0.003f - _kick * 0.006f, 0f);
        _pad.localRotation = Quaternion.Euler(-58f + _kick * 4f, 0f, -_x * 6f);
        _leftThumb.localPosition = _leftThumbRest + new Vector3(_x * 0.009f, _x != 0 || _y != 0 ? -0.008f : 0f, _y * 0.009f);
        _rightThumb.localPosition = _rightThumbRest + new Vector3(0f, _a ? -0.009f : 0f, 0f);
        _dpad.localRotation = Quaternion.Euler(_y * 10f, 0f, -_x * 10f);
        _buttonA.localScale = new Vector3(0.018f, _a ? 0.002f : 0.005f, 0.018f);
    }

    /// <summary>The local contestant's own body would block the view: keep only its shadow.</summary>
    public static void HideBody(Player player, bool hide)
    {
        var mode = hide ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        foreach (var renderer in player.GetComponentsInChildren<Renderer>())
            if (renderer.shadowCastingMode != mode && renderer is SkinnedMeshRenderer or MeshRenderer)
                renderer.shadowCastingMode = mode;
    }
}
