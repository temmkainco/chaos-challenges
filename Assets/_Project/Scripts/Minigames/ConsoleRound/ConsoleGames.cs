using UnityEngine;

/// <summary>One frame of gamepad / keyboard input for a console game. <see cref="Y"/> is +1 for up.</summary>
public struct ConsoleInput
{
    public int X, Y;
    public bool A;
    public bool APressed, UpPressed;
}

/// <summary>Chaos applied to a console game (low gravity, super jump).</summary>
public struct ConsoleTuning
{
    public float Gravity, Jump;

    public static ConsoleTuning Normal => new() { Gravity = 1f, Jump = 1f };
}

/// <summary>
/// A cartridge of the Console Round: a small retro game simulated locally on the player's own TV at a fixed
/// 60 Hz step, drawn into a 128x96 <see cref="PixelScreen"/>. Every contestant gets the same course: the seed is
/// shared, so the n-th run of every player is identical. A run ends on <see cref="Over"/>; the station restarts it.
/// </summary>
public abstract class ConsoleGame
{
    public const int W = 128, H = 96;
    public const int COUNT = 5;

    public int Score { get; protected set; }
    public bool Over { get; protected set; }
    /// <summary>Best score this round, shown in the header.</summary>
    public int Best;

    protected float T;
    protected ConsoleTuning Tune;
    private System.Random _rng;

    public static ConsoleGame Create(int cartridge) => cartridge switch
    {
        0 => new SnakeGame(),
        1 => new FlapGame(),
        2 => new RunnerGame(),
        3 => new BrickGame(),
        _ => new HopperGame(),
    };

    public abstract string Title { get; }

    /// <summary>Whether the cartridge has gravity and jumps (Flap Dash, Pixel Runner, Sky Hopper), for chaos.</summary>
    public static bool HasGravity(int cartridge) => cartridge is 1 or 2 or 4;

    public void Begin(int seed)
    {
        _rng = new System.Random(seed);
        Score = 0;
        Over = false;
        T = 0f;
        Tune = ConsoleTuning.Normal;
        Reset();
    }

    public void Step(float dt, in ConsoleInput input, in ConsoleTuning tune)
    {
        if (Over) return;
        Tune = tune;
        T += dt;
        Update(dt, input);
    }

    protected abstract void Reset();
    protected abstract void Update(float dt, in ConsoleInput input);
    public abstract void Draw(PixelScreen s);

    protected float Rand(float min, float max) => min + (float)_rng.NextDouble() * (max - min);
    protected int RandInt(int min, int maxExclusive) => _rng.Next(min, maxExclusive);

    protected static bool Overlap(float ax, float ay, float aw, float ah, float bx, float by, float bw, float bh) =>
        ax < bx + bw && bx < ax + aw && ay < by + bh && by < ay + ah;

    /// <summary>Score and best on a 7 px bar along the top.</summary>
    protected void Header(PixelScreen s, Color32 bar, Color32 ink)
    {
        s.Rect(0, 0, W, 7, bar);
        s.Text(2, 1, "SCORE", ink);
        s.Number(23, 1, Score, ink);
        int bestWidth = PixelScreen.NumberWidth(Best);
        s.Number(W - 2 - bestWidth, 1, Best, ink);
        s.Text(W - 2 - bestWidth - 11, 1, "HI", ink);
    }

    protected static Color32 C(byte r, byte g, byte b) => new(r, g, b, 255);
}

/// <summary>SNAKE PIT: eat the apples, grow longer, don't hit the walls or yourself. Faster as you grow.</summary>
public sealed class SnakeGame : ConsoleGame
{
    private const int CELL = 4, GW = 32, GH = 22, TOP = 8;

    private readonly Vector2Int[] _body = new Vector2Int[GW * GH];
    private readonly bool[] _occupied = new bool[GW * GH];
    private int _length, _grow;
    private Vector2Int _dir, _queued, _food;
    private float _timer;

    public override string Title => "SNAKE PIT";

    protected override void Reset()
    {
        System.Array.Clear(_occupied, 0, _occupied.Length);
        _length = 3;
        _grow = 0;
        for (int i = 0; i < _length; i++)
        {
            _body[i] = new Vector2Int(8 - i, GH / 2);
            _occupied[Index(_body[i])] = true;
        }
        _dir = _queued = Vector2Int.right;
        _timer = 0f;
        PlaceFood();
    }

