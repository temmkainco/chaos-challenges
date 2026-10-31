using UnityEngine;

/// <summary>
/// A ring in the player's colour round each contestant's feet (<see cref="Player.PlayerColor"/>), so everyone can tell
/// who is who in the lobby and in every minigame, whatever their outfit. Unlit, so it reads the same in every place.
/// </summary>
public class ColorRing : MonoBehaviour
{
    private const float INNER = 0.48f, OUTER = 0.66f, LIFT = 0.04f;

    private static Mesh _mesh;
    private Player _player;
    private Renderer _renderer;
    private MaterialPropertyBlock _block;
    private Color _shown = Color.clear;

    public static void Attach(Player player)
    {
        if (player.GetComponentInChildren<ColorRing>(true) != null) return;
        var go = new GameObject("ColorRing", typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(player.transform, false);
        go.transform.localPosition = Vector3.up * LIFT;
        go.GetComponent<MeshFilter>().sharedMesh = _mesh != null ? _mesh : _mesh = Annulus(INNER, OUTER, 40);
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = Resources.Load<Material>("ArenaGlow");
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var ring = go.AddComponent<ColorRing>();
        ring._player = player;
        ring._renderer = renderer;
        ring._block = new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        if (_player == null || _player.Object == null || !_player.Object.IsValid) return;
        // Ghosts and contestants hidden for the ready check don't get one.
        bool show = !_player.IsSpectator;
        if (_renderer.enabled != show) _renderer.enabled = show;
        var color = _player.PlayerColor;
        if (color == _shown) return;
        _shown = color;
        _block.SetColor("_BaseColor", color);
        _renderer.SetPropertyBlock(_block);
    }

    private static Mesh Annulus(float inner, float outer, int segments)
    {
        var vertices = new Vector3[segments * 2];
        var triangles = new int[segments * 6];
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            vertices[i * 2] = direction * inner;
            vertices[i * 2 + 1] = direction * outer;
            int next = (i + 1) % segments;
            int t = i * 6;
            triangles[t] = i * 2; triangles[t + 1] = next * 2; triangles[t + 2] = i * 2 + 1;
            triangles[t + 3] = i * 2 + 1; triangles[t + 4] = next * 2; triangles[t + 5] = next * 2 + 1;
        }
        var mesh = new Mesh { name = "ColorRing", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
