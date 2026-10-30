using UnityEngine;

/// <summary>
/// Console Round pose: sinks the contestant into their bean bag, knees up, gamepad held in both hands. Applied on
/// top of the idle animation every frame (bones aimed in world space, like <see cref="EmotePlayer"/>); emotes
/// still take over the arms.
/// </summary>
public class ConsoleSitter : MonoBehaviour
{
    private const float SEAT_HEIGHT = 0.42f;

    private Player _player;
    private Transform _hips, _spine, _head, _lUpperLeg, _lLowerLeg, _rUpperLeg, _rLowerLeg, _lUpperArm, _lLowerArm, _rUpperArm, _rLowerArm, _lHand, _rHand;
    private Transform _pad;

    private void Awake()
    {
        _player = GetComponent<Player>();
        var animator = GetComponentInChildren<Animator>(true);
        if (animator == null || !animator.isHuman) { enabled = false; return; }
        Transform B(HumanBodyBones bone) => animator.GetBoneTransform(bone);
        _hips = B(HumanBodyBones.Hips); _spine = B(HumanBodyBones.Spine); _head = B(HumanBodyBones.Head);
        _lUpperLeg = B(HumanBodyBones.LeftUpperLeg); _lLowerLeg = B(HumanBodyBones.LeftLowerLeg);
        _rUpperLeg = B(HumanBodyBones.RightUpperLeg); _rLowerLeg = B(HumanBodyBones.RightLowerLeg);
        _lUpperArm = B(HumanBodyBones.LeftUpperArm); _lLowerArm = B(HumanBodyBones.LeftLowerArm);
        _rUpperArm = B(HumanBodyBones.RightUpperArm); _rLowerArm = B(HumanBodyBones.RightLowerArm);
        _lHand = B(HumanBodyBones.LeftHand); _rHand = B(HumanBodyBones.RightHand);

        _pad = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
        Destroy(_pad.GetComponent<Collider>());
        _pad.name = "HeldGamepad";
        _pad.localScale = new Vector3(0.24f, 0.05f, 0.11f);
        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", new Color(0.2f, 0.18f, 0.28f));
        _pad.GetComponent<Renderer>().SetPropertyBlock(block);
    }

    private bool Seated =>
        _player != null && _player.Object != null && _player.Object.IsValid && _player.MovementLocked
        && MinigameController.Current is ConsoleRoundMinigameController;

    private bool Emoting =>
        _player.EmoteId != 0 && _player.Runner != null && (_player.Runner.Tick - _player.EmoteTick) * _player.Runner.DeltaTime < EmotePlayer.DURATION;

    private void LateUpdate()
    {
        bool seated = Seated;
        if (_pad != null) _pad.gameObject.SetActive(seated && !Emoting);
        if (!seated || _hips == null) return;

        var body = transform;
        Vector3 up = Vector3.up, fwd = body.forward, right = body.right;
        float t = Time.time;

        // Sink into the bean bag, leaning back a little.
        var hips = _hips.position;
        hips.y = body.position.y + SEAT_HEIGHT;
        _hips.position = hips - fwd * 0.1f;
        Tilt(_spine, right, -12f + Mathf.Sin(t * 1.7f) * 2f);

        // Knees up and apart, feet on the floor.
        Aim(_lUpperLeg, _lLowerLeg, (fwd + up * 0.25f - right * 0.18f).normalized);
        Aim(_rUpperLeg, _rLowerLeg, (fwd + up * 0.25f + right * 0.18f).normalized);
        Aim(_lLowerLeg, null, (-up + fwd * 0.35f).normalized);
        Aim(_rLowerLeg, null, (-up + fwd * 0.35f).normalized);

        if (Emoting) return;
        // Elbows in, forearms forward: hands meet over the lap holding the pad (with a little button-mashing jiggle).
        float mash = Mathf.Sin(t * 13f + _player.Object.InputAuthority.PlayerId) * 0.08f;
        Aim(_lUpperArm, _lLowerArm, (-up + fwd * 0.55f - right * 0.15f).normalized);
        Aim(_rUpperArm, _rLowerArm, (-up + fwd * 0.55f + right * 0.15f).normalized);
        Aim(_lLowerArm, null, (fwd + right * 0.55f + up * (0.25f + mash)).normalized);
        Aim(_rLowerArm, null, (fwd - right * 0.55f + up * (0.25f - mash)).normalized);
        Tilt(_head, right, 8f);

        if (_lHand != null && _rHand != null && _pad != null)
        {
            _pad.position = (_lHand.position + _rHand.position) * 0.5f + up * 0.03f;
            _pad.rotation = Quaternion.LookRotation(fwd, up) * Quaternion.Euler(-35f, 0f, 0f);
        }
    }

    private static void Aim(Transform bone, Transform child, Vector3 direction)
    {
        if (bone == null) return;
        Vector3 current = child != null ? child.position - bone.position : bone.childCount > 0 ? bone.GetChild(0).position - bone.position : bone.up;
        if (current.sqrMagnitude < 0.0001f) return;
        bone.rotation = Quaternion.FromToRotation(current, direction) * bone.rotation;
    }

    private static void Tilt(Transform bone, Vector3 axis, float degrees)
    {
        if (bone != null) bone.rotation = Quaternion.AngleAxis(degrees, axis) * bone.rotation;
    }

    private void OnDestroy()
    {
        if (_pad != null) Destroy(_pad.gameObject);
    }
}
