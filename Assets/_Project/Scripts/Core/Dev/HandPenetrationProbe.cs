#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// Editor tests only: skins the character every frame (after <see cref="HandClearance"/>) and records how deep any
/// vertex of the hands (gloves and cuffs) goes into the head ball or the body ellipsoid.
/// </summary>
[DefaultExecutionOrder(300)]
public class HandPenetrationProbe : MonoBehaviour
{
    public static float MaxHead, MaxBody;
    public static int Frames;
    public static void Reset() { MaxHead = MaxBody = 0f; Frames = 0; }

    private SkinnedMeshRenderer _renderer;
    private HandClearance _clearance;
    private Mesh _baked;
    private int[] _handVertices;

    private void Awake()
    {
        _renderer = GetComponentInChildren<SkinnedMeshRenderer>();
        _clearance = GetComponent<HandClearance>();
        _baked = new Mesh();
        var mesh = _renderer.sharedMesh;
        var set = new System.Collections.Generic.HashSet<int>(mesh.GetTriangles(1));
        // Plus the forearms: body-part vertices far out to the side in the bind pose (T-pose, arms along X).
        var bind = mesh.vertices;
        foreach (int i in mesh.GetTriangles(0)) if (Mathf.Abs(bind[i].x) > 0.42f) set.Add(i);
        _handVertices = new int[set.Count];
        set.CopyTo(_handVertices);
    }

    private void LateUpdate()
    {
        _renderer.BakeMesh(_baked, false);   // renderer space without its scale: localToWorld applies it
        var vertices = _baked.vertices;
        var toWorld = _renderer.transform.localToWorldMatrix;
        var (headCentre, headRadius) = _clearance.Head;
        var (bodyCentre, bodyRotation, bodySize) = _clearance.Body;
        var inverse = Quaternion.Inverse(bodyRotation);
        foreach (int i in _handVertices)
        {
            var v = toWorld.MultiplyPoint3x4(vertices[i]);
            MaxHead = Mathf.Max(MaxHead, headRadius - (v - headCentre).magnitude);
            var local = inverse * (v - bodyCentre);
            float s = new Vector3(local.x / bodySize.x, local.y / bodySize.y, local.z / bodySize.z).magnitude;
            if (s < 1f) MaxBody = Mathf.Max(MaxBody, (1f - s) * Mathf.Min(bodySize.x, bodySize.z));
        }
        Frames++;
    }
}
#endif
