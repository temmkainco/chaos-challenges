using UnityEngine;

/// <summary>ASTRO BLAST: steer the ship (left/right), fire (A, hold for auto-fire) and blast the falling meteors. Big
/// ones take two hits. A meteor that reaches your ship ends the run; the rocks fall faster as you go.</summary>
public sealed class AstroGame : ConsoleGame
{
    private const float SHIP_Y = 84f, SHIP_SPEED = 72f, SHOT_SPEED = 150f, FIRE_EVERY = 0.22f;

    private struct Rock { public float X, Y, Speed; public byte Size, Hits; public bool Active; }
    private struct Shot { public float X, Y; public bool Active; }

    private readonly Rock[] _rocks = new Rock[10];
    private readonly Shot[] _shots = new Shot[8];
    private float _x, _cooldown, _nextRock;

    private static readonly string[] Ship = { "...1...", "..121..", ".11211.", "1111111", "13...31" };
    private static readonly Color32[] ShipPalette = { default, C(200, 220, 255), C(90, 200, 255), C(255, 140, 40) };

    public override string Title => "ASTRO BLAST";

    protected override void Reset()
    {
        _x = W / 2f;
        _cooldown = 0f;
        _nextRock = 0.6f;
        for (int i = 0; i < _rocks.Length; i++) _rocks[i].Active = false;
        for (int i = 0; i < _shots.Length; i++) _shots[i].Active = false;
    }

    protected override void Update(float dt, in ConsoleInput input)
    {
        _x = Mathf.Clamp(_x + input.X * SHIP_SPEED * dt, 5f, W - 5f);

        _cooldown -= dt;
        if ((input.APressed || input.A) && _cooldown <= 0f)
            for (int i = 0; i < _shots.Length; i++)
                if (!_shots[i].Active)
                {
                    _shots[i] = new Shot { X = _x, Y = SHIP_Y - 3f, Active = true };
                    _cooldown = FIRE_EVERY;
                    break;
                }
        for (int i = 0; i < _shots.Length; i++)
            if (_shots[i].Active && (_shots[i].Y -= SHOT_SPEED * dt) < 8f)
                _shots[i].Active = false;

        _nextRock -= dt;
        if (_nextRock <= 0f)
        {
            _nextRock = Mathf.Max(0.35f, 1.1f - T * 0.012f);
            for (int i = 0; i < _rocks.Length; i++)
                if (!_rocks[i].Active)
                {
                    byte size = (byte)(RandInt(0, 3) == 0 ? 8 : 5);
                    _rocks[i] = new Rock { X = Rand(6f, W - 6f), Y = 4f, Speed = Rand(16f, 26f) + T * 0.35f, Size = size, Active = true };
                    break;
                }
        }

        for (int r = 0; r < _rocks.Length; r++)
        {
            if (!_rocks[r].Active) continue;
            ref var rock = ref _rocks[r];
            rock.Y += rock.Speed * dt;
            float half = rock.Size * 0.5f;
            for (int s = 0; s < _shots.Length; s++)
            {
                if (!_shots[s].Active || !Overlap(rock.X - half, rock.Y - half, rock.Size, rock.Size, _shots[s].X - 0.5f, _shots[s].Y, 1f, 3f)) continue;
                _shots[s].Active = false;
                rock.Hits++;
                if (rock.Size < 8 || rock.Hits >= 2)
                {
                    rock.Active = false;
                    Score += rock.Size < 8 ? 1 : 2;
                }
                break;
            }
            if (rock.Active && Overlap(rock.X - half, rock.Y - half, rock.Size, rock.Size, _x - 3f, SHIP_Y - 2f, 7f, 5f))
                Over = true;
            if (rock.Y - half > H) rock.Active = false;
        }
    }

    public override void Draw(PixelScreen s)
    {
        s.Clear(C(10, 8, 28));
        // Two layers of stars scrolling down.
        for (int i = 0; i < 24; i++)
        {
            int sx = (i * 53) % W, speed = i % 3 == 0 ? 24 : 10;
            int sy = (int)Mathf.Repeat(i * 29 + T * speed, H - 8) + 8;
            s.Set(sx, sy, i % 3 == 0 ? C(220, 220, 255) : C(110, 110, 170));
        }
        foreach (var rock in _rocks)
        {
            if (!rock.Active) continue;
            int x = (int)(rock.X - rock.Size * 0.5f), y = (int)(rock.Y - rock.Size * 0.5f), n = rock.Size;
            var body = rock.Hits > 0 ? C(220, 120, 70) : C(150, 120, 100);
            s.Rect(x + 1, y, n - 2, n, body);
            s.Rect(x, y + 1, n, n - 2, body);
            s.Rect(x + 1, y + 1, 2, 1, C(200, 175, 150));
            s.Set(x + n - 2, y + n - 3, C(90, 70, 60));
        }
        foreach (var shot in _shots)
            if (shot.Active) s.Rect((int)shot.X, (int)shot.Y, 1, 3, C(255, 240, 120));
        s.Sprite((int)_x - 3, (int)SHIP_Y - 2, Ship, ShipPalette);
        if (((int)(T * 10f) & 1) == 0) s.Set((int)_x, (int)SHIP_Y + 3, C(255, 200, 60));
        if (T < 2f && ((int)(T * 3f) & 1) == 0)
            s.TextShadowCentered(48, "SPACE TO FIRE", C(255, 255, 255), C(40, 30, 90));
        Header(s, C(30, 20, 70), C(140, 220, 255));
    }
}