    private static int Index(Vector2Int c) => c.y * GW + c.x;

    private void PlaceFood()
    {
        for (int attempt = 0; attempt < 400; attempt++)
        {
            var c = new Vector2Int(RandInt(1, GW - 1), RandInt(1, GH - 1));
            if (!_occupied[Index(c)]) { _food = c; return; }
        }
    }

    protected override void Update(float dt, in ConsoleInput input)
    {
        if (input.X != 0 && _dir.x == 0) _queued = new Vector2Int(input.X, 0);
        else if (input.Y != 0 && _dir.y == 0) _queued = new Vector2Int(0, -input.Y);

        float step = Mathf.Max(0.06f, 0.13f - _length * 0.0022f);
        _timer += dt;
        while (_timer >= step && !Over)
        {
            _timer -= step;
            Move();
        }
    }

    private void Move()
    {
        _dir = _queued;
        var next = _body[0] + _dir;
        var tail = _body[_length - 1];
        bool tailMoves = _grow == 0;
        if (next.x < 0 || next.y < 0 || next.x >= GW || next.y >= GH
            || (_occupied[Index(next)] && !(tailMoves && next == tail)))
        {
            Over = true;
            return;
        }

        if (tailMoves) _occupied[Index(tail)] = false;
        else { _grow--; _length++; }
        for (int i = _length - 1; i > 0; i--) _body[i] = _body[i - 1];
        _body[0] = next;
        _occupied[Index(next)] = true;

        if (next == _food)
        {
            Score++;
            _grow += 2;
            PlaceFood();
        }
    }

    public override void Draw(PixelScreen s)
    {
        s.Clear(C(24, 40, 26));
        // Checkerboard field.
        for (int y = 0; y < GH; y++)
            for (int x = (y & 1); x < GW; x += 2)
                s.Rect(x * CELL, TOP + y * CELL, CELL, CELL, C(30, 50, 32));
        bool blink = ((int)(T * 6f) & 1) == 0;
        s.Rect(_food.x * CELL, TOP + _food.y * CELL, CELL, CELL, C(232, 60, 60));
        s.Set(_food.x * CELL + 1, TOP + _food.y * CELL - 1, blink ? C(110, 200, 90) : C(80, 160, 70));
        for (int i = _length - 1; i >= 0; i--)
        {
            var c = _body[i];
            var color = i == 0 ? C(200, 240, 120) : (i & 1) == 0 ? C(140, 210, 80) : C(120, 190, 70);
            s.Rect(c.x * CELL, TOP + c.y * CELL, CELL, CELL, color);
        }
        // Eyes look where the snake goes.
        var head = _body[0];
        int ex = head.x * CELL + (_dir.x > 0 ? 2 : _dir.x < 0 ? 1 : 1), ey = TOP + head.y * CELL + (_dir.y > 0 ? 2 : 1);
        s.Set(ex, ey, C(20, 30, 20));
        s.Set(ex + (_dir.y != 0 ? 2 : 0), ey + (_dir.x != 0 ? 1 : 0), C(20, 30, 20));
        Header(s, C(14, 24, 16), C(200, 240, 120));
    }
}

/// <summary>FLAP DASH: tap to flap through the gaps between the pipes. Faster the longer you last.</summary>
public sealed class FlapGame : ConsoleGame
{
    private const float BIRD_X = 30f, GROUND = 86f, GAP = 34f, PIPE_W = 12f, SPACING = 58f;

    private readonly float[] _pipeX = new float[4];
    private readonly float[] _gapY = new float[4];
    private readonly bool[] _passed = new bool[4];
    private float _y, _vy;
    private bool _started;

    private static readonly string[] Bird = { ".1111..", "122131.", "1111144", ".1111.." };
    private static readonly string[] BirdFlap = { ".1111..", "111131.", "1221144", ".1111.." };
    private static readonly Color32[] BirdPalette = { default, C(255, 213, 74), C(255, 255, 255), C(30, 30, 40), C(240, 120, 40) };

