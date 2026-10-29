using DG.Tweening;
using UnityEngine;

/// <summary>
/// Local visual and collider of one Tilefall tile. Driven every frame by the replicated health:
/// it darkens and shakes as it cracks, drops away at zero and pops back when the tile respawns.
/// </summary>
public class TileView : MonoBehaviour
{
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private const float SHAKE_START = 0.6f;

    private MeshRenderer _renderer;
    private BoxCollider _collider;
    private Transform _visual;
    private Color _color;
    private Color _crackColor;
    private MaterialPropertyBlock _block;
    private Vector3 _restLocalPosition;
    private bool _solid;
    private bool _collapsed;
    private float _shownHealth = -1f;

    public static TileView Create(Transform parent, Vector3 position, Mesh mesh, Material material, Color color,
        Color crackColor, bool solid, float popDelay)
    {
        var go = new GameObject(solid ? "SolidTile" : "Tile");
        go.transform.SetParent(parent, true);
        go.transform.SetPositionAndRotation(position, parent.rotation);

        var visual = new GameObject("Visual", typeof(MeshFilter), typeof(MeshRenderer));
        visual.transform.SetParent(go.transform, false);
        visual.GetComponent<MeshFilter>().sharedMesh = mesh;

        var view = go.AddComponent<TileView>();
        view._visual = visual.transform;
        view._renderer = visual.GetComponent<MeshRenderer>();
        view._renderer.sharedMaterial = material;
        view._color = color;
        view._crackColor = crackColor;
        view._solid = solid;
        view._block = new MaterialPropertyBlock();

        var bounds = mesh.bounds;
        view._collider = go.AddComponent<BoxCollider>();
        view._collider.center = bounds.center;
        view._collider.size = bounds.size;

        view.SetColor(color);
        visual.transform.localScale = Vector3.zero;
        visual.transform.DOScale(1f, 0.4f).SetDelay(popDelay).SetEase(Ease.OutBack).SetLink(go);
        return view;
    }

    public void Apply(float health)
    {
        if (_solid || Mathf.Approximately(health, _shownHealth))
        {
            Shake(health);
            return;
        }

        _shownHealth = health;
        if (health <= 0f && !_collapsed)
            Collapse();
        else if (health > 0f && _collapsed)
            Restore();

        if (!_collapsed)
            SetColor(Color.Lerp(_crackColor, _color, Mathf.Clamp01(health * 1.15f)));
        Shake(health);
    }

    private void Shake(float health)
    {
        if (_solid || _collapsed || health >= SHAKE_START)
            return;

        float strength = (SHAKE_START - health) / SHAKE_START * 0.08f;
        _visual.localPosition = new Vector3(
            (Mathf.PerlinNoise(Time.time * 25f, 0f) - 0.5f) * strength,
            0f,
            (Mathf.PerlinNoise(0f, Time.time * 25f) - 0.5f) * strength);
    }

    private void Collapse()
    {
        Sfx.Play(Sound.Crack, 0.6f, 0.15f);
        _collapsed = true;
        _collider.enabled = false;
        _visual.DOKill();
        _visual.localPosition = Vector3.zero;
        DOTween.Sequence()
            .Append(_visual.DOLocalMoveY(-12f, 0.9f).SetEase(Ease.InQuad))
            .Join(_visual.DOLocalRotate(new Vector3(Random.Range(-40f, 40f), 0f, Random.Range(-40f, 40f)), 0.9f))
            .Join(_visual.DOScale(0.4f, 0.9f).SetEase(Ease.InQuad))
            .SetLink(gameObject);
    }

    private void Restore()
    {
        _collapsed = false;
        _visual.DOKill();
        _visual.localPosition = Vector3.zero;
        _visual.localRotation = Quaternion.identity;
        _visual.localScale = Vector3.zero;
        _visual.DOScale(1f, 0.35f).SetEase(Ease.OutBack).SetLink(gameObject);
        _collider.enabled = true;
        SetColor(_color);
    }

    private void SetColor(Color color)
    {
        _renderer.GetPropertyBlock(_block);
        _block.SetColor(BaseColor, color);
        _renderer.SetPropertyBlock(_block);
    }
}
