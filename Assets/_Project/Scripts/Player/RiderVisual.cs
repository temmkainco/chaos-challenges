using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Puts a contestant in the minigame's vehicle (<see cref="MinigameController.Vehicle"/>): a kart or a plane built
/// from primitives in their outfit colour, the character seated in it with hands on the wheel or stick, and (for
/// planes) the whole thing tilted with the networked pitch and bank. For the local player the orbit camera swings
/// behind the vehicle unless the mouse moved recently. Visual only; driving is in <see cref="PlayerMovement"/>.
/// </summary>
public class RiderVisual : MonoBehaviour
{
    private Player _player;
    private PlayerMovement _movement;
    private Transform _visuals, _vehicle;
    private VehicleKind _built;
    private Transform _hips, _spine, _lUpperLeg, _lLowerLeg, _rUpperLeg, _rLowerLeg, _lUpperArm, _lLowerArm, _rUpperArm, _rLowerArm, _lHand, _rHand, _lFoot, _rFoot;
    private CinemachineOrbitalFollow _orbit;
    private float _manualCameraUntil;
    private MaterialPropertyBlock _block;

    public static void Attach(Player player)
    {
        if (!player.TryGetComponent<RiderVisual>(out _))
            player.gameObject.AddComponent<RiderVisual>();
    }

    private void Awake()
    {
        _player = GetComponent<Player>();
        _movement = GetComponent<PlayerMovement>();
        var animator = GetComponentInChildren<Animator>(true);
        if (animator == null || !animator.isHuman) { enabled = false; return; }
        _visuals = animator.transform;
        Transform B(HumanBodyBones bone) => animator.GetBoneTransform(bone);
        _hips = B(HumanBodyBones.Hips); _spine = B(HumanBodyBones.Spine);
        _lUpperLeg = B(HumanBodyBones.LeftUpperLeg); _lLowerLeg = B(HumanBodyBones.LeftLowerLeg); _lFoot = B(HumanBodyBones.LeftFoot);
        _rUpperLeg = B(HumanBodyBones.RightUpperLeg); _rLowerLeg = B(HumanBodyBones.RightLowerLeg); _rFoot = B(HumanBodyBones.RightFoot);
        _lUpperArm = B(HumanBodyBones.LeftUpperArm); _lLowerArm = B(HumanBodyBones.LeftLowerArm); _lHand = B(HumanBodyBones.LeftHand);
        _rUpperArm = B(HumanBodyBones.RightUpperArm); _rLowerArm = B(HumanBodyBones.RightLowerArm); _rHand = B(HumanBodyBones.RightHand);
    }

    private VehicleKind Kind
    {
        get
        {
            var minigame = MinigameController.Current;
            if (_player == null || _player.Object == null || !_player.Object.IsValid || _player.IsSpectator) return VehicleKind.None;
            return minigame != null && minigame.Object != null && minigame.Object.IsValid ? minigame.Vehicle : VehicleKind.None;
        }
    }

    private void LateUpdate()
    {
        var kind = Kind;
        if (kind != _built) Rebuild(kind);
        if (kind == VehicleKind.None)
        {
            if (_visuals != null && _visuals.localRotation != Quaternion.identity) _visuals.localRotation = Quaternion.identity;
            return;
        }

        // Planes pitch and bank the whole rider; karts lean a touch into corners.
        float pitch = _movement != null ? _movement.Pitch : 0f, bank = _movement != null ? _movement.Bank : 0f;
        _visuals.localRotation = kind == VehicleKind.Plane ? Quaternion.Euler(-pitch, 0f, bank) : Quaternion.Euler(0f, 0f, bank);
        Pose(kind);
        if (_player.Object.HasInputAuthority) FollowCamera();
    }

    private bool Emoting =>
        _player.EmoteId != 0 && _player.Runner != null && (_player.Runner.Tick - _player.EmoteTick) * _player.Runner.DeltaTime < EmotePlayer.DURATION;

    /// <summary>Seated: hips down in the seat, legs forward, hands on the wheel (kart) or the stick (plane).</summary>
    private void Pose(VehicleKind kind)
    {
        if (_hips == null) return;
        Vector3 up = _visuals.up, fwd = _visuals.forward, right = _visuals.right;
        float seat = kind == VehicleKind.Kart ? 1.12f : 0.62f;   // on the toy car's roof; in the plane's body
        _hips.position = _visuals.TransformPoint(new Vector3(0f, seat, -0.25f));
        Tilt(_spine, right, -10f);
        Aim(_lUpperLeg, _lLowerLeg, (fwd + up * 0.15f - right * 0.12f).normalized);
        Aim(_rUpperLeg, _rLowerLeg, (fwd + up * 0.15f + right * 0.12f).normalized);
        Aim(_lLowerLeg, _lFoot, (fwd * 0.7f - up).normalized);
        Aim(_rLowerLeg, _rFoot, (fwd * 0.7f - up).normalized);
        if (Emoting) return;
        float wiggle = Mathf.Sin(Time.time * 9f) * 0.05f;
        Aim(_lUpperArm, _lLowerArm, (fwd - up * 0.45f - right * 0.2f).normalized);
        Aim(_rUpperArm, _rLowerArm, (fwd - up * 0.45f + right * 0.2f).normalized);
        Aim(_lLowerArm, _lHand, (fwd + up * (0.15f + wiggle) + right * 0.35f).normalized);
        Aim(_rLowerArm, _rHand, (fwd + up * (0.15f - wiggle) - right * 0.35f).normalized);
    }