    public override string Title => "FLAP DASH";

    protected override void Reset()
    {
        _y = 40f;
        _vy = 0f;
        _started = false;
        for (int i = 0; i < _pipeX.Length; i++)
        {
            _pipeX[i] = 140f + i * SPACING;
            _gapY[i] = Rand(16f, GROUND - GAP - 8f);
            _passed[i] = false;
        }
    }

    protected override void Update(float dt, in ConsoleInput input)
    {
        bool flap = input.APressed || input.UpPressed;
        if (!_started)
        {
            if (!flap) { _y = 40f + Mathf.Sin(T * 4f) * 2f; return; }
            _started = true;
        }
        if (flap) _vy = -105f * Tune.Jump;
        _vy = Mathf.Min(_vy + 300f * Tune.Gravity * dt, 160f);
        _y += _vy * dt;
        if (_y < 10f) { _y = 10f; _vy = 0f; }

        float speed = Mathf.Min(64f, 40f + T * 0.5f);
        float maxX = 0f;
        for (int i = 0; i < _pipeX.Length; i++) maxX = Mathf.Max(maxX, _pipeX[i]);
        for (int i = 0; i < _pipeX.Length; i++)
        {
            _pipeX[i] -= speed * dt;
            if (_pipeX[i] < -PIPE_W)
            {
                _pipeX[i] = maxX + SPACING;
                _gapY[i] = Rand(16f, GROUND - GAP - 8f);
                _passed[i] = false;
            }
            if (!_passed[i] && _pipeX[i] + PIPE_W < BIRD_X - 3f)
            {
                _passed[i] = true;
                Score++;
            }
            float bx = BIRD_X - 3f, by = _y - 2f;
            if (Overlap(bx + 1, by + 1, 5, 3, _pipeX[i], 0f, PIPE_W, _gapY[i])
                || Overlap(bx + 1, by + 1, 5, 3, _pipeX[i], _gapY[i] + GAP, PIPE_W, GROUND - _gapY[i] - GAP))
                Over = true;
        }
        if (_y + 2f >= GROUND) Over = true;
    }

    public override void Draw(PixelScreen s)
    {
        s.Clear(C(112, 197, 206));
        // Clouds and hills drift slower than the pipes.
        for (int i = 0; i < 4; i++)
        {
            int cx = (int)Mathf.Repeat(i * 40f - T * 8f, W + 30f) - 20;
            s.Rect(cx, 18 + (i % 2) * 10, 16, 4, C(235, 248, 250));
            s.Rect(cx + 4, 15 + (i % 2) * 10, 8, 3, C(235, 248, 250));
            int hx = (int)Mathf.Repeat(i * 37f - T * 14f, W + 40f) - 30;
            s.Rect(hx, (int)GROUND - 8, 30, 8, C(140, 210, 150));
        }
        for (int i = 0; i < _pipeX.Length; i++)
        {
            int x = (int)_pipeX[i], gy = (int)_gapY[i], w = (int)PIPE_W;
            s.Rect(x, 0, w, gy, C(115, 191, 46));
            s.Rect(x - 1, gy - 4, w + 2, 4, C(140, 214, 60));
            s.Rect(x, gy + (int)GAP, w, (int)GROUND - gy - (int)GAP, C(115, 191, 46));
            s.Rect(x - 1, gy + (int)GAP, w + 2, 4, C(140, 214, 60));
            s.Rect(x + w - 3, 0, 2, gy - 4, C(85, 150, 35));
            s.Rect(x + w - 3, gy + (int)GAP + 4, 2, (int)GROUND - gy - (int)GAP - 4, C(85, 150, 35));
        }
        s.Rect(0, (int)GROUND, W, H - (int)GROUND, C(222, 216, 149));
        for (int x = (int)Mathf.Repeat(-T * 40f, 8f) - 8; x < W; x += 8)
            s.Rect(x, (int)GROUND, 4, 2, C(120, 190, 70));
        bool up = _vy < 0f && ((int)(T * 12f) & 1) == 0;
        s.Sprite((int)BIRD_X - 3, (int)_y - 2, up ? BirdFlap : Bird, BirdPalette);
        if (!_started && ((int)(T * 3f) & 1) == 0)
            s.TextShadowCentered(56, "PRESS SPACE", C(255, 255, 255), C(40, 60, 90));
        Header(s, C(40, 110, 130), C(255, 255, 255));
    }
}

