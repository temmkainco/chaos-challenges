using TMPro;
using UnityEngine;

/// <summary>
/// The "knocked out" lounge: a railed platform floating beside the arena. Eliminated contestants are sent here with
/// their controls back, so they can run, jump and emote while watching instead of standing frozen among the players.
/// Built locally on every peer (the floor must exist for client prediction); see <see cref="EliminationMinigameController"/>.
/// </summary>
public class SpectatorLounge : MonoBehaviour
{
    private const float RADIUS = 5f;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

    public static SpectatorLounge Build(Transform parent, Vector3 position, Vector3 lookAt)
    {
        var root = new GameObject("SpectatorLounge");
        root.transform.SetParent(parent, true);
        root.transform.position = position;
        var face = lookAt - position;
        face.y = 0f;
        if (face.sqrMagnitude > 0.01f) root.transform.rotation = Quaternion.LookRotation(face);
        var lounge = root.AddComponent<SpectatorLounge>();
        lounge.BuildGeometry();
        Physics.SyncTransforms();
        return lounge;
    }

    /// <summary>A free spot on the lounge for the n-th knocked-out contestant.</summary>
    public Vector3 Spot(int index)
    {
        float angle = index * 2.4f;
        float radius = 1.2f + (index % 3) * 1.1f;
        return transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius + Vector3.up * 0.6f;
    }

    private void BuildGeometry()
    {
        var block = new MaterialPropertyBlock();
        void Tint(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColor, color);
            renderer.SetPropertyBlock(block);
        }

        // Floor: a cylinder with a mesh collider (a stretched capsule collider would be a dome).
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        floor.name = "LoungeFloor";
        Destroy(floor.GetComponent<Collider>());
        floor.transform.SetParent(transform, false);
        floor.transform.localPosition = new Vector3(0f, -0.25f, 0f);
        floor.transform.localScale = new Vector3(RADIUS * 2f, 0.25f, RADIUS * 2f);
        floor.AddComponent<MeshCollider>().sharedMesh = floor.GetComponent<MeshFilter>().sharedMesh;
        Tint(floor, new Color(0.72f, 0.61f, 0.95f));

        var rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(rim.GetComponent<Collider>());
        rim.transform.SetParent(transform, false);
        rim.transform.localPosition = new Vector3(0f, -0.3f, 0f);
        rim.transform.localScale = new Vector3(RADIUS * 2f + 0.4f, 0.24f, RADIUS * 2f + 0.4f);
        Tint(rim, new Color(1f, 0.79f, 0.24f));

        // Rail posts all round, so nobody wanders off by accident.
        const int posts = 20;
        for (int i = 0; i < posts; i++)
        {
            float a = i / (float)posts * Mathf.PI * 2f;
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.transform.SetParent(transform, false);
            post.transform.localPosition = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * RADIUS + Vector3.up * 0.55f;
            post.transform.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
            post.transform.localScale = new Vector3(0.25f, 1.1f, 1.6f);
            Tint(post, i % 2 == 0 ? new Color(1f, 0.49f, 0.71f) : new Color(1f, 1f, 1f));
        }

        // Sign facing the arena.
        var sign = new GameObject("LoungeSign");
        sign.transform.SetParent(transform, false);
        sign.transform.localPosition = new Vector3(0f, 3.2f, -RADIUS);
        sign.transform.localRotation = Quaternion.identity;
        var text = sign.AddComponent<TextMeshPro>();
        text.text = "KNOCKED-OUT LOUNGE";
        text.fontSize = 6f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.sizeDelta = new Vector2(12f, 2f);
        text.color = new Color(1f, 0.79f, 0.24f);
        text.outlineWidth = 0.25f;
        text.outlineColor = new Color32(46, 26, 71, 255);
        sign.AddComponent<Billboard>();
    }
}
