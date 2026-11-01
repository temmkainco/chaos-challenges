using UnityEngine;

/// <summary>
/// Keeps the hands out of the big head and the bean body once everything else has posed the arms (clips made for
/// slim mocap actors, procedural emotes, kart and console seats): a palm or fingertip inside either is moved out to
/// its surface by turning the upper arm at the shoulder, so the elbow bend stays. The shapes follow the head and chest bones,
/// measured from the rest pose.
/// </summary>
[DefaultExecutionOrder(100)]   // after EmotePlayer, RiderVisual and ConsoleSitter pose the arms in LateUpdate
[RequireComponent(typeof(Animator))]
public class HandClearance : MonoBehaviour
{
    [SerializeField, Tooltip("Centre of the head ball in the character's rest pose (m, from its feet)")]
    private Vector3 _headCentre = new(0f, 1.524f, 0f);
    [SerializeField, Tooltip("Head ball radius (m)")]
    private float _headRadius = 0.275f;
    [SerializeField, Tooltip("Centre of the body in the rest pose (m)")]
    private Vector3 _bodyCentre = new(0f, 0.943f, 0f);
    [SerializeField, Tooltip("Body half-sizes (m): width, height, depth")]
    private Vector3 _bodySize = new(0.236f, 0.341f, 0.193f);
    [SerializeField, Tooltip("How far outside the surface the palm stays (m): the glove's own thickness")]
    private float _margin = 0.07f;
    [SerializeField, Tooltip("How far outside the surface the fingertips stay (m)")]
    private float _tipMargin = 0.03f;

    private Transform _head, _chest;
    private (Transform upper, Transform palm, Transform tip)[] _arms;
    private Vector3 _headLocal, _bodyLocal;
    private Quaternion _bodyRotation;

    private void Awake()
    {
        var animator = GetComponent<Animator>();
        if (animator == null || !animator.isHuman) { enabled = false; return; }
        _head = animator.GetBoneTransform(HumanBodyBones.Head);
        _chest = animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine);
        Transform Bone(HumanBodyBones bone, HumanBodyBones fallback) => animator.GetBoneTransform(bone) ?? animator.GetBoneTransform(fallback);
        _arms = new[]
        {
            (animator.GetBoneTransform(HumanBodyBones.LeftUpperArm), Bone(HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftHand),
                Bone(HumanBodyBones.LeftMiddleDistal, HumanBodyBones.LeftHand)),
            (animator.GetBoneTransform(HumanBodyBones.RightUpperArm), Bone(HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightHand),
                Bone(HumanBodyBones.RightMiddleDistal, HumanBodyBones.RightHand)),
        };
        if (_head == null || _chest == null || _arms[0].upper == null || _arms[1].upper == null) { enabled = false; return; }
        // Awake runs before the animator first poses the rig: this is the rest pose the shapes were measured in.
        _headLocal = _head.InverseTransformPoint(transform.TransformPoint(_headCentre));
        _bodyLocal = _chest.InverseTransformPoint(transform.TransformPoint(_bodyCentre));
        _bodyRotation = Quaternion.Inverse(_chest.rotation) * transform.rotation;
    }

    private void LateUpdate()
    {
        float scale = transform.lossyScale.y;
        var head = _head.TransformPoint(_headLocal);
        var bodyCentre = _chest.TransformPoint(_bodyLocal);
        var bodyRotation = _chest.rotation * _bodyRotation;
        foreach (var (upper, palm, tip) in _arms)
        {
            // Body first (the hand may be pushed towards the head), then the head; palm, then fingertips (the glove
            // reaches well past the palm).
            Clear(upper, palm, OutOfBody(palm.position, bodyCentre, bodyRotation, scale, _margin));
            Clear(upper, palm, OutOfBall(palm.position, head, (_headRadius + _margin) * scale));
            Clear(upper, tip, OutOfBody(tip.position, bodyCentre, bodyRotation, scale, _tipMargin));
            Clear(upper, tip, OutOfBall(tip.position, head, (_headRadius + _tipMargin) * scale));
        }
    }

    private static Vector3? OutOfBall(Vector3 point, Vector3 centre, float radius)
    {
        var offset = point - centre;
        if (offset.sqrMagnitude >= radius * radius) return null;
        return centre + (offset.sqrMagnitude > 1e-6f ? offset.normalized : Vector3.forward) * radius;
    }

    private Vector3? OutOfBody(Vector3 point, Vector3 centre, Quaternion rotation, float scale, float margin)
    {
        // In the body's frame scaled to a unit ball: inside if shorter than 1.
        var size = (_bodySize + Vector3.one * margin) * scale;
        var local = Quaternion.Inverse(rotation) * (point - centre);
        var unit = new Vector3(local.x / size.x, local.y / size.y, local.z / size.z);
        if (unit.sqrMagnitude >= 1f) return null;
        var surface = unit.sqrMagnitude > 1e-6f ? unit.normalized : Vector3.forward;
        return centre + rotation * Vector3.Scale(surface, size);
    }

    private static void Clear(Transform upper, Transform point, Vector3? target)
    {
        if (target == null) return;
        var from = point.position - upper.position;
        var to = target.Value - upper.position;
        if (from.sqrMagnitude < 1e-6f || to.sqrMagnitude < 1e-6f) return;
        upper.rotation = Quaternion.FromToRotation(from, to) * upper.rotation;
    }
}
