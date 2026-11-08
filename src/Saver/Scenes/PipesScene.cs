using System.Drawing.Drawing2D;

namespace ThimbleBerry.Screensavers.Saver.Scenes;

/// <summary>
/// Shaded pipes growing through a 3D grid, turning at rounded joints. When the screen
/// fills up the picture is cleared and new pipes start.
/// </summary>
public sealed class PipesScene : CanvasScene
{
    private static readonly (int X, int Y, int Z)[] Directions =
        [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)];

    private const double StepsPerSecond = 9;
    private const int GridY = 10;
    private const int GridZ = 10;
    private int _gridX;
    private bool[,,] _used = new bool[1, 1, 1];
    private double _cell;
    private double _camera;
    private double _stepClock;

    // Current pipe.
    private (int X, int Y, int Z) _pos;
    private int _dir = -1;
    private double _hue;
    private int _pipesThisRound;
    private int _cellsUsed;
    private int _length;

    public override int PreferredFps => 20;

    protected override void OnResize()
    {
        base.OnResize();
        _gridX = Math.Max(6, (int)Math.Round(GridY * Size.Width / (double)Math.Max(1, Size.Height)));
        // Size cells so the nearest layer of the grid roughly fills the screen.
        _cell = Size.Height / (GridY * 1.35);
        _camera = _cell * GridZ * 1.8;
        StartRound();
    }

    private void StartRound()
    {
        ClearCanvas();
        _used = new bool[_gridX, GridY, GridZ];
        _pipesThisRound = 0;
        _cellsUsed = 0;
        _dir = -1;
    }

    public override void Update(double dt)
    {
        if (Canvas is null) return;
        _stepClock += dt;
        int steps = 0;
        while (_stepClock >= 1 / StepsPerSecond && steps++ < 4)
        {
            _stepClock -= 1 / StepsPerSecond;
            Step();
        }
    }

    private void Step()
    {
        if (_dir < 0 && !StartPipe()) { StartRound(); return; }

        // Mostly keep going straight; turn sometimes, or when blocked.
        int next = _dir;
        if (!IsFree(Move(_pos, next)) || Rng.NextDouble() < 0.25)
        {
            var options = Enumerable.Range(0, Directions.Length).Where(d => IsFree(Move(_pos, d))).ToList();
            if (options.Count == 0 || _length > 60)
            {
                DrawJoint(_pos); // end cap
                _dir = -1;
                return;
            }
            next = options[Rng.Next(options.Count)];
        }

        if (next != _dir) DrawJoint(_pos);
        var to = Move(_pos, next);
        DrawSegment(_pos, to);
        Occupy(to);
        _pos = to;
        _dir = next;
        _length++;
    }

    private bool StartPipe()
    {
        int total = _gridX * GridY * GridZ;
        if (_pipesThisRound >= 14 || _cellsUsed > total * 0.3) return false;
        for (int attempt = 0; attempt < 50; attempt++)
        {
            var p = (Rng.Next(_gridX), Rng.Next(GridY), Rng.Next(GridZ));
            if (!IsFree(p)) continue;
            _pos = p;
            Occupy(p);
            _dir = Rng.Next(Directions.Length);
            _hue = Rng.NextDouble() * 360;
            _length = 0;
            _pipesThisRound++;
            DrawJoint(p);
            return true;
        }
        return false;
    }

    private static (int X, int Y, int Z) Move((int X, int Y, int Z) p, int d) =>
        (p.X + Directions[d].X, p.Y + Directions[d].Y, p.Z + Directions[d].Z);

    private bool IsFree((int X, int Y, int Z) p) =>
        p.X >= 0 && p.Y >= 0 && p.Z >= 0 && p.X < _gridX && p.Y < GridY && p.Z < GridZ && !_used[p.X, p.Y, p.Z];

    private void Occupy((int X, int Y, int Z) p)
    {
        _used[p.X, p.Y, p.Z] = true;
        _cellsUsed++;
    }

    /// <summary>Grid cell to screen point plus the perspective scale at that depth.</summary>
    private (PointF Point, float Scale) Project((int X, int Y, int Z) p)
    {
        double x = (p.X - (_gridX - 1) / 2.0) * _cell;
        double y = (p.Y - (GridY - 1) / 2.0) * _cell;
        double z = (p.Z - (GridZ - 1) / 2.0) * _cell;
        double k = _camera / (_camera + z);
        return (new PointF((float)(Size.Width / 2.0 + x * k), (float)(Size.Height / 2.0 + y * k)), (float)k);
    }

    private float Radius(float scale) => (float)(_cell * 0.2 * scale);

    private void DrawSegment((int X, int Y, int Z) from, (int X, int Y, int Z) to)
    {
        var (a, ka) = Project(from);
        var (b, kb) = Project(to);
        float r = Radius((ka + kb) / 2);
        float dx = b.X - a.X, dy = b.Y - a.Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);

        if (len < r * 0.6f)
        {
            // Pointing almost straight at the viewer: the pipe is seen end-on.
            DrawJoint(to);
            return;
        }

        // Shading runs across the pipe (perpendicular to it) to make it look round.
        float px = -dy / len * r, py = dx / len * r;
        using var brush = new LinearGradientBrush(
            new PointF(a.X + px, a.Y + py), new PointF(a.X - px, a.Y - py), Color.Black, Color.Black)
        {
            InterpolationColors = new ColorBlend
            {
                Colors = [Hsv(_hue, 0.75, 0.25), Hsv(_hue, 0.55, 1.0), Hsv(_hue, 0.8, 0.45), Hsv(_hue, 0.85, 0.15)],
                Positions = [0f, 0.3f, 0.7f, 1f],
            },
            WrapMode = WrapMode.TileFlipXY,
        };
        Canvas!.FillPolygon(brush, [
            new PointF(a.X + px, a.Y + py), new PointF(b.X + px, b.Y + py),
            new PointF(b.X - px, b.Y - py), new PointF(a.X - px, a.Y - py)]);
    }

    private void DrawJoint((int X, int Y, int Z) p)
    {
        var (c, k) = Project(p);
        float r = Radius(k) * 1.15f;
        using var path = new GraphicsPath();
        path.AddEllipse(c.X - r, c.Y - r, r * 2, r * 2);
        using var brush = new PathGradientBrush(path)
        {
            CenterPoint = new PointF(c.X - r * 0.35f, c.Y - r * 0.35f),
            CenterColor = Hsv(_hue, 0.45, 1.0),
            SurroundColors = [Hsv(_hue, 0.85, 0.2)],
        };
        Canvas!.FillPath(brush, path);
    }
}
