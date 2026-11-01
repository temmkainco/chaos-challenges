using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The kit's shapes, drawn once at runtime: a white rounded rectangle (9-sliced; any corner radius through
/// <see cref="UnityEngine.UI.Image.pixelsPerUnitMultiplier"/>), a circle, a soft radial glow and the icon set.
/// White so any token colour can tint them; anti-aliased edges.
/// </summary>
public static class UiShapes
{
    /// <summary>Corner radius baked into <see cref="RoundRect"/> (texture pixels = canvas units at multiplier 1).</summary>
    public const float BAKED_RADIUS = 32f;

    private static Sprite _roundRect, _circle, _glow;
    private static readonly Dictionary<string, Sprite> Icons = new();

    public static Sprite RoundRect => _roundRect != null ? _roundRect : _roundRect = MakeRoundRect();
    public static Sprite Circle => _circle != null ? _circle : _circle = MakeCircle();
    /// <summary>Radial white glow, opaque in the middle to clear at the edge (spotlights).</summary>
    public static Sprite Glow => _glow != null ? _glow : _glow = MakeGlow();

    /// <summary>The multiplier that shows <see cref="RoundRect"/> with this corner radius (canvas units).</summary>
    public static float RadiusMultiplier(float radius) => BAKED_RADIUS / Mathf.Max(1f, radius);