/// <summary>FROG HOP: hop up (and left, right, down) across the road and the river to the lily pads at the top. Cars
/// squash you; in the river you have to ride the logs. Every row further than you've been this crossing scores, the
/// far bank scores 5 and the traffic speeds up.</summary>
public sealed class FrogGame : ConsoleGame
{
    private const int CELL = 8, COLS = W / CELL, ROWS = 11, TOP = 8;

    private float _x;
    private int _row, _best, _level;
    private int _prevX, _prevY;
    private float _deadTimer;

    /// <summary>Per row: 0 grass, 1 road, 2 river. Row 0 is the bottom (start), the top row is the far bank.</summary>
    private static readonly byte[] Kind = { 0, 1, 1, 1, 1, 0, 2, 2, 2, 2, 0 };
    private readonly float[] _offset = new float[ROWS];
    private readonly float[] _speed = new float[ROWS];
    private readonly int[] _pattern = new int[ROWS];

    public override string Title => "FROG HOP";

    protected override void Reset()
    {
        _level = 0;
        _deadTimer = 0f;
        StartCrossing();
        for (int r = 0; r < ROWS; r++)
        {
            _offset[r] = Rand(0f, W);
            _pattern[r] = RandInt(0, 4);
            _speed[r] = Kind[r] == 0 ? 0f : Rand(14f, 26f) * ((r & 1) == 0 ? 1f : -1f);
        }
    }

    private void StartCrossing()
    {
        _x = W / 2f - CELL / 2f;
        _row = 0;
        _best = 0;
    }

    /// <summary>Cars on a road row, logs on a river row: segments of the repeating lane pattern.</summary>
    private bool Covered(int row, float x)
    {
        int length = Kind[row] == 1 ? 12 + (_pattern[row] % 2) * 6 : 22 + _pattern[row] * 4;
        int period = Kind[row] == 1 ? 44 + _pattern[row] * 6 : 46 + _pattern[row] * 3;
        float local = Mathf.Repeat(x - _offset[row], period);
        return local < length;
    }

    protected override void Update(float dt, in ConsoleInput input)
    {
        float pace = 1f + _level * 0.18f;
        for (int r = 0; r < ROWS; r++) _offset[r] += _speed[r] * pace * dt;

        // One hop per press (edges of the d-pad), not per frame held.
        int dx = input.X != _prevX ? input.X : 0, dy = input.Y != _prevY ? input.Y : 0;
        _prevX = input.X;
        _prevY = input.Y;
        if (input.UpPressed && dy == 0) dy = 1;
        if (dy > 0 && _row < ROWS - 1) _row++;
        else if (dy < 0 && _row > 0) _row--;
        else if (dx != 0) _x = Mathf.Clamp(_x + dx * CELL, 0f, W - CELL);

        if (_row > _best)
        {
            Score += _row - _best;
            _best = _row;
        }
        if (_row == ROWS - 1)
        {
            Score += 5;
            _level++;
            StartCrossing();
            return;
        }

        float centre = _x + CELL / 2f;
        if (Kind[_row] == 1 && Covered(_row, centre)) Over = true;   // squashed
        if (Kind[_row] == 2)
        {
            if (!Covered(_row, centre)) Over = true;                 // in the water
            else _x += _speed[_row] * pace * dt;                      // riding the log
            if (_x < -2f || _x > W - CELL + 2f) Over = true;          // carried off screen
        }
    }

