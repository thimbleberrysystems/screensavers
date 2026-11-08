using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ThimbleBerry.Screensavers.Saver.Scenes;

/// <summary>
/// Time and date on black, drifting slowly so no pixel stays lit for long (burn-in safe).
/// Draws once per second: the cheapest scene by far.
/// </summary>
public sealed class ClockScene : Scene
{
    private Font? _timeFont;
    private Font? _dateFont;
    private SizeF _blockSize;
    private PointF _pos;
    private PointF _vel;

    public override int PreferredFps => 1;

    protected override void OnResize()
    {
        _timeFont?.Dispose();
        _dateFont?.Dispose();
        float timePx = Math.Max(10f, Size.Height / 7f);
        _timeFont = new Font("Segoe UI Light", timePx, FontStyle.Regular, GraphicsUnit.Pixel);
        _dateFont = new Font("Segoe UI", Math.Max(6f, timePx / 4.5f), FontStyle.Regular, GraphicsUnit.Pixel);

        using var bmp = new Bitmap(1, 1);
        using var g = Graphics.FromImage(bmp);
        var t = g.MeasureString("00:00", _timeFont);
        var d = g.MeasureString("Wednesday, September 30", _dateFont);
        _blockSize = new SizeF(Math.Max(t.Width, d.Width), t.Height + d.Height);

        _pos = new PointF(
            (float)Rng.NextDouble() * Math.Max(1, Size.Width - _blockSize.Width),
            (float)Rng.NextDouble() * Math.Max(1, Size.Height - _blockSize.Height));
        // About 3 px/s on a 1080p screen: slow enough to look still, fast enough to avoid burn-in.
        float speed = Size.Height / 360f;
        double angle = Rng.NextDouble() * Math.PI * 2;
        _vel = new PointF((float)Math.Cos(angle) * speed, (float)Math.Sin(angle) * speed);
    }

    public override void Update(double dt)
    {
        float maxX = Math.Max(0, Size.Width - _blockSize.Width);
        float maxY = Math.Max(0, Size.Height - _blockSize.Height);
        _pos.X += (float)(_vel.X * dt);
        _pos.Y += (float)(_vel.Y * dt);
        if (_pos.X < 0 || _pos.X > maxX) { _vel.X = -_vel.X; _pos.X = Math.Clamp(_pos.X, 0, maxX); }
        if (_pos.Y < 0 || _pos.Y > maxY) { _vel.Y = -_vel.Y; _pos.Y = Math.Clamp(_pos.Y, 0, maxY); }
    }

    public override void Render(Graphics g)
    {
        g.Clear(Color.Black);
        if (_timeFont is null || _dateFont is null) return;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var now = DateTime.Now;
        string time = now.ToString("t");
        string date = now.ToString("dddd, MMMM d");

        var timeSize = g.MeasureString(time, _timeFont);
        var dateSize = g.MeasureString(date, _dateFont);
        float cx = _pos.X + _blockSize.Width / 2;

        using var timeBrush = new SolidBrush(Dim(230, 230, 235));
        using var dateBrush = new SolidBrush(Dim(200, 120, 150, 0.8)); // berry tint
        g.DrawString(time, _timeFont, timeBrush, cx - timeSize.Width / 2, _pos.Y);
        g.DrawString(date, _dateFont, dateBrush, cx - dateSize.Width / 2, _pos.Y + timeSize.Height * 0.92f);
    }
}