    /// <summary>The orbit camera swings round behind the vehicle; moving the mouse takes over for a moment.</summary>
    private void FollowCamera()
    {
        if (_orbit == null)
        {
            if (_player.Camera == null) return;
            _orbit = _player.Camera.GetComponentInChildren<CinemachineOrbitalFollow>(true);
            if (_orbit == null) return;
        }
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouse.delta.ReadValue().sqrMagnitude > 9f) _manualCameraUntil = Time.time + 1.5f;
        if (Time.time < _manualCameraUntil || PauseMenu.IsOpen) return;
        float yaw = transform.eulerAngles.y;
        _orbit.HorizontalAxis.Value = Mathf.LerpAngle(_orbit.HorizontalAxis.Value, Mathf.DeltaAngle(0f, yaw), 1f - Mathf.Exp(-5f * Time.deltaTime));
    }

    private void Rebuild(VehicleKind kind)
    {
        _built = kind;
        if (_vehicle != null) Destroy(_vehicle.gameObject);
        _vehicle = null;
        if (kind == VehicleKind.None || _visuals == null) return;
        _vehicle = new GameObject(kind + "Vehicle").transform;
        _vehicle.SetParent(_visuals, false);
        var paint = _player.OutfitColor;
        var dark = new Color(0.14f, 0.13f, 0.18f);
        var trim = Color.Lerp(paint, Color.white, 0.45f);
        if (kind == VehicleKind.Kart)
        {
            // A toy car in the outfit colour; the contestant rides on top, hands on a steering wheel.
            Model("ToyCar", new Vector3(0f, 0.55f, 0.1f), Vector3.zero, new Vector3(1.6f, 1.1f, 2.7f), Color.Lerp(Color.white, paint, 0.7f));
            Model("SteeringWheel", new Vector3(0f, 1.45f, 0.42f), new Vector3(-60f, 0f, 0f), new Vector3(0.45f, 0.45f, 0.12f), null);
        }
        else
        {
            // A plane built from toy bricks: a long brick body, a plate for wings and tail, a spinning propeller.
            Model("BrickLong", new Vector3(0f, 0.5f, 0.1f), new Vector3(0f, 90f, 0f), new Vector3(2.9f, 0.85f, 0.9f), paint);
            Model("BrickPlate", new Vector3(0f, 0.62f, 0.35f), Vector3.zero, new Vector3(4.4f, 0.14f, 1f), trim);
            Model("BrickPlate", new Vector3(0f, 0.75f, -1.25f), Vector3.zero, new Vector3(1.7f, 0.12f, 0.55f), trim);
            Model("Brick2", new Vector3(0f, 1.05f, -1.3f), new Vector3(0f, 0f, 90f), new Vector3(0.7f, 0.12f, 0.6f), paint);
            var spinner = new GameObject("Propeller").transform;
            spinner.SetParent(_vehicle, false);
            spinner.localPosition = new Vector3(0f, 0.5f, 1.68f);
            spinner.localRotation = Quaternion.Euler(90f, 0f, 0f);   // spins round the plane's forward axis
            var blade = Part(PrimitiveType.Cube, Vector3.zero, Vector3.zero, new Vector3(1.4f, 0.04f, 0.14f), dark);
            blade.SetParent(spinner, false);
            spinner.gameObject.AddComponent<CosmeticSpin>();
        }
    }

    /// <summary>A prop-pack model fitted to <paramref name="size"/> at a spot on the vehicle (falls back to a box).</summary>
    private void Model(string key, Vector3 position, Vector3 euler, Vector3 size, Color? tint)
    {
        var model = ArenaModels.Spawn(key, null, _vehicle.TransformPoint(position), _vehicle.rotation * Quaternion.Euler(euler), size, false, tint);
        if (model == null) { Part(PrimitiveType.Cube, position, euler, size, tint ?? Color.white); return; }
        model.SetParent(_vehicle, true);
        foreach (var renderer in model.GetComponentsInChildren<Renderer>()) renderer.gameObject.layer = gameObject.layer;
    }

    private Transform Part(PrimitiveType shape, Vector3 position, Vector3 euler, Vector3 scale, Color color)
    {
        var go = GameObject.CreatePrimitive(shape);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(_vehicle, false);
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.layer = gameObject.layer;
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = ArenaProps.Material;
        _block ??= new MaterialPropertyBlock();
        _block.SetColor("_BaseColor", color);
        renderer.SetPropertyBlock(_block);
        return go.transform;
    }

    private static void Aim(Transform bone, Transform child, Vector3 direction)
    {
        if (bone == null || child == null) return;
        Vector3 current = child.position - bone.position;
        if (current.sqrMagnitude < 0.0001f) return;
        bone.rotation = Quaternion.FromToRotation(current, direction) * bone.rotation;
    }

    private static void Tilt(Transform bone, Vector3 axis, float degrees)
    {
        if (bone != null) bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
    }

    private void OnDestroy()
    {
        if (_vehicle != null) Destroy(_vehicle.gameObject);
    }
}
