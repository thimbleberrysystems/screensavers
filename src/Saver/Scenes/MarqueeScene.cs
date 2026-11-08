using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace ThimbleBerry.Screensavers.Saver.Scenes;

/// <summary>Your custom text scrolling right to left, at a new height each pass (burn-in safe).</summary>
public sealed class MarqueeScene : Scene
{
    private readonly string _text;
    private readonly Color _color;
    private readonly double _sizePercent;
    private readonly double _speedFactor;
    private Font? _font;
    private SizeF _textSize;
    private float _x;
    private float _y;

    public MarqueeScene(SaverSettings settings)
    {
        var text = string.IsNullOrWhiteSpace(settings.CustomText) ? "ThimbleBerry" : settings.CustomText;
        _text = string.Join("   ", text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        _color = SaverSettings.ParseColor(settings.CustomTextColor);
        _sizePercent = settings.CustomTextSize;
        _speedFactor = settings.Text3DSpeed / 5.0;
    }

    public override int PreferredFps => 30; // scrolling needs more frames than the other text scenes to look smooth

    protected override void OnResize()
    {
        _font?.Dispose();
        _font = new Font("Segoe UI", Math.Max(8f, (float)(Size.Height * _sizePercent / 100.0)), FontStyle.Regular, GraphicsUnit.Pixel);
        using var bmp = new Bitmap(1, 1);
        using var g = Graphics.FromImage(bmp);
        _textSize = g.MeasureString(_text, _font);
        StartPass();
    }

    private void StartPass()
    {
        _x = Size.Width;
        _y = (float)Rng.NextDouble() * Math.Max(0, Size.Height - _textSize.Height);
    }

    public override void Update(double dt)
    {
        _x -= (float)(Size.Width / 8.0 * _speedFactor * dt); // about 8 s to cross at speed 5
        if (_x < -_textSize.Width) StartPass();
    }

    public override void Render(Graphics g)
    {
        g.Clear(Color.Black);
        if (_font is null) return;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(Dim(_color.R, _color.G, _color.B));
        g.DrawString(_text, _font, brush, _x, _y);
    }

    public override void Dispose() => _font?.Dispose();
}
