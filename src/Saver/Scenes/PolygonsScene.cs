using System.Drawing.Drawing2D;

namespace ThimbleBerry.Screensavers.Saver.Scenes;

/// <summary>Two bouncing four-sided outlines, each trailing fading copies of itself as its colour shifts.</summary>
public sealed class PolygonsScene : Scene
{
    private const int Vertices = 4;
    private const int Trail = 11;

    private sealed class Shape
    {
        public PointF[] Pos = new PointF[Vertices];
        public PointF[] Vel = new PointF[Vertices];
        public PointF[][] History = new PointF[Trail][];
        public int Head;
        public int Count;
        public double Hue;
        public double HueSpeed;
    }

    private Shape[] _shapes = [];
    private double _sinceSnapshot;

    public override int PreferredFps => 20;

    protected override void OnResize()
    {
        float speed = Size.Height / 4f;
        _shapes = new Shape[2];
        for (int s = 0; s < _shapes.Length; s++)
        {
            var shape = new Shape { Hue = Rng.NextDouble() * 360, HueSpeed = 12 + Rng.NextDouble() * 10 };
            for (int i = 0; i < Vertices; i++)
            {
                shape.Pos[i] = new PointF((float)Rng.NextDouble() * Size.Width, (float)Rng.NextDouble() * Size.Height);
                double a = Rng.NextDouble() * Math.PI * 2;
                float v = speed * (0.5f + (float)Rng.NextDouble() * 0.5f);
                shape.Vel[i] = new PointF((float)Math.Cos(a) * v, (float)Math.Sin(a) * v);
            }
            for (int t = 0; t < Trail; t++) shape.History[t] = new PointF[Vertices];
            _shapes[s] = shape;
        }
    }

    public override void Update(double dt)
    {
        foreach (var s in _shapes)
        {
            s.Hue += s.HueSpeed * dt;
            for (int i = 0; i < Vertices; i++)
            {
                ref var p = ref s.Pos[i];
                ref var v = ref s.Vel[i];
                p.X += (float)(v.X * dt);
                p.Y += (float)(v.Y * dt);
                if (p.X < 0) { p.X = 0; v.X = Math.Abs(v.X); }
                if (p.X > Size.Width) { p.X = Size.Width; v.X = -Math.Abs(v.X); }
                if (p.Y < 0) { p.Y = 0; v.Y = Math.Abs(v.Y); }
                if (p.Y > Size.Height) { p.Y = Size.Height; v.Y = -Math.Abs(v.Y); }
            }
        }

        // Record trail copies at a fixed spacing in time, independent of frame rate.
        _sinceSnapshot += dt;
        if (_sinceSnapshot < 0.06) return;
        _sinceSnapshot = 0;
        foreach (var s in _shapes)
        {
            s.Head = (s.Head + 1) % Trail;
            Array.Copy(s.Pos, s.History[s.Head], Vertices);
            s.Count = Math.Min(Trail, s.Count + 1);
        }
    }

    public override void Render(Graphics g)
    {
        g.Clear(Color.Black);
        // Anti-aliasing long, screen-crossing lines costs ~5x more in GDI+; crisp lines suit this scene anyway.
        g.SmoothingMode = SmoothingMode.HighSpeed;
        float width = Math.Max(1f, MathF.Round(Size.Height / 700f));
        foreach (var s in _shapes)
        {
            for (int age = s.Count - 1; age >= 0; age--)
            {
                var poly = s.History[(s.Head - age + Trail) % Trail];
                double fade = 1.0 - age / (double)Trail;
                using var pen = new Pen(Hsv(s.Hue - age * 4, 0.75, fade * fade), width);
                g.DrawPolygon(pen, poly);
            }
        }
    }
}
