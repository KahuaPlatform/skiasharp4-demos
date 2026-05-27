using System.Text;
using SkiaSharp;

namespace UnoGallery.LiveTiles;

/// <summary>
/// L-system tree that animates its own growth from the bottom up. Each
/// "tree" is defined by an axiom + production rule + turn angle; we expand
/// the rules a fixed number of times to get a long instruction string,
/// then progressively render more of it each frame until the tree is
/// fully drawn. After a short dwell, we pick the next rule set and reset.
///
/// Turtle commands:
///   F    forward one step, drawing
///   +    turn left by the system's angle
///   -    turn right by the system's angle
///   [ ]  push / pop the turtle state
/// </summary>
public sealed class LSystemTile : ILiveTile
{
    record TreeKind(string Caption, string Axiom, string Rule, float AngleDeg, int Iterations);

    static readonly TreeKind[] Trees =
    {
        // Symmetric binary tree — classic. Five iterations gives ~3k chars.
        new("Binary",  "F", "FF+[+F-F-F]-[-F+F+F]", 22.5f, 4),
        // Bushy plant
        new("Plant",   "X", new string('F', 0) + "F-[[X]+X]+F[+FX]-X".Replace("X", "X"), 25f, 5),
        // Spiral / curly
        new("Spiral",  "F", "FF-[-F+F+F]+[+F-F-F]", 20f, 4),
        // Asymmetric weeping
        new("Weep",    "F", "F[+F]F[-F][F]", 20f, 4),
    };

    const float DrawSpeed = 280f;   // characters interpreted per second
    const float CompleteDwell = 4f; // wait this long after fully drawn before restarting

    string _instructions = "";
    int _drawCount;
    float _localTimeBase;
    float _completedAt = -1f;
    int _kindIdx = -1;
    float _maxExtent = 1f;
    int _maxDepth = 1;        // computed once at expansion time

    public string Caption => "Tree";

    public ImmutableArray<SKColor> Palette { get; } = ImmutableArray.Create(
        new SKColor(10, 16, 24),
        new SKColor(80, 200, 130),
        new SKColor(200, 240, 160),
        new SKColor(255, 220, 130));

    public LSystemTile()
    {
        SelectNext(0f);
    }

    public void Draw(SKCanvas canvas, SKRect dest, float t)
    {
        // Drive growth from a local time origin so each rotation of the tree
        // starts at draw step 0 cleanly.
        float local = t - _localTimeBase;
        int targetDrawCount = Math.Min(_instructions.Length, (int)(local * DrawSpeed));
        _drawCount = targetDrawCount;

        if (_drawCount >= _instructions.Length)
        {
            if (_completedAt < 0f) _completedAt = t;
            if (t - _completedAt > CompleteDwell) SelectNext(t);
        }

        using var bg = new SKPaint { Color = Palette[0] };
        canvas.DrawRect(dest, bg);

        DrawTurtle(canvas, dest);
    }

    void SelectNext(float t)
    {
        _kindIdx = (_kindIdx + 1) % Trees.Length;
        var kind = Trees[_kindIdx];
        _instructions = ExpandRule(kind.Axiom, kind.Rule, kind.Iterations);
        _drawCount = 0;
        _localTimeBase = t;
        _completedAt = -1f;
        _maxExtent = ComputeMaxExtent(kind);

        // Compute max nesting depth once — used to grade branch colour/stroke
        // by recursion level. Previously this was a full string scan per frame.
        int depth = 0, maxDepth = 0;
        foreach (char ch in _instructions)
        {
            if (ch == '[') { depth++; if (depth > maxDepth) maxDepth = depth; }
            else if (ch == ']' && depth > 0) depth--;
        }
        _maxDepth = Math.Max(1, maxDepth);
    }

    static string ExpandRule(string axiom, string rule, int iterations)
    {
        var current = new StringBuilder(axiom);
        for (int i = 0; i < iterations; i++)
        {
            var next = new StringBuilder(current.Length * 4);
            for (int c = 0; c < current.Length; c++)
            {
                char ch = current[c];
                if (ch == 'F') next.Append(rule);
                else next.Append(ch);
            }
            current = next;
            // Soft cap so we don't OOM on aggressive rules
            if (current.Length > 50000) break;
        }
        return current.ToString();
    }

