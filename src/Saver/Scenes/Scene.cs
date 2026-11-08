namespace ThimbleBerry.Screensavers.Saver.Scenes;

/// <summary>
/// One animated scene drawn into one window. Scenes are drawn with GDI+ on a timer;
/// nothing runs between frames, which is what keeps CPU and GPU use low.
/// </summary>
public abstract class Scene : IDisposable
{
    public virtual void Dispose() { }

    /// <summary>Hue 0..360, saturation and value 0..1, then scaled by the brightness setting.</summary>
    protected Color Hsv(double h, double s, double v, double alpha = 1.0)
    {
        h = ((h % 360) + 360) % 360 / 60.0;
        double c = v * s, x = c * (1 - Math.Abs(h % 2 - 1)), m = v - c;
        var (r, g, b) = (int)h switch
        {
            0 => (c, x, 0.0), 1 => (x, c, 0.0), 2 => (0.0, c, x),
            3 => (0.0, x, c), 4 => (x, 0.0, c), _ => (c, 0.0, x),
        };
        double k = Math.Clamp(Brightness, 0, 1) * 255;
        return Color.FromArgb((int)(Math.Clamp(alpha, 0, 1) * 255), (int)((r + m) * k), (int)((g + m) * k), (int)((b + m) * k));
    }

    protected readonly Random Rng = new();
    protected Size Size { get; private set; }
    protected bool IsPreview { get; private set; }
    protected double Brightness { get; private set; } = 0.6;

    /// <summary>Frames per second this scene needs to look right. The host may draw less often.</summary>
    public abstract int PreferredFps { get; }

    public void Configure(Size size, bool isPreview, double brightness)
    {
        Size = size;
        IsPreview = isPreview;
        Brightness = brightness;
        OnResize();
    }

    protected virtual void OnResize() { }

    /// <summary>Advance the animation by <paramref name="dt"/> seconds.</summary>
    public abstract void Update(double dt);

    public abstract void Render(Graphics g);

    /// <summary>Scale a color by the brightness setting.</summary>
    protected Color Dim(int r, int g, int b, double factor = 1.0)
    {
        double k = Math.Clamp(Brightness * factor, 0, 1);
        return Color.FromArgb((int)(r * k), (int)(g * k), (int)(b * k));
    }
}

public static class SceneFactory
{
    public static Scene Create(SaverSettings settings) => settings.Scene switch
    {
        "starfield" => new StarfieldScene(),
        "constellations" => new ConstellationScene(),
        "text" => new TextScene(settings),
        "text3d" => new Text3DScene(settings),
        "marquee" => new MarqueeScene(settings),
        "polygons" => new PolygonsScene(),
        "bubbles" => new BubblesScene(),
        "ribbons" => new RibbonsScene(),
        "pipes" => new PipesScene(),
        _ => new ClockScene(),
    };

    public static string DisplayName(string id) => id switch
    {
        "starfield" => "Starfield",
        "constellations" => "Constellations",
        "text" => "Custom text",
        "text3d" => "3D text",
        "marquee" => "Marquee (scrolling text)",
        "polygons" => "Polygon trails",
        "bubbles" => "Bubbles",
        "ribbons" => "Ribbons",
        "pipes" => "3D pipes",
        _ => "Drifting clock (lowest power)",
    };
}
