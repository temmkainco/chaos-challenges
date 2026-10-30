using System;
using UnityEngine;

/// <summary>
/// A tiny software framebuffer for the Console Round's retro games: draw rectangles, sprites and 3x5 pixel text
/// into a <see cref="Width"/> x <see cref="Height"/> buffer (top-left origin), then <see cref="Upload"/> it to a
/// point-filtered texture with CRT scanlines (every game row becomes a bright and a darker texture row).
/// No allocations per frame.
/// </summary>
public sealed class PixelScreen
{
    public readonly int Width, Height;
    public readonly Texture2D Texture;

    /// <summary>Mirror Mode twist: everything is drawn flipped left to right.</summary>
    public bool MirrorX;

    private readonly Color32[] _pixels;
    private readonly Color32[] _display;

    public PixelScreen(int width, int height)
    {
        Width = width;
        Height = height;
        _pixels = new Color32[width * height];
        _display = new Color32[width * height * 2];
        Texture = new Texture2D(width, height * 2, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "ConsoleScreen",
        };
    }

    public void Clear(Color32 color) => Array.Fill(_pixels, color);

    public void Set(int x, int y, Color32 color)
    {
        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height) return;
        if (MirrorX) x = Width - 1 - x;
        _pixels[y * Width + x] = color;
    }

    public void Rect(int x, int y, int w, int h, Color32 color)
    {
        int x0 = Mathf.Max(0, x), y0 = Mathf.Max(0, y), x1 = Mathf.Min(Width, x + w), y1 = Mathf.Min(Height, y + h);
        for (int py = y0; py < y1; py++)
            for (int px = x0; px < x1; px++)
                _pixels[py * Width + (MirrorX ? Width - 1 - px : px)] = color;
    }

    public void Frame(int x, int y, int w, int h, Color32 color)
    {
        Rect(x, y, w, 1, color);
        Rect(x, y + h - 1, w, 1, color);
        Rect(x, y, 1, h, color);
        Rect(x + w - 1, y, 1, h, color);
    }

    /// <summary>Rows of palette indices ('1'..'9'); '.' is transparent. <paramref name="flip"/> mirrors the sprite.</summary>
    public void Sprite(int x, int y, string[] rows, Color32[] palette, bool flip = false)
    {
        for (int r = 0; r < rows.Length; r++)
        {
            var row = rows[r];
            for (int c = 0; c < row.Length; c++)
            {
                char ch = row[flip ? row.Length - 1 - c : c];
                if (ch == '.') continue;
                Set(x + c, y + r, palette[ch - '0']);
            }
        }
    }

    #region Text (3x5 font, 1 px spacing)

    // 15 bits per glyph, row-major from the top-left.
    private static readonly ushort[] Glyphs = BuildGlyphs();

    private static ushort[] BuildGlyphs()
    {
        var table = new ushort[128];
        void G(char c, string rows)
        {
            ushort v = 0;
            var bits = rows.Replace("|", "");
            for (int i = 0; i < 15; i++) if (bits[i] == '#') v |= (ushort)(1 << i);
            table[c] = v;
        }
        G('0', ".#.|#.#|#.#|#.#|.#."); G('1', ".#.|##.|.#.|.#.|###"); G('2', "##.|..#|.#.|#..|###"); G('3', "##.|..#|.#.|..#|##.");
        G('4', "#.#|#.#|###|..#|..#"); G('5', "###|#..|##.|..#|##."); G('6', ".##|#..|###|#.#|###"); G('7', "###|..#|.#.|.#.|.#.");
        G('8', "###|#.#|###|#.#|###"); G('9', "###|#.#|###|..#|##."); G('A', ".#.|#.#|###|#.#|#.#"); G('B', "##.|#.#|##.|#.#|##.");
        G('C', ".##|#..|#..|#..|.##"); G('D', "##.|#.#|#.#|#.#|##."); G('E', "###|#..|##.|#..|###"); G('F', "###|#..|##.|#..|#..");
        G('G', ".##|#..|#.#|#.#|.##"); G('H', "#.#|#.#|###|#.#|#.#"); G('I', "###|.#.|.#.|.#.|###"); G('J', "..#|..#|..#|#.#|.#.");
        G('K', "#.#|#.#|##.|#.#|#.#"); G('L', "#..|#..|#..|#..|###"); G('M', "#.#|###|###|#.#|#.#"); G('N', "##.|#.#|#.#|#.#|#.#");
        G('O', ".#.|#.#|#.#|#.#|.#."); G('P', "##.|#.#|##.|#..|#.."); G('Q', ".#.|#.#|#.#|##.|.##"); G('R', "##.|#.#|##.|#.#|#.#");
        G('S', ".##|#..|.#.|..#|##."); G('T', "###|.#.|.#.|.#.|.#."); G('U', "#.#|#.#|#.#|#.#|###"); G('V', "#.#|#.#|#.#|#.#|.#.");
        G('W', "#.#|#.#|###|###|#.#"); G('X', "#.#|#.#|.#.|#.#|#.#"); G('Y', "#.#|#.#|.#.|.#.|.#."); G('Z', "###|..#|.#.|#..|###");
        G('!', ".#.|.#.|.#.|...|.#."); G('?', "##.|..#|.#.|...|.#."); G(':', "...|.#.|...|.#.|..."); G('.', "...|...|...|...|.#.");
        G('-', "...|...|###|...|..."); G('+', "...|.#.|###|.#.|..."); G('/', "..#|..#|.#.|#..|#.."); G('\'',".#.|.#.|...|...|...");
        G('<', "..#|.#.|#..|.#.|..#"); G('>', "#..|.#.|..#|.#.|#.."); G('*', "#.#|.#.|#.#|...|...");
        return table;
    }

    public static int TextWidth(string text, int scale = 1) => text.Length == 0 ? 0 : (text.Length * 4 - 1) * scale;

    public void Text(int x, int y, string text, Color32 color, int scale = 1)
    {
        for (int i = 0; i < text.Length; i++)
        {
            Glyph(x, y, char.ToUpperInvariant(text[i]), color, scale);
            x += 4 * scale;
        }
    }

    public void TextCentered(int y, string text, Color32 color, int scale = 1) =>
        Text((Width - TextWidth(text, scale)) / 2, y, text, color, scale);

    /// <summary>Text with a 1 px drop shadow, for titles over busy backgrounds.</summary>
    public void TextShadowCentered(int y, string text, Color32 color, Color32 shadow, int scale = 1)
    {
        int x = (Width - TextWidth(text, scale)) / 2;
        Text(x + 1, y + 1, text, shadow, scale);
        Text(x, y, text, color, scale);
    }

    /// <summary>Draws a non-negative number without allocating a string; returns the x after the last digit.</summary>
    public int Number(int x, int y, int value, Color32 color, int scale = 1, int minDigits = 1)
    {
        value = Mathf.Max(0, value);
        int digits = 1;
        for (int v = value / 10; v > 0; v /= 10) digits++;
        digits = Mathf.Max(digits, minDigits);
        int right = x + (digits * 4 - 1) * scale;
        int cx = x + (digits - 1) * 4 * scale;
        for (int d = 0; d < digits; d++)
        {
            Glyph(cx, y, (char)('0' + value % 10), color, scale);
            value /= 10;
            cx -= 4 * scale;
        }
        return right + scale;
    }

    public static int NumberWidth(int value, int scale = 1, int minDigits = 1)
    {
        int digits = 1;
        for (int v = Mathf.Max(0, value) / 10; v > 0; v /= 10) digits++;
        return (Mathf.Max(digits, minDigits) * 4 - 1) * scale;
    }

    private void Glyph(int x, int y, char c, Color32 color, int scale)
    {
        if (c >= 128) return;
        ushort bits = Glyphs[c];
        if (bits == 0) return;
        for (int i = 0; i < 15; i++)
        {
            if ((bits & (1 << i)) == 0) continue;
            int gx = x + (i % 3) * scale, gy = y + (i / 3) * scale;
            if (scale == 1) Set(gx, gy, color);
            else Rect(gx, gy, scale, scale, color);
        }
    }

    #endregion

    #region Effects

    /// <summary>TV static over the picture (Blackout chaos, no-signal screens).</summary>
    public void Static(float amount, uint seed)
    {
        uint s = seed * 747796405u + 2891336453u;
        for (int i = 0; i < _pixels.Length; i++)
        {
            s ^= s << 13; s ^= s >> 17; s ^= s << 5;
            if ((s & 1023) / 1023f >= amount) continue;
            byte v = (byte)(s >> 24);
            _pixels[i] = new Color32(v, v, v, 255);
        }
    }

    public void Dim(float factor)
    {
        for (int i = 0; i < _pixels.Length; i++)
        {
            var p = _pixels[i];
            _pixels[i] = new Color32((byte)(p.r * factor), (byte)(p.g * factor), (byte)(p.b * factor), 255);
        }
    }

    #endregion

    /// <summary>Sends the frame to the GPU with scanlines (the texture is twice as tall as the picture).</summary>
    public void Upload()
    {
        for (int y = 0; y < Height; y++)
        {
            // Texture rows go bottom-up: picture row y fills texture rows (H-1-y)*2+1 (bright) and (H-1-y)*2 (dark).
            int bright = ((Height - 1 - y) * 2 + 1) * Width, dark = (Height - 1 - y) * 2 * Width, src = y * Width;
            for (int x = 0; x < Width; x++)
            {
                var p = _pixels[src + x];
                _display[bright + x] = p;
                _display[dark + x] = new Color32((byte)(p.r * 0.62f), (byte)(p.g * 0.62f), (byte)(p.b * 0.62f), 255);
            }
        }
        Texture.SetPixelData(_display, 0);
        Texture.Apply(false);
    }
}
