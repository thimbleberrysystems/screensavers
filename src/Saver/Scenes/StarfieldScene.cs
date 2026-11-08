namespace ThimbleBerry.Screensavers.Saver.Scenes;

/// <summary>A slow flight through a sparse starfield. Mostly black pixels.</summary>
public sealed class StarfieldScene : Scene
{
    private struct Star { public double X, Y, Z; }

    private Star[] _stars = [];
    private SolidBrush[] _brushes = [];

    public override int PreferredFps => 15;

    protected override void OnResize()
    {
        int count = Math.Clamp(Size.Width * Size.Height / 9000, 40, 350);
        _stars = new Star[count];
        for (int i = 0; i < count; i++) _stars[i] = NewStar(randomDepth: true);

        foreach (var b in _brushes) b.Dispose();
        // Eight pre-built brightness levels, so no brushes are created while drawing.
        _brushes = Enumerable.Range(1, 8).Select(i => new SolidBrush(Dim(225, 225, 255, 0.4 + 0.6 * i / 8.0))).ToArray();
    }

    private Star NewStar(bool randomDepth) => new()
    {
        X = Rng.NextDouble() * 2 - 1,
        Y = Rng.NextDouble() * 2 - 1,
        Z = randomDepth ? 0.05 + Rng.NextDouble() * 0.95 : 1.0,
    };

    public override void Update(double dt)
    {
        const double speed = 0.06; // depth units per second
        for (int i = 0; i < _stars.Length; i++)
        {
            _stars[i].Z -= speed * dt;
            if (_stars[i].Z <= 0.05) _stars[i] = NewStar(randomDepth: false);
        }
    }

    public override void Render(Graphics g)
    {
        g.Clear(Color.Black);
        float cx = Size.Width / 2f, cy = Size.Height / 2f;
        float scale = Math.Max(Size.Width, Size.Height) / 2f;
        float maxSize = Math.Max(1.5f, Size.Height / 400f);

        foreach (var s in _stars)
        {
            float x = cx + (float)(s.X / s.Z) * scale * 0.5f;
            float y = cy + (float)(s.Y / s.Z) * scale * 0.5f;
            if (x < 0 || y < 0 || x >= Size.Width || y >= Size.Height) continue;

            double nearness = 1 - s.Z; // 0 far .. ~1 near
            float size = (float)(1.0 + nearness * maxSize);
            int level = Math.Clamp((int)(nearness * 8), 0, 7);
            g.FillRectangle(_brushes[level], x, y, size, size);
        }
    }
}
