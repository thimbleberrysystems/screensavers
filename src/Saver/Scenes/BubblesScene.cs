using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ThimbleBerry.Screensavers.Saver.Scenes;

/// <summary>Glossy see-through bubbles drifting and bouncing off each other and the screen edges.</summary>
public sealed class BubblesScene : Scene
{
    private sealed class Bubble
    {
        public float X, Y, Vx, Vy, R;
        public Bitmap Sprite = null!;
    }

    private Bubble[] _bubbles = [];

    public override int PreferredFps => 20;

    protected override void OnResize()
    {
        DisposeSprites();
        int count = Math.Clamp(Size.Width * Size.Height / 140000, 5, 16);
        float speed = Size.Height / 9f;
        var list = new List<Bubble>();
        for (int i = 0; i < count; i++)
        {
            float r = Size.Height * (0.04f + (float)Rng.NextDouble() * 0.06f);
            double a = Rng.NextDouble() * Math.PI * 2;
            float v = speed * (0.5f + (float)Rng.NextDouble() * 0.5f);
            var b = new Bubble
            {
                R = r,
                X = r + (float)Rng.NextDouble() * Math.Max(1, Size.Width - 2 * r),
                Y = r + (float)Rng.NextDouble() * Math.Max(1, Size.Height - 2 * r),
                Vx = (float)Math.Cos(a) * v,
                Vy = (float)Math.Sin(a) * v,
            };
            b.Sprite = MakeSprite(r, Rng.NextDouble() * 360);
            list.Add(b);
        }
        _bubbles = list.ToArray();
    }

    /// <summary>Draws one bubble once; frames then only copy this small image.</summary>
    private Bitmap MakeSprite(float r, double hue)
    {
        int d = Math.Max(4, (int)Math.Ceiling(r * 2) + 2);
        var bmp = new Bitmap(d, d, PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new RectangleF(1, 1, r * 2, r * 2);

        using (var body = new GraphicsPath())
        {
            body.AddEllipse(rect);
            using var brush = new PathGradientBrush(body)
            {
                CenterColor = Hsv(hue, 0.5, 0.35, 0.10),
                SurroundColors = [Hsv(hue, 0.55, 1.0, 0.85)],
                FocusScales = new PointF(0.75f, 0.75f),
            };
            g.FillPath(brush, body);
        }

        // Soft highlight towards the top left.
        var hl = new RectangleF(rect.X + r * 0.35f, rect.Y + r * 0.25f, r * 0.7f, r * 0.45f);
        using (var hlPath = new GraphicsPath())
        {
            hlPath.AddEllipse(hl);
            using var brush = new PathGradientBrush(hlPath)
            {
                CenterColor = Hsv(0, 0, 1.0, 0.75),
                SurroundColors = [Hsv(0, 0, 1.0, 0.0)],
            };
            g.FillPath(brush, hlPath);
        }
        return bmp;
    }

    public override void Update(double dt)
    {
        foreach (var b in _bubbles)
        {
            b.X += (float)(b.Vx * dt);
            b.Y += (float)(b.Vy * dt);
            if (b.X < b.R) { b.X = b.R; b.Vx = Math.Abs(b.Vx); }
            if (b.X > Size.Width - b.R) { b.X = Size.Width - b.R; b.Vx = -Math.Abs(b.Vx); }
            if (b.Y < b.R) { b.Y = b.R; b.Vy = Math.Abs(b.Vy); }
            if (b.Y > Size.Height - b.R) { b.Y = Size.Height - b.R; b.Vy = -Math.Abs(b.Vy); }
        }

        // Elastic collisions; heavier (bigger) bubbles push smaller ones more.
        for (int i = 0; i < _bubbles.Length; i++)
        {
            for (int j = i + 1; j < _bubbles.Length; j++)
            {
                var a = _bubbles[i];
                var b = _bubbles[j];
                float dx = b.X - a.X, dy = b.Y - a.Y;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                float minDist = a.R + b.R;
                if (dist >= minDist || dist < 0.001f) continue;

                float nx = dx / dist, ny = dy / dist;
                float ma = a.R * a.R, mb = b.R * b.R;
                float overlap = minDist - dist;
                a.X -= nx * overlap * mb / (ma + mb);
                a.Y -= ny * overlap * mb / (ma + mb);
                b.X += nx * overlap * ma / (ma + mb);
                b.Y += ny * overlap * ma / (ma + mb);

                float rel = (a.Vx - b.Vx) * nx + (a.Vy - b.Vy) * ny;
                if (rel <= 0) continue;
                float impulse = 2 * rel / (ma + mb);
                a.Vx -= impulse * mb * nx; a.Vy -= impulse * mb * ny;
                b.Vx += impulse * ma * nx; b.Vy += impulse * ma * ny;
            }
        }
    }

    public override void Render(Graphics g)
    {
        g.Clear(Color.Black);
        foreach (var b in _bubbles)
            g.DrawImageUnscaled(b.Sprite, (int)(b.X - b.R - 1), (int)(b.Y - b.R - 1));
    }

    private void DisposeSprites()
    {
        foreach (var b in _bubbles) b.Sprite.Dispose();
        _bubbles = [];
    }

    public override void Dispose() => DisposeSprites();
}