    /// <summary>
    /// Dry-run the turtle once to find the largest displacement from origin,
    /// so the actual draw can be scaled to fit the tile snugly.
    /// </summary>
    float ComputeMaxExtent(TreeKind kind)
    {
        float x = 0f, y = 0f;
        float angle = MathF.PI / 2f;
        float angleStep = kind.AngleDeg * MathF.PI / 180f;
        var stack = new Stack<(float x, float y, float a)>();
        float maxR = 1f;

        foreach (char ch in _instructions)
        {
            switch (ch)
            {
                case 'F':
                    x += MathF.Cos(angle);
                    y += MathF.Sin(angle);
                    float r = MathF.Sqrt(x * x + y * y);
                    if (r > maxR) maxR = r;
                    break;
                case '+': angle += angleStep; break;
                case '-': angle -= angleStep; break;
                case '[': stack.Push((x, y, angle)); break;
                case ']': if (stack.Count > 0) { (x, y, angle) = stack.Pop(); } break;
            }
        }
        return maxR;
    }

    void DrawTurtle(SKCanvas canvas, SKRect dest)
    {
        var kind = Trees[_kindIdx];
        float angle = MathF.PI / 2f;   // start pointing up
        float angleStep = kind.AngleDeg * MathF.PI / 180f;

        // Scale tree to fit ~85 % of tile height; origin at bottom-centre.
        float stepLen = MathF.Min(dest.Width, dest.Height) * 0.40f / _maxExtent;
        float originX = dest.MidX;
        float originY = dest.Bottom - dest.Height * 0.08f;
        float x = originX, y = originY;

        var stack = new Stack<(float x, float y, float a, int depth)>();
        int depth = 0;
        int maxDepth = _maxDepth; // cached at expansion time — no per-frame string scan

        // Each branch is drawn with one paint reused per depth.
        var paintCache = new SKPaint[maxDepth + 1];

        for (int i = 0; i < _drawCount; i++)
        {
            char ch = _instructions[i];
            switch (ch)
            {
                case 'F':
                    float nx = x + MathF.Cos(angle) * stepLen;
                    float ny = y - MathF.Sin(angle) * stepLen;       // canvas Y points down
                    var paint = paintCache[depth] ??= MakeBranchPaint(depth, maxDepth);
                    canvas.DrawLine(x, y, nx, ny, paint);
                    x = nx; y = ny;
                    break;
                case '+': angle += angleStep; break;
                case '-': angle -= angleStep; break;
                case '[': stack.Push((x, y, angle, depth)); depth++; break;
                case ']':
                    if (stack.Count > 0)
                    {
                        var s = stack.Pop();
                        x = s.x; y = s.y; angle = s.a; depth = s.depth;
                    }
                    break;
            }
        }

        // Dispose paint cache.
        foreach (var p in paintCache) p?.Dispose();
    }

    SKPaint MakeBranchPaint(int depth, int maxDepth)
    {
        float u = depth / (float)maxDepth;
        var col = LerpPalette(0.2f + u * 0.7f);
        float stroke = Math.Max(0.8f, (1f - u) * 2.6f);
        return new SKPaint
        {
            Color = col,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = stroke,
            StrokeCap = SKStrokeCap.Round,
            IsAntialias = true,
        };
    }

    SKColor LerpPalette(float t)
    {
        t = Math.Clamp(t, 0f, 0.999f);
        float scaled = t * (Palette.Length - 1);
        int idx = (int)scaled;
        float f = scaled - idx;
        var a = Palette[idx];
        var b = Palette[Math.Min(idx + 1, Palette.Length - 1)];
        return new SKColor(
            (byte)(a.Red + (b.Red - a.Red) * f),
            (byte)(a.Green + (b.Green - a.Green) * f),
            (byte)(a.Blue + (b.Blue - a.Blue) * f));
    }
}
