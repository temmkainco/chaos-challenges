using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Leans a graphic like a broadcast banner (lower thirds, round tags): shears its vertices sideways around the rect's
/// centre by the theme's banner slant. Text inside stays upright (put this on the backgrounds only).
/// </summary>
[RequireComponent(typeof(Graphic))]
public class UiSkew : BaseMeshEffect
{
    [Tooltip("Degrees; negative = the theme's banner slant")] public float Angle = -1f;

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;
        float angle = Angle >= 0f ? Angle : UiTheme.Instance.BannerSlant;
        float shear = Mathf.Tan(angle * Mathf.Deg2Rad);
        var rect = ((RectTransform)transform).rect;
        float centreY = rect.center.y;
        var v = new UIVertex();
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref v, i);
            v.position.x += (v.position.y - centreY) * shear;
            vh.SetUIVertex(v, i);
        }
    }
}