/// <summary>PIXEL RUNNER: jump the cacti, duck the low birds, keep running. The desert speeds up.</summary>
public sealed class RunnerGame : ConsoleGame
{
    private const float GROUND = 80f, PX = 18f;

    private struct Obstacle { public float X; public byte Kind; public bool Active; }

    private readonly Obstacle[] _obstacles = new Obstacle[6];
    private float _feet, _vy, _distance, _nextSpawn;
    private bool _duck;

    private static readonly string[] Run1 = { ".11.", ".12.", "3333", "3333", ".33.", ".4.4", "4..4" };
    private static readonly string[] Run2 = { ".11.", ".12.", "3333", "3333", ".33.", ".44.", ".44." };
    private static readonly Color32[] RunnerPalette = { default, C(60, 40, 30), C(255, 214, 170), C(232, 89, 12), C(46, 26, 71) };

    public override string Title => "PIXEL RUNNER";

    protected override void Reset()
    {
        _feet = GROUND;
        _vy = 0f;
        _distance = 0f;
        _nextSpawn = 60f;
        _duck = false;
        for (int i = 0; i < _obstacles.Length; i++) _obstacles[i].Active = false;
    }

    private float Speed => Mathf.Min(118f, 56f + T * 1.6f);

    private static void Size(byte kind, out float w, out float h, out float lift)
    {
        switch (kind)
        {
            case 0: w = 4; h = 9; lift = 0; break;        // small cactus
            case 1: w = 6; h = 13; lift = 0; break;       // tall cactus
            case 2: w = 10; h = 9; lift = 0; break;       // cactus pair
            case 3: w = 9; h = 5; lift = 9; break;        // low bird: duck
            default: w = 9; h = 5; lift = 19; break;      // high bird: stay down
        }
    }

    protected override void Update(float dt, in ConsoleInput input)
    {
        bool grounded = _feet >= GROUND - 0.01f;
        if (grounded && (input.APressed || input.UpPressed))
        {
            _vy = -150f * Tune.Jump;
            grounded = false;
        }
        _duck = grounded && input.Y < 0;
        float gravity = 480f * Tune.Gravity * (!grounded && input.Y < 0 ? 2.2f : 1f);
        _vy += gravity * dt;
        _feet = Mathf.Min(GROUND, _feet + _vy * dt);
        if (_feet >= GROUND) _vy = 0f;

        float move = Speed * dt;
        _distance += move;
        Score = (int)(_distance / 10f);

        _nextSpawn -= move;
        if (_nextSpawn <= 0f)
        {
            for (int i = 0; i < _obstacles.Length; i++)
            {
                if (_obstacles[i].Active) continue;
                int roll = RandInt(0, T > 8f ? 10 : 7);
                byte kind = roll < 3 ? (byte)0 : roll < 5 ? (byte)1 : roll < 7 ? (byte)2 : roll < 9 ? (byte)3 : (byte)4;
                _obstacles[i] = new Obstacle { X = W + 4, Kind = kind, Active = true };
                break;
            }
            _nextSpawn = Rand(50f, 95f) + Speed * 0.3f;
        }

        float ph = _duck ? 6f : 11f, pw = _duck ? 9f : 5f;
        for (int i = 0; i < _obstacles.Length; i++)
        {
            ref var o = ref _obstacles[i];
            if (!o.Active) continue;
            o.X -= move;
            if (o.X < -14f) { o.Active = false; continue; }
            Size(o.Kind, out float w, out float h, out float lift);
            if (Overlap(PX + 1, _feet - ph, pw - 1, ph, o.X + 1, GROUND - lift - h, w - 2, h - 1))
                Over = true;
        }
    }

