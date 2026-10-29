using System.Collections.Generic;
using UnityEngine;

/// <summary>Small flat-shaded meshes built in code (tiles, puffs), cached by the callers.</summary>
public static class ProceduralMeshes
{
    /// <summary>Square tile: box with chamfered top edges. Origin at the centre of the top face.</summary>
    public static Mesh ChamferedTile(float width, float depth, float height, float chamfer)
    {
        float hw = width / 2f, hd = depth / 2f;
        float iw = hw - chamfer, id = hd - chamfer;
        float yTop = 0f, yEdge = -chamfer, yBottom = -height;

        // Top inset ring, top outer ring (lowered by the chamfer), bottom ring.
        Vector3[] top = { new(-iw, yTop, -id), new(-iw, yTop, id), new(iw, yTop, id), new(iw, yTop, -id) };
        Vector3[] edge = { new(-hw, yEdge, -hd), new(-hw, yEdge, hd), new(hw, yEdge, hd), new(hw, yEdge, -hd) };
        Vector3[] bottom = { new(-hw, yBottom, -hd), new(-hw, yBottom, hd), new(hw, yBottom, hd), new(hw, yBottom, -hd) };

        var verts = new List<Vector3>();
        var tris = new List<int>();
        void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }

        Quad(top[0], top[1], top[2], top[3]);
        Quad(bottom[3], bottom[2], bottom[1], bottom[0]);
        for (int k = 0; k < 4; k++)
        {
            int n = (k + 1) % 4;
            Quad(edge[k], edge[n], top[n], top[k]);
            Quad(bottom[k], bottom[n], edge[n], edge[k]);
        }

        return FlatShaded(verts.ToArray(), tris.ToArray(), "ChamferedTile");
    }

    /// <summary>
    /// Splits shared vertices so every triangle gets its own flat normal (low-poly look). For convex shapes only:
    /// each triangle is wound to face away from the shape's centre, so the input winding does not matter.
    /// </summary>
    public static Mesh FlatShaded(Vector3[] vertices, int[] triangles, string name)
    {
        var centre = Vector3.zero;
        foreach (var v in vertices) centre += v;
        centre /= vertices.Length;

        var verts = new Vector3[triangles.Length];
        var tris = new int[triangles.Length];
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - centre) < 0f)
                (b, c) = (c, b);

            verts[i] = a; verts[i + 1] = b; verts[i + 2] = c;
            tris[i] = i; tris[i + 1] = i + 1; tris[i + 2] = i + 2;
        }

        var mesh = new Mesh { name = name, vertices = verts, triangles = tris };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
