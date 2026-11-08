namespace ThimbleBerry.Screensavers.Saver.Scenes;

/// <summary>Twisting coloured ribbons that wind across the screen, build up, then start over.</summary>
public sealed class RibbonsScene : CanvasScene
{
    private sealed class Ribbon
    {
        public double X, Y, Heading, Turn, Twist, TwistSpeed, Hue;
        public PointF Left, Right;
        public bool HasEdge;
    }

    private const double RoundSeconds = 45;
    private Ribbon[] _ribbons = [];
    private double _speed;
    private double _halfWidth;
    private double _roundTime;

    public override int PreferredFps => 20;

    protected override void OnResize()
    {
        base.OnResize();
        // Anti-aliased neighbouring pieces leave faint seams; without it the ribbon is solid.
        if (Canvas is not null) Canvas.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighSpeed;
        StartRound();
    }

    private void StartRound()
    {
        ClearCanvas();
        _roundTime = 0;
        _speed = Size.Height / 5.0;
        _halfWidth = Math.Max(3, Size.Height / 60.0);
        _ribbons = Enumerable.Range(0, 3).Select(_ => new Ribbon
        {
            X = Rng.NextDouble() * Size.Width,
            Y = Rng.NextDouble() * Size.Height,
            Heading = Rng.NextDouble() * Math.PI * 2,
            Turn = 0,
            Twist = Rng.NextDouble() * Math.PI,
            TwistSpeed = 1.2 + Rng.NextDouble() * 1.5,
            Hue = Rng.NextDouble() * 360,
        }).ToArray();
    }

    public override void Update(double dt)
    {
        _roundTime += dt;
        if (_roundTime > RoundSeconds) { StartRound(); return; }
        if (Canvas is null) return;

        foreach (var r in _ribbons)
        {
            // Wander: the turn rate drifts randomly; steer back when near an edge.
            r.Turn += (Rng.NextDouble() - 0.5) * 3.0 * dt;
            r.Turn = Math.Clamp(r.Turn, -1.4, 1.4);
            double margin = Size.Height * 0.08;
            if (r.X < margin || r.X > Size.Width - margin || r.Y < margin || r.Y > Size.Height - margin)
            {
                double toCentre = Math.Atan2(Size.Height / 2.0 - r.Y, Size.Width / 2.0 - r.X);
                double diff = Math.IEEERemainder(toCentre - r.Heading, Math.PI * 2);
                r.Turn = Math.Sign(diff) * 1.6;
            }

            r.Heading += r.Turn * dt;
            r.X += Math.Cos(r.Heading) * _speed * dt;
            r.Y += Math.Sin(r.Heading) * _speed * dt;
            r.Twist += r.TwistSpeed * dt;
            r.Hue += 8 * dt;

            // The visible width follows cos(twist), which makes the ribbon look like it rolls over.
            double w = _halfWidth * Math.Cos(r.Twist);
            double px = -Math.Sin(r.Heading) * w, py = Math.Cos(r.Heading) * w;
            var left = new PointF((float)(r.X + px), (float)(r.Y + py));
            var right = new PointF((float)(r.X - px), (float)(r.Y - py));

            if (r.HasEdge)
            {
                double light = 0.35 + 0.65 * Math.Abs(Math.Cos(r.Twist));
                using var brush = new SolidBrush(Hsv(r.Hue, 0.7, light));
                Canvas.FillPolygon(brush, [r.Left, r.Right, right, left]);
            }
            r.Left = left;
            r.Right = right;
            r.HasEdge = true;
        }
    }
}