    public override void Draw(PixelScreen s)
    {
        s.Clear(C(250, 236, 210));
        s.Rect(96, 14, 12, 12, C(255, 200, 90));
        for (int i = 0; i < 3; i++)
        {
            int dx = (int)Mathf.Repeat(i * 50f - _distance * 0.2f, W + 50f) - 40;
            s.Rect(dx, (int)GROUND - 10, 40, 10, C(238, 214, 176));
            s.Rect(dx + 8, (int)GROUND - 16, 22, 6, C(238, 214, 176));
        }
        s.Rect(0, (int)GROUND, W, H - (int)GROUND, C(226, 190, 140));
        s.Rect(0, (int)GROUND, W, 1, C(120, 80, 60));
        for (int x = (int)Mathf.Repeat(-_distance, 11f) - 11; x < W; x += 11)
            s.Rect(x, (int)GROUND + 4 + (x & 3), 2, 1, C(170, 120, 80));

        for (int i = 0; i < _obstacles.Length; i++)
        {
            var o = _obstacles[i];
            if (!o.Active) continue;
            Size(o.Kind, out float w, out float h, out float lift);
            int x = (int)o.X, top = (int)(GROUND - lift - h);
            if (o.Kind <= 2)
            {
                var green = C(60, 150, 80);
                if (o.Kind == 2) { s.Rect(x, top, 4, (int)h, green); s.Rect(x + 6, top + 2, 4, (int)h - 2, green); }
                else
                {
                    s.Rect(x, top, (int)w, (int)h, green);
                    s.Rect(x - 2, top + 3, 2, 3, green);
                    s.Rect(x + (int)w, top + 2, 2, 3, green);
                }
            }
            else
            {
                bool wing = ((int)(T * 8f) & 1) == 0;
                var purple = C(120, 80, 170);
                s.Rect(x, top + 2, (int)w, 2, purple);
                s.Rect(x + 2, wing ? top : top + 4, 4, 2, purple);
                s.Set(x, top + 2, C(240, 150, 40));
            }
        }

        int px = (int)PX, feet = (int)_feet;
        if (_duck)
        {
            s.Rect(px, feet - 6, 9, 4, C(232, 89, 12));
            s.Rect(px + 6, feet - 7, 3, 3, C(255, 214, 170));
            s.Rect(px + 1, feet - 2, 2, 2, C(46, 26, 71));
            s.Rect(px + 5, feet - 2, 2, 2, C(46, 26, 71));
        }
        else
        {
            bool stride = ((int)(_distance / 6f) & 1) == 0 || feet < GROUND;
            s.Sprite(px, feet - 7, stride ? Run1 : Run2, RunnerPalette);
            s.Rect(px, feet - 11, 4, 4, C(60, 40, 30));
        }
        Header(s, C(120, 80, 60), C(255, 240, 220));
    }
}

/// <summary>BRICK BASH: steer the paddle, keep the ball up, smash every brick. Each new wall is faster.</summary>
public sealed class BrickGame : ConsoleGame
{
    private const int ROWS = 5, COLS = 10, BW = 11, BH = 4, BX0 = 4, BY0 = 14, PW = 20, PY = 88;

    private readonly bool[] _bricks = new bool[ROWS * COLS];
    private int _left, _wall;
    private float _px, _bx, _by, _vx, _vy;
    private bool _launched;

    private static readonly Color32[] RowColors = { C(255, 94, 91), C(255, 160, 70), C(255, 202, 61), C(79, 209, 160), C(92, 191, 235) };

    public override string Title => "BRICK BASH";

    protected override void Reset()
    {
        _wall = 0;
        NewWall();
        _px = W / 2f - PW / 2f;
        _launched = false;
    }

    private void NewWall()
    {
        for (int i = 0; i < _bricks.Length; i++) _bricks[i] = true;
        _left = _bricks.Length;
        _launched = false;
    }

    private float BallSpeed => Mathf.Min(140f, 78f + _wall * 12f + (ROWS * COLS - _left) * 0.6f);

