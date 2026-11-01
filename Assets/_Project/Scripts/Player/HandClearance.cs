using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps the hands (gloves and cuffs) out of the big head, the eyes and the bean body once everything else has posed
/// the arms (clips made for slim mocap actors, procedural emotes, kart and console seats). Each hand is a few dozen
/// points sampled from its mesh in the rest pose and carried by their bones; while any point is inside the head ball,
/// an eye or the body ellipsoid, the arm reaches out by that much (two-bone IK: the elbow opens or closes for the
/// distance, the upper arm turns to aim, the bend stays in its plane). Shapes follow the head and chest bones, measured from the rest pose.
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
    [SerializeField, Tooltip("Gap kept between the hands and the head or body (m)")]
    private float _margin = 0.015f;
    [SerializeField, Tooltip("Every how many hand vertices one is sampled")]
    private int _sampleStep = 6;
    [SerializeField, Tooltip("Push passes per frame (a push out of the body can push into the head)")]
    private int _passes = 10;

    private const float MAX_ESCAPE = 0.6f;   // m: the furthest a hand is ever moved out in one pass

    private Transform _head, _chest;
    private Vector3 _headLocal, _bodyLocal;
    private Quaternion _bodyRotation;
    private readonly List<(Vector3 local, float radius)> _eyes = new();   // in head bone space
    private Arm[] _arms;

    private class Arm
    {
        public Transform Upper, Lower, Hand;
        public readonly List<(Transform bone, Vector3 local)> Points = new();
    }

    /// <summary>The head ball now (centre, radius) and the body ellipsoid (centre, rotation, half-sizes), in world space.</summary>
    public (Vector3 centre, float radius) Head => (_head.TransformPoint(_headLocal), _headRadius * transform.lossyScale.y);
    public (Vector3 centre, Quaternion rotation, Vector3 size) Body => (_chest.TransformPoint(_bodyLocal), _chest.rotation * _bodyRotation, _bodySize * transform.lossyScale.y);

    private void Awake()
    {
        var animator = GetComponent<Animator>();
        var renderer = GetComponentInChildren<SkinnedMeshRenderer>();
        if (animator == null || !animator.isHuman || renderer == null || renderer.sharedMesh == null) { enabled = false; return; }
        _head = animator.GetBoneTransform(HumanBodyBones.Head);
        _chest = animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine);
        _arms = new[]
        {
            new Arm { Upper = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm), Lower = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm),
                Hand = animator.GetBoneTransform(HumanBodyBones.LeftHand) },
            new Arm { Upper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm), Lower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm),
                Hand = animator.GetBoneTransform(HumanBodyBones.RightHand) },
        };
        if (_head == null || _chest == null || System.Array.Exists(_arms, a => a.Upper == null || a.Lower == null || a.Hand == null))
        {
            enabled = false;
            return;
        }
        // Awake runs before the animator first poses the rig: this is the rest pose the shapes and samples come from.
        _headLocal = _head.InverseTransformPoint(transform.TransformPoint(_headCentre));
        _bodyLocal = _chest.InverseTransformPoint(transform.TransformPoint(_bodyCentre));
        _bodyRotation = Quaternion.Inverse(_chest.rotation) * transform.rotation;
        Sample(renderer);
    }

    /// <summary>Hand points from the gloves and cuffs; eye balls from the head part's vertices outside the head ball.</summary>
    private void Sample(SkinnedMeshRenderer renderer)
    {
        var mesh = renderer.sharedMesh;
        var baked = new Mesh();
        renderer.BakeMesh(baked, false);
        var vertices = baked.vertices;
        Destroy(baked);
        var toWorld = renderer.transform.localToWorldMatrix;

        // Each sampled vertex rides on the nearest joint of its hand (the wrist for cuff and palm, the finger joints
        // for fingers). The mesh's bone weights are not readable here (Unity's newer weight format).
        var joints = new List<Transform>[2];
        for (int a = 0; a < 2; a++) joints[a] = new List<Transform>(_arms[a].Hand.GetComponentsInChildren<Transform>());
        int hands = (int)CosmeticsCatalog.Part.Hands, headPart = (int)CosmeticsCatalog.Part.Head;
        var seen = new HashSet<int>();
        int n = 0;
        foreach (int i in mesh.GetTriangles(hands))
        {
            if (!seen.Add(i) || n++ % _sampleStep != 0) continue;
            var v = toWorld.MultiplyPoint3x4(vertices[i]);
            int side = (v - _arms[0].Hand.position).sqrMagnitude < (v - _arms[1].Hand.position).sqrMagnitude ? 0 : 1;
            Transform nearest = _arms[side].Hand;
            foreach (var joint in joints[side])
                if ((v - joint.position).sqrMagnitude < (v - nearest.position).sqrMagnitude) nearest = joint;
            _arms[side].Points.Add((nearest, nearest.InverseTransformPoint(v)));
        }
        // The forearm tubes too (body-part vertices nearer the forearm than the elbow's other side).
        seen.Clear();
        n = 0;
        foreach (int i in mesh.GetTriangles((int)CosmeticsCatalog.Part.Body))
        {
            if (!seen.Add(i) || n++ % _sampleStep != 0) continue;
            var v = toWorld.MultiplyPoint3x4(vertices[i]);
            foreach (var arm in _arms)
            {
                var elbow = arm.Lower.position;
                var along = arm.Hand.position - elbow;
                float t = Vector3.Dot(v - elbow, along) / along.sqrMagnitude;
                if (t < 0.1f || t > 1f || (v - (elbow + along * t)).magnitude > 0.08f * transform.lossyScale.y) continue;
                arm.Points.Add((arm.Lower, arm.Lower.InverseTransformPoint(v)));
            }
        }

        // The eyes stick out of the head ball: one ball per side round its outside vertices.
        var headCentre = _head.TransformPoint(_headLocal);
        float radius = _headRadius * transform.lossyScale.y;
        var sides = new[] { new List<Vector3>(), new List<Vector3>() };
        seen.Clear();
        foreach (int i in mesh.GetTriangles(headPart))
        {
            if (!seen.Add(i)) continue;
            var v = toWorld.MultiplyPoint3x4(vertices[i]);
            if ((v - headCentre).magnitude > radius * 1.02f)
                sides[transform.InverseTransformPoint(v).x > 0f ? 0 : 1].Add(v);
        }
        foreach (var side in sides)
        {
            if (side.Count == 0) continue;
            var bounds = new Bounds(side[0], Vector3.zero);
            foreach (var v in side) bounds.Encapsulate(v);
            _eyes.Add((_head.InverseTransformPoint(bounds.center), bounds.extents.magnitude / transform.lossyScale.y));
        }
    }

    private void LateUpdate()
    {
        float scale = transform.lossyScale.y, margin = _margin * scale;
        var bodySize = _bodySize * scale + Vector3.one * margin;
        for (int pass = 0; pass < _passes; pass++)
        {
            var head = _head.TransformPoint(_headLocal);
            var bodyCentre = _chest.TransformPoint(_bodyLocal);
            var bodyRotation = _chest.rotation * _bodyRotation;
            bool moved = false;
            float headRadius = _headRadius * scale + margin;
            var forward = bodyRotation * Vector3.forward;
            bool Inside(Vector3 q)
            {
                if ((q - head).sqrMagnitude < headRadius * headRadius) return true;
                foreach (var (eye, r) in _eyes)
                {
                    float er = r * scale + margin;
                    if ((q - _head.TransformPoint(eye)).sqrMagnitude < er * er) return true;
                }
                var local = Quaternion.Inverse(bodyRotation) * (q - bodyCentre);
                return new Vector3(local.x / bodySize.x, local.y / bodySize.y, local.z / bodySize.z).sqrMagnitude < 1f;
            }
            foreach (var arm in _arms)
            {
                // Each point inside the head, an eye or the body escapes straight out from the body's axis (the
                // line from the body's centre to the head's): pushing out of one along its own surface sent hands
                // in the dip between head and body into the other. The point needing the longest escape decides.
                Vector3 push = Vector3.zero;
                float deepest = 0f;
                foreach (var (bone, local) in arm.Points)
                {
                    var p = bone.TransformPoint(local);
                    if (!Inside(p)) continue;
                    var axis = head - bodyCentre;
                    var onAxis = bodyCentre + axis * Mathf.Clamp(Vector3.Dot(p - bodyCentre, axis) / axis.sqrMagnitude, -0.5f, 1.5f);
                    var away = Vector3.ProjectOnPlane(p - onAxis, axis);
                    var direction = away.sqrMagnitude > 1e-4f ? away.normalized : forward;
                    // A hand on the front half (at the face, the chest, the belly) comes out in front: that is
                    // where those gestures (crying, laughing, hugging, dabbing) are meant to be.
                    if (Vector3.Dot(away, forward) > 0f)
                        direction = (forward + direction * 0.35f).normalized;
                    for (float t = 0.01f; t <= MAX_ESCAPE * scale; t += 0.01f * scale)
                        if (!Inside(p + direction * t))
                        {
                            Consider(direction * t, ref push, ref deepest);
                            break;
                        }
                }
                if (deepest <= 0f) continue;
                // Move the hand by that much.
                Reach(arm, arm.Hand.position + push);
                moved = true;
            }
            if (!moved) break;
        }
    }

    /// <summary>
    /// Two-bone IK: the elbow opens or closes so the wrist can be as far from the shoulder as <paramref name="target"/>,
    /// keeping the arm's bend plane, then the upper arm turns to put the wrist on it.
    /// </summary>
    private static void Reach(Arm arm, Vector3 target)
    {
        Vector3 shoulder = arm.Upper.position, elbow = arm.Lower.position, wrist = arm.Hand.position;
        float upper = (elbow - shoulder).magnitude, lower = (wrist - elbow).magnitude;
        float distance = Mathf.Clamp((target - shoulder).magnitude, Mathf.Abs(upper - lower) + 0.001f, upper + lower - 0.001f);
        var bendAxis = Vector3.Cross(shoulder - elbow, wrist - elbow);
        if (bendAxis.sqrMagnitude > 1e-8f)
        {
            float current = Vector3.Angle(shoulder - elbow, wrist - elbow);
            float wanted = Mathf.Acos(Mathf.Clamp((upper * upper + lower * lower - distance * distance) / (2f * upper * lower), -1f, 1f)) * Mathf.Rad2Deg;
            arm.Lower.rotation = Quaternion.AngleAxis(wanted - current, bendAxis.normalized) * arm.Lower.rotation;
        }
        var from = arm.Hand.position - shoulder;
        var to = target - shoulder;
        if (from.sqrMagnitude > 1e-6f && to.sqrMagnitude > 1e-6f)
            arm.Upper.rotation = Quaternion.FromToRotation(from, to) * arm.Upper.rotation;
    }

    private static void Consider(Vector3 push, ref Vector3 best, ref float deepest)
    {
        float depth = push.magnitude;
        if (depth > deepest) { deepest = depth; best = push; }
    }

    /// <summary>How far (and which way) <paramref name="point"/> has to move to leave the ball; zero if outside.</summary>
    private static Vector3 OutOfBall(Vector3 point, Vector3 centre, float radius)
    {
        var offset = point - centre;
        float distance = offset.magnitude;
        if (distance >= radius) return Vector3.zero;
        return (distance > 1e-5f ? offset / distance : Vector3.forward) * (radius - distance);
    }

    private static Vector3 OutOfBody(Vector3 point, Vector3 centre, Quaternion rotation, Vector3 size)
    {
        // In the body's frame scaled to a unit ball: inside if shorter than 1; out along the scaled direction.
        var local = Quaternion.Inverse(rotation) * (point - centre);
        var unit = new Vector3(local.x / size.x, local.y / size.y, local.z / size.z);
        float length = unit.magnitude;
        if (length >= 1f) return Vector3.zero;
        var surface = length > 1e-5f ? unit / length : Vector3.forward;
        return centre + rotation * Vector3.Scale(surface, size) - point;
    }

    private void OnDrawGizmosSelected()
    {
        if (_head == null) return;
        var (centre, radius) = Head;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(centre, radius);
        foreach (var (eye, r) in _eyes) Gizmos.DrawWireSphere(_head.TransformPoint(eye), r * transform.lossyScale.y);
        var (bodyCentre, rotation, size) = Body;
        Gizmos.matrix = Matrix4x4.TRS(bodyCentre, rotation, size);
        Gizmos.DrawWireSphere(Vector3.zero, 1f);
        Gizmos.matrix = Matrix4x4.identity;
        if (_arms == null) return;
        Gizmos.color = Color.cyan;
        foreach (var arm in _arms) foreach (var (bone, local) in arm.Points) Gizmos.DrawSphere(bone.TransformPoint(local), 0.006f);
    }
}