    public override void Draw(PixelScreen s)
    {
        s.Clear(C(30, 30, 40));
        for (int r = 0; r < ROWS; r++)
        {
            int y = TOP + (ROWS - 1 - r) * CELL;
            var ground = Kind[r] switch { 1 => C(60, 60, 70), 2 => C(40, 90, 170), _ => r == ROWS - 1 ? C(60, 140, 70) : C(80, 160, 80) };
            s.Rect(0, y, W, CELL, ground);
            if (Kind[r] == 1)
                for (int x = (int)Mathf.Repeat(_offset[r] * 0.5f, 12f) - 12; x < W; x += 12) s.Rect(x, y + CELL / 2, 6, 1, C(200, 200, 120));
            if (r == ROWS - 1)
                for (int x = 6; x < W; x += 24) { s.Rect(x, y + 2, 6, 4, C(40, 110, 50)); s.Set(x + 2, y + 1, C(240, 120, 160)); }
            if (Kind[r] == 0) continue;
            for (int x = 0; x < W; x++)
            {
                if (!Covered(r, x)) continue;
                if (Kind[r] == 1)
                {
                    var car = (_pattern[r] % 3) switch { 0 => C(232, 70, 60), 1 => C(250, 200, 60), _ => C(90, 190, 240) };
                    s.Rect(x, y + 2, 1, CELL - 4, car);
                }
                else s.Rect(x, y + 2, 1, CELL - 4, C(140, 90, 50));
            }
        }
        int fy = TOP + (ROWS - 1 - _row) * CELL;
        s.Rect((int)_x + 1, fy + 2, CELL - 2, CELL - 3, C(110, 220, 90));
        s.Set((int)_x + 2, fy + 2, C(20, 30, 20));
        s.Set((int)_x + CELL - 3, fy + 2, C(20, 30, 20));
        if (T < 2f && ((int)(T * 3f) & 1) == 0)
            s.TextShadowCentered(48, "ARROWS TO HOP", C(255, 255, 255), C(30, 60, 30));
        Header(s, C(20, 60, 30), C(170, 240, 140));
    }
}

/// <summary>STACK TOWER: a block slides back and forth; press A (or Space) to drop it on the tower. What hangs over the
/// edge is sliced off, so the blocks get narrower; miss completely and the run ends. Each block scores, a perfect drop
/// scores double, and the slider speeds up as the tower grows.</summary>
public sealed class StackGame : ConsoleGame
{
    private const int BLOCK_H = 6, BASE_Y = 90, VISIBLE = 12, MAX_BLOCKS = 256;

    private readonly float[] _left = new float[MAX_BLOCKS];
    private readonly float[] _width = new float[MAX_BLOCKS];
    private int _count;
    private float _x, _dir, _flash;

    public override string Title => "STACK TOWER";

    protected override void Reset()
    {
        _count = 1;
        _left[0] = 34f;
        _width[0] = 60f;
        NextBlock();
        _flash = 0f;
    }

    private void NextBlock()
    {
        _dir = (_count & 1) == 0 ? 1f : -1f;
        _x = _dir > 0 ? -_width[_count - 1] : W;
    }

    private float SlideSpeed => 40f + _count * 2.2f;

    protected override void Update(float dt, in ConsoleInput input)
    {
        _flash = Mathf.Max(0f, _flash - dt);
        float width = _width[_count - 1];
        _x += _dir * SlideSpeed * dt;
        if (_x > W - width) { _x = W - width; _dir = -1f; }
        if (_x < 0f) { _x = 0f; _dir = 1f; }
        if (!input.APressed && !input.UpPressed) return;

        float below = _left[_count - 1];
        float left = Mathf.Max(_x, below), right = Mathf.Min(_x + width, below + width);
        if (right - left <= 0.5f) { Over = true; return; }
        bool perfect = Mathf.Abs(_x - below) < 1.5f;
        if (perfect) { left = below; right = below + width; _flash = 0.3f; }
        if (_count < MAX_BLOCKS)
        {
            _left[_count] = left;
            _width[_count] = right - left;
            _count++;
        }
        Score += perfect ? 2 : 1;
        NextBlock();
    }

    private static Color32 Shade(int i) => (i % 5) switch
    {
        0 => C(250, 110, 120), 1 => C(250, 180, 80), 2 => C(240, 230, 100), 3 => C(110, 210, 140), _ => C(110, 170, 250),
    };

    public override void Draw(PixelScreen s)
    {
        s.Clear(_flash > 0f ? C(70, 50, 110) : C(40, 30, 80));
        for (int i = 0; i < 16; i++) s.Set((i * 37) % W, 10 + (i * 23) % 70, C(90, 80, 150));
        // The tower scrolls down so the top few blocks stay on screen.
        int scroll = Mathf.Max(0, _count - VISIBLE) * BLOCK_H;
        for (int i = Mathf.Max(0, _count - VISIBLE - 1); i < _count; i++)
        {
            int y = BASE_Y - (i + 1) * BLOCK_H + scroll;
            s.Rect((int)_left[i], y, Mathf.Max(1, (int)_width[i]), BLOCK_H - 1, Shade(i));
            s.Rect((int)_left[i], y, Mathf.Max(1, (int)_width[i]), 1, C(255, 255, 255));
        }
        int top = BASE_Y - (_count + 1) * BLOCK_H + scroll;
        s.Rect((int)_x, top, Mathf.Max(1, (int)_width[_count - 1]), BLOCK_H - 1, Shade(_count));
        s.Rect(0, BASE_Y + scroll, W, H, C(25, 20, 50));
        if (_count == 1 && ((int)(T * 3f) & 1) == 0)
            s.TextShadowCentered(30, "SPACE TO DROP", C(255, 255, 255), C(60, 30, 90));
        Header(s, C(25, 18, 60), C(250, 200, 120));
    }
}
