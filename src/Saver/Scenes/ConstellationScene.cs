using System.Drawing.Drawing2D;

namespace ThimbleBerry.Screensavers.Saver.Scenes;

/// <summary>Points drifting slowly, joined by faint lines when they come close.</summary>
public sealed class ConstellationScene : Scene
{
    private struct Node { public float X, Y, Vx, Vy; }

    private const int Levels = 8;
    private Node[] _nodes = [];
    private Pen[] _pens = [];
    private SolidBrush? _nodeBrush;
    private float _linkDistance;

    public override int PreferredFps => 12;

    protected override void OnResize()
    {
        int count = Math.Clamp(Size.Width * Size.Height / 40000, 12, 70);
        _linkDistance = Math.Min(Size.Width, Size.Height) / 5f;
        float speed = Math.Max(4f, Size.Height / 90f); // px per second

        _nodes = new Node[count];
        for (int i = 0; i < count; i++)
        {
            double a = Rng.NextDouble() * Math.PI * 2;
            float v = speed * (0.4f + (float)Rng.NextDouble() * 0.6f);
            _nodes[i] = new Node
            {
                X = (float)Rng.NextDouble() * Size.Width,
                Y = (float)Rng.NextDouble() * Size.Height,
                Vx = (float)Math.Cos(a) * v,
                Vy = (float)Math.Sin(a) * v,
            };
        }

        foreach (var p in _pens) p.Dispose();
        _nodeBrush?.Dispose();
        float width = Math.Max(1f, Size.Height / 1000f);
        _pens = Enumerable.Range(1, Levels).Select(i => new Pen(Dim(220, 100, 155, 0.3 + 0.7 * i / (double)Levels), width)).ToArray();
        _nodeBrush = new SolidBrush(Dim(235, 170, 200));
    }

    public override void Update(double dt)
    {
        for (int i = 0; i < _nodes.Length; i++)
        {
            ref var n = ref _nodes[i];
            n.X += (float)(n.Vx * dt);
            n.Y += (float)(n.Vy * dt);
            if (n.X < 0 || n.X > Size.Width) n.Vx = -n.Vx;
            if (n.Y < 0 || n.Y > Size.Height) n.Vy = -n.Vy;
        }
    }

    public override void Render(Graphics g)
    {
        g.Clear(Color.Black);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        float limit2 = _linkDistance * _linkDistance;

        for (int i = 0; i < _nodes.Length; i++)
        {
            for (int j = i + 1; j < _nodes.Length; j++)
            {
                float dx = _nodes[i].X - _nodes[j].X, dy = _nodes[i].Y - _nodes[j].Y;
                float d2 = dx * dx + dy * dy;
                if (d2 >= limit2) continue;
                int level = Math.Clamp((int)((1 - MathF.Sqrt(d2) / _linkDistance) * Levels), 0, Levels - 1);
                g.DrawLine(_pens[level], _nodes[i].X, _nodes[i].Y, _nodes[j].X, _nodes[j].Y);
            }
        }

        float r = Math.Max(1.5f, Size.Height / 450f);
        foreach (var n in _nodes)
            g.FillEllipse(_nodeBrush!, n.X - r, n.Y - r, r * 2, r * 2);
    }
}