    protected override void Update(float dt, in ConsoleInput input)
    {
        _px = Mathf.Clamp(_px + input.X * 125f * dt, 1f, W - 1f - PW);

        if (!_launched)
        {
            _bx = _px + PW / 2f - 1f;
            _by = PY - 3f;
            if (input.APressed || input.UpPressed || T > 3f && _wall == 0 && Score == 0)
            {
                _launched = true;
                float side = Rand(0f, 1f) < 0.5f ? -1f : 1f;
                _vx = side * BallSpeed * 0.5f;
                _vy = -Mathf.Sqrt(BallSpeed * BallSpeed - _vx * _vx);
            }
            return;
        }

        for (int sub = 0; sub < 3; sub++)
        {
            float h = dt / 3f;
            float oldX = _bx, oldY = _by;
            _bx += _vx * h;
            _by += _vy * h;
            if (_bx < 1f) { _bx = 1f; _vx = Mathf.Abs(_vx); }
            if (_bx > W - 3f) { _bx = W - 3f; _vx = -Mathf.Abs(_vx); }
            if (_by < 8f) { _by = 8f; _vy = Mathf.Abs(_vy); }

            if (_vy > 0f && Overlap(_bx, _by, 2, 2, _px, PY, PW, 3))
            {
                float offset = Mathf.Clamp((_bx + 1f - (_px + PW / 2f)) / (PW / 2f), -1f, 1f);
                float speed = BallSpeed;
                _vx = speed * offset * 0.8f;
                _vy = -Mathf.Sqrt(speed * speed - _vx * _vx);
                _by = PY - 2f;
            }

            int col = Mathf.FloorToInt((_bx + 1f - BX0) / (BW + 1)), row = Mathf.FloorToInt((_by + 1f - BY0) / (BH + 1));
            if (col >= 0 && col < COLS && row >= 0 && row < ROWS && _bricks[row * COLS + col])
            {
                _bricks[row * COLS + col] = false;
                _left--;
                Score++;
                int oldCol = Mathf.FloorToInt((oldX + 1f - BX0) / (BW + 1));
                if (oldCol != col) { _vx = -_vx; _bx = oldX; }
                else { _vy = -_vy; _by = oldY; }
                float scale = BallSpeed / Mathf.Max(1f, new Vector2(_vx, _vy).magnitude);
                _vx *= scale;
                _vy *= scale;
                if (_left == 0) { _wall++; NewWall(); return; }
            }
        }
        if (_by > H) Over = true;
    }

    public override void Draw(PixelScreen s)
    {
        s.Clear(C(22, 22, 52));
        s.Rect(0, 7, 1, H - 7, C(70, 70, 120));
        s.Rect(W - 1, 7, 1, H - 7, C(70, 70, 120));
        for (int r = 0; r < ROWS; r++)
            for (int c = 0; c < COLS; c++)
            {
                if (!_bricks[r * COLS + c]) continue;
                int x = BX0 + c * (BW + 1), y = BY0 + r * (BH + 1);
                s.Rect(x, y, BW, BH, RowColors[r]);
                s.Rect(x, y + BH - 1, BW, 1, C((byte)(RowColors[r].r * 0.6f), (byte)(RowColors[r].g * 0.6f), (byte)(RowColors[r].b * 0.6f)));
            }
        s.Rect((int)_px, PY, PW, 3, C(240, 240, 255));
        s.Rect((int)_px + 2, PY + 3, PW - 4, 1, C(150, 150, 190));
        s.Rect((int)_bx, (int)_by, 2, 2, C(255, 255, 255));
        if (!_launched && ((int)(T * 3f) & 1) == 0)
            s.TextShadowCentered(60, "PRESS SPACE", C(255, 255, 255), C(60, 40, 100));
        Header(s, C(46, 26, 71), C(255, 202, 61));
    }
}

/// <summary>SKY HOPPER: bounce up from platform to platform, steer left and right. Fall off the screen and it's over.</summary>
public sealed class HopperGame : ConsoleGame
{
    private const int N = 11, PLAT_W = 14;

    private readonly float[] _platX = new float[N];
    private readonly float[] _platY = new float[N];
    private readonly float[] _platV = new float[N];
    private readonly byte[] _platKind = new byte[N];   // 0 normal, 1 moving, 2 spring
    private float _x, _y, _vx, _vy, _camera, _top;
    private bool _facingLeft;

    private static readonly string[] Hopper = { ".1.1.", ".1.1.", "11111", "12121", "11111", "11111", ".1.1." };
    private static readonly Color32[] HopperPalette = { default, C(255, 125, 180), C(46, 26, 71) };

