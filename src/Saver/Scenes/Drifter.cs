namespace ThimbleBerry.Screensavers.Saver.Scenes;

/// <summary>
/// Moves a block slowly around the screen, bouncing off the edges, so no pixel stays lit
/// for long (protects OLED and plasma screens from burn-in).
/// </summary>
public sealed class Drifter
{
    private PointF _pos;
    private PointF _vel;
    private SizeF _block;
    private Size _area;

    public PointF Position => _pos;

    public void Reset(Size area, SizeF block, Random rng)
    {
        _area = area;
        _block = block;
        _pos = new PointF(
            (float)rng.NextDouble() * Math.Max(0, area.Width - block.Width),
            (float)rng.NextDouble() * Math.Max(0, area.Height - block.Height));
        // About 3 px/s on a 1080p screen: slow enough to look still, fast enough to avoid burn-in.
        float speed = area.Height / 360f;
        double angle = rng.NextDouble() * Math.PI * 2;
        _vel = new PointF((float)Math.Cos(angle) * speed, (float)Math.Sin(angle) * speed);
    }

    public void Update(double dt)
    {
        float maxX = Math.Max(0, _area.Width - _block.Width);
        float maxY = Math.Max(0, _area.Height - _block.Height);
        _pos.X += (float)(_vel.X * dt);
        _pos.Y += (float)(_vel.Y * dt);
        if (_pos.X < 0 || _pos.X > maxX) { _vel.X = -_vel.X; _pos.X = Math.Clamp(_pos.X, 0, maxX); }
        if (_pos.Y < 0 || _pos.Y > maxY) { _vel.Y = -_vel.Y; _pos.Y = Math.Clamp(_pos.Y, 0, maxY); }
    }
}
