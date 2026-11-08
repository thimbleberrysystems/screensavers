using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ThimbleBerry.Screensavers.Saver.Scenes;

/// <summary>
/// Your own message, centred and wrapped, drifting slowly on black (burn-in safe).
/// The text never changes, so it only needs one frame per second.
/// </summary>
public sealed class TextScene : Scene
{
    private readonly Drifter _drift = new();
    private readonly string _text;
    private readonly Color _color;
    private readonly double _sizePercent;
    private readonly StringFormat _format = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Near };
    private Font? _font;
    private SizeF _blockSize;

    public TextScene(SaverSettings settings)
    {
        _text = string.IsNullOrWhiteSpace(settings.CustomText) ? " " : settings.CustomText.Replace("\r\n", "\n").Trim();
        _color = SaverSettings.ParseColor(settings.CustomTextColor);
        _sizePercent = settings.CustomTextSize;
    }

    public override int PreferredFps => 1;

    protected override void OnResize()
    {
        using var bmp = new Bitmap(1, 1);
        using var g = Graphics.FromImage(bmp);
        float maxWidth = Size.Width * 0.8f;
        float px = Math.Max(8f, (float)(Size.Height * _sizePercent / 100.0));

        // Shrink the font until the wrapped text fits in 80% of the screen.
        while (true)
        {
            _font?.Dispose();
            _font = new Font("Segoe UI Light", px, FontStyle.Regular, GraphicsUnit.Pixel);
            _blockSize = g.MeasureString(_text, _font, (int)maxWidth, _format);
            if (_blockSize.Height <= Size.Height * 0.8f || px <= 8f) break;
            px *= 0.9f;
        }
        _drift.Reset(Size, _blockSize, Rng);
    }

    public override void Update(double dt) => _drift.Update(dt);

    public override void Render(Graphics g)
    {
        g.Clear(Color.Black);
        if (_font is null) return;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var brush = new SolidBrush(Dim(_color.R, _color.G, _color.B));
        var pos = _drift.Position;
        g.DrawString(_text, _font, brush, new RectangleF(pos.X, pos.Y, _blockSize.Width, _blockSize.Height + 2), _format);
    }
}