    public override string Title => "SKY HOPPER";

    protected override void Reset()
    {
        _x = 60f;
        _y = 70f;
        _vx = 0f;
        _vy = -170f;
        _camera = 0f;
        _platX[0] = 55f; _platY[0] = 86f; _platKind[0] = 0; _platV[0] = 0f;
        _top = 86f;
        for (int i = 1; i < N; i++) Place(i);
    }

    private void Place(int i)
    {
        int height = Mathf.Max(0, Score);
        _top -= Rand(10f, 14f + Mathf.Min(10f, height / 25f));
        _platY[i] = _top;
        _platX[i] = Rand(2f, W - PLAT_W - 2f);
        float roll = Rand(0f, 1f);
        _platKind[i] = roll < 0.08f ? (byte)2 : roll < Mathf.Min(0.45f, height / 200f) ? (byte)1 : (byte)0;
        _platV[i] = _platKind[i] == 1 ? (Rand(0f, 1f) < 0.5f ? -1f : 1f) * Rand(18f, 32f) : 0f;
    }

    protected override void Update(float dt, in ConsoleInput input)
    {
        _vx = input.X * 78f;
        if (input.X != 0) _facingLeft = input.X < 0;
        _x += _vx * dt;
        if (_x < -4f) _x += W;
        if (_x > W - 1f) _x -= W;

        float oldFeet = _y + 7f;
        _vy += 300f * Tune.Gravity * dt;
        _y += _vy * dt;

        for (int i = 0; i < N; i++)
        {
            if (_platKind[i] == 1)
            {
                _platX[i] += _platV[i] * dt;
                if (_platX[i] < 1f || _platX[i] > W - PLAT_W - 1f) _platV[i] = -_platV[i];
            }
            if (_vy > 0f && oldFeet <= _platY[i] && _y + 7f >= _platY[i] && _x + 4f > _platX[i] && _x + 1f < _platX[i] + PLAT_W)
            {
                _y = _platY[i] - 7f;
                _vy = (_platKind[i] == 2 ? -270f : -170f) * Tune.Jump;
            }
        }

        if (_y - _camera < 36f) _camera = _y - 36f;
        Score = Mathf.Max(Score, (int)((70f - _y) / 10f));
        for (int i = 0; i < N; i++)
            if (_platY[i] - _camera > H + 4f) Place(i);
        if (_y - _camera > H + 6f) Over = true;
    }

    public override void Draw(PixelScreen s)
    {
        // Night sky in bands, getting darker as you climb.
        float depth = Mathf.Clamp01(-_camera / 3000f);
        for (int band = 0; band < 6; band++)
        {
            byte r = (byte)Mathf.Lerp(70 - band * 8, 18, depth), g = (byte)Mathf.Lerp(50 - band * 5, 14, depth), b = (byte)Mathf.Lerp(120 - band * 10, 50, depth);
            s.Rect(0, band * 16, W, 16, C(r, g, b));
        }
        for (int i = 0; i < 18; i++)
        {
            int sx = (i * 53) % W, sy = (int)Mathf.Repeat(i * 37f - _camera * 0.25f, H);
            s.Set(sx, sy, ((i + (int)(T * 2f)) % 5) == 0 ? C(255, 255, 200) : C(200, 200, 230));
        }
        for (int i = 0; i < N; i++)
        {
            int x = (int)_platX[i], y = (int)(_platY[i] - _camera);
            var top = _platKind[i] == 1 ? C(92, 191, 235) : C(124, 212, 90);
            s.Rect(x, y, PLAT_W, 2, top);
            s.Rect(x + 1, y + 2, PLAT_W - 2, 1, C((byte)(top.r * 0.6f), (byte)(top.g * 0.6f), (byte)(top.b * 0.6f)));
            if (_platKind[i] == 2)
            {
                s.Rect(x + 5, y - 3, 4, 1, C(255, 94, 91));
                s.Rect(x + 6, y - 2, 2, 2, C(200, 200, 210));
            }
        }
        s.Sprite((int)_x, (int)(_y - _camera), Hopper, HopperPalette, _facingLeft);
        Header(s, C(24, 16, 48), C(255, 125, 180));
    }
}