    private static Sprite MakeRoundRect()
    {
        const int SIZE = 96;
        float r = BAKED_RADIUS;
        var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "UiRoundRect" };
        var pixels = new Color32[SIZE * SIZE];
        for (int y = 0; y < SIZE; y++)
            for (int x = 0; x < SIZE; x++)
            {
                // Distance outside the rounded rectangle (negative inside), pixel centres.
                float px = x + 0.5f, py = y + 0.5f;
                float cx = Mathf.Clamp(px, r, SIZE - r), cy = Mathf.Clamp(py, r, SIZE - r);
                float d = new Vector2(px - cx, py - cy).magnitude - r;
                byte a = (byte)(Mathf.Clamp01(0.5f - d) * 255f);
                pixels[y * SIZE + x] = new Color32(255, 255, 255, a);
            }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(r, r, r, r));
    }

    private static Sprite MakeCircle()
    {
        const int SIZE = 128;
        var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "UiCircle" };
        var pixels = new Color32[SIZE * SIZE];
        float c = SIZE / 2f;
        for (int y = 0; y < SIZE; y++)
            for (int x = 0; x < SIZE; x++)
            {
                float d = new Vector2(x + 0.5f - c, y + 0.5f - c).magnitude - (c - 1f);
                pixels[y * SIZE + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255f));
            }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Sprite MakeGlow()
    {
        const int SIZE = 128;
        var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "UiGlow" };
        var pixels = new Color32[SIZE * SIZE];
        float c = SIZE / 2f;
        for (int y = 0; y < SIZE; y++)
            for (int x = 0; x < SIZE; x++)
            {
                float t = Mathf.Clamp01(new Vector2(x + 0.5f - c, y + 0.5f - c).magnitude / c);
                float a = 1f - t * t * (3f - 2f * t);
                pixels[y * SIZE + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f), 100f);
    }

    /// <summary>
    /// The icon set, drawn as thick anti-aliased strokes so they match the outlined look: tick, cross, play, arrows,
    /// lock, star, heart, gear, person, gift, clock, back. White, 128 px.
    /// </summary>
    public static Sprite Icon(string name)
    {
        if (Icons.TryGetValue(name, out var cached) && cached != null) return cached;
        const int SIZE = 128;
        var strokes = IconStrokes(name);
        var tex = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "UiIcon " + name };
        var pixels = new Color32[SIZE * SIZE];
        for (int y = 0; y < SIZE; y++)
            for (int x = 0; x < SIZE; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f) / SIZE;
                float d = float.MaxValue;
                foreach (var s in strokes) d = Mathf.Min(d, s(p));
                pixels[y * SIZE + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d * SIZE) * 255f));
            }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        var sprite = Sprite.Create(tex, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f), 100f);
        Icons[name] = sprite;
        return sprite;
    }

    private delegate float Shape(Vector2 p);

    // Signed distances in 0..1 icon space (y up). Strokes are 0.11 thick.
    private const float W = 0.055f;
    private static Shape Line(float ax, float ay, float bx, float by, float w = W) => p =>
    {
        var a = new Vector2(ax, ay); var b = new Vector2(bx, by);
        var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - (a + ab * t)).magnitude - w;
    };
    private static Shape Ring(float cx, float cy, float r, float w = W) => p => Mathf.Abs((p - new Vector2(cx, cy)).magnitude - r) - w;
    private static Shape Dot(float cx, float cy, float r) => p => (p - new Vector2(cx, cy)).magnitude - r;
    private static Shape Box(float x0, float y0, float x1, float y1) => p =>
    {
        var c = new Vector2((x0 + x1) / 2f, (y0 + y1) / 2f); var h = new Vector2((x1 - x0) / 2f, (y1 - y0) / 2f);
        var d = new Vector2(Mathf.Abs(p.x - c.x) - h.x, Mathf.Abs(p.y - c.y) - h.y);
        return Vector2.Max(d, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f);
    };
    private static Shape Triangle(Vector2 a, Vector2 b, Vector2 c) => p =>
    {
        // Inside if on the same side of all edges: negative distance to the nearest edge.
        float Edge(Vector2 e0, Vector2 e1) { var e = e1 - e0; var n = new Vector2(e.y, -e.x).normalized; return Vector2.Dot(p - e0, n); }
        // Either winding: inside is negative for one of the two orientations.
        float cw = Mathf.Max(Edge(a, b), Mathf.Max(Edge(b, c), Edge(c, a)));
        float ccw = Mathf.Max(-Edge(a, b), Mathf.Max(-Edge(b, c), -Edge(c, a)));
        return Mathf.Min(cw, ccw);
    };

    private static Shape[] IconStrokes(string name) => name switch
    {
        "tick" => new[] { Line(0.2f, 0.52f, 0.42f, 0.3f), Line(0.42f, 0.3f, 0.8f, 0.72f) },
        "cross" => new[] { Line(0.25f, 0.25f, 0.75f, 0.75f), Line(0.25f, 0.75f, 0.75f, 0.25f) },
        "play" => new[] { Triangle(new Vector2(0.32f, 0.2f), new Vector2(0.32f, 0.8f), new Vector2(0.82f, 0.5f)) },
        "left" => new[] { Line(0.62f, 0.2f, 0.32f, 0.5f), Line(0.32f, 0.5f, 0.62f, 0.8f) },
        "right" => new[] { Line(0.38f, 0.2f, 0.68f, 0.5f), Line(0.68f, 0.5f, 0.38f, 0.8f) },
        "up" => new[] { Line(0.2f, 0.38f, 0.5f, 0.68f), Line(0.5f, 0.68f, 0.8f, 0.38f) },
        "down" => new[] { Line(0.2f, 0.62f, 0.5f, 0.32f), Line(0.5f, 0.32f, 0.8f, 0.62f) },
        "lock" => new[] { Box(0.24f, 0.14f, 0.76f, 0.54f), Ring(0.5f, 0.6f, 0.17f) },
        "star" => new[] { Star() },
        "heart" => new[] { Dot(0.36f, 0.6f, 0.16f), Dot(0.64f, 0.6f, 0.16f), Triangle(new Vector2(0.21f, 0.56f), new Vector2(0.79f, 0.56f), new Vector2(0.5f, 0.18f)) },
        "gear" => Gear(),
        "person" => new[] { Ring(0.5f, 0.66f, 0.15f), Line(0.22f, 0.16f, 0.32f, 0.36f), Line(0.32f, 0.36f, 0.68f, 0.36f), Line(0.68f, 0.36f, 0.78f, 0.16f) },
        "gift" => new[] { Box(0.18f, 0.12f, 0.82f, 0.56f), Box(0.12f, 0.5f, 0.88f, 0.66f), Line(0.5f, 0.12f, 0.5f, 0.66f, 0.03f) },
        "clock" => new[] { Ring(0.5f, 0.5f, 0.34f), Line(0.5f, 0.5f, 0.5f, 0.72f), Line(0.5f, 0.5f, 0.66f, 0.42f) },
        "join" => new[] { Line(0.18f, 0.5f, 0.6f, 0.5f), Line(0.44f, 0.32f, 0.62f, 0.5f), Line(0.62f, 0.5f, 0.44f, 0.68f), Line(0.8f, 0.18f, 0.8f, 0.82f) },
        "back" => new[] { Line(0.24f, 0.5f, 0.76f, 0.5f), Line(0.24f, 0.5f, 0.44f, 0.7f), Line(0.24f, 0.5f, 0.44f, 0.3f) },
        _ => new[] { Dot(0.5f, 0.5f, 0.2f) },
    };

    private static Shape Star()
    {
        var pts = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float a = Mathf.PI / 2f + i * Mathf.PI / 5f, r = i % 2 == 0 ? 0.4f : 0.17f;
            pts[i] = new Vector2(0.5f + Mathf.Cos(a) * r, 0.47f + Mathf.Sin(a) * r);
        }
        return p =>
        {
            // Inside test by winding; distance to the outline for anti-aliasing.
            bool inside = false; float d = float.MaxValue;
            for (int i = 0, j = 9; i < 10; j = i++)
            {
                var a = pts[i]; var b = pts[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
                var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                d = Mathf.Min(d, (p - (a + ab * t)).magnitude);
            }
            return inside ? -d : d;
        };
    }

    private static Shape[] Gear()
    {
        var list = new List<Shape> { Ring(0.5f, 0.5f, 0.2f, 0.07f) };
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f;
            list.Add(Line(0.5f + Mathf.Cos(a) * 0.26f, 0.5f + Mathf.Sin(a) * 0.26f, 0.5f + Mathf.Cos(a) * 0.38f, 0.5f + Mathf.Sin(a) * 0.38f, 0.06f));
        }
        return list.ToArray();
    }
}
