using DG.Tweening;
using UnityEngine;

/// <summary>
/// Cartoon explosion made of flat-shaded puffs (fire core, then smoke), matching the low-poly art.
/// Purely visual and local; every peer plays it from replicated state.
/// </summary>
public static class LowPolyExplosion
{
    private static readonly Color[] FireColors = { new(1f, 0.85f, 0.2f), new(1f, 0.55f, 0.1f), new(0.95f, 0.25f, 0.1f) };
    private static readonly Color SmokeColor = new(0.35f, 0.33f, 0.36f);
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    private static Mesh _puffMesh;
    private static Material _material;

    /// <summary><paramref name="material"/> must be a lit material with a _BaseColor property.</summary>
    public static void Play(Vector3 position, float size, Material material)
    {
        _material = material;
        _puffMesh ??= CreatePuffMesh();

        for (int i = 0; i < 9; i++)
            SpawnPuff(position, FireColors[i % FireColors.Length], size * Random.Range(0.5f, 0.9f),
                Random.insideUnitSphere * size * 0.6f + Vector3.up * size * 0.3f, 0.25f, i * 0.02f);

        for (int i = 0; i < 7; i++)
            SpawnPuff(position, SmokeColor, size * Random.Range(0.45f, 0.75f),
                Random.insideUnitSphere * size * 0.5f + Vector3.up * size * Random.Range(0.8f, 1.6f), 0.7f, 0.12f + i * 0.04f);
    }

    // Puffs are pooled: an explosion reuses 16 parked puffs instead of creating GameObjects.
    private static readonly System.Collections.Generic.Stack<MeshRenderer> Pool = new();
    private static MaterialPropertyBlock _block;

    private static void SpawnPuff(Vector3 origin, Color color, float scale, Vector3 offset, float lifetime, float delay)
    {
        MeshRenderer renderer = null;
        while (Pool.Count > 0 && renderer == null)
            renderer = Pool.Pop();   // entries die with their scene
        if (renderer == null)
        {
            var go = new GameObject("Puff", typeof(MeshFilter), typeof(MeshRenderer));
            go.GetComponent<MeshFilter>().sharedMesh = _puffMesh;
            renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        var puff = renderer.gameObject;
        puff.SetActive(true);
        puff.transform.SetPositionAndRotation(origin, Random.rotation);
        puff.transform.localScale = Vector3.zero;
        renderer.sharedMaterial = _material;
        _block ??= new MaterialPropertyBlock();
        _block.SetColor(BaseColor, color);
        renderer.SetPropertyBlock(_block);

        DOTween.Sequence()
            .SetDelay(delay)
            .Append(puff.transform.DOScale(scale, 0.18f).SetEase(Ease.OutBack))
            .Join(puff.transform.DOMove(origin + offset, 0.35f + lifetime).SetEase(Ease.OutCubic))
            .AppendInterval(lifetime * 0.4f)
            .Append(puff.transform.DOScale(0f, lifetime).SetEase(Ease.InQuad))
            .OnComplete(() =>
            {
                puff.SetActive(false);
                Pool.Push(renderer);
            })
            .SetLink(puff);
    }

    /// <summary>Low-poly icosphere with flat shading.</summary>
    private static Mesh CreatePuffMesh()
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var v = new[]
        {
            new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
            new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
            new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
        };
        int[] f =
        {
            0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
            3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
        };
        return ProceduralMeshes.FlatShaded(System.Array.ConvertAll(v, p => p.normalized * 0.5f), f, "Puff");
    }
}
