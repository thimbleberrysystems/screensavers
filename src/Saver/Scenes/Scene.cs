namespace ThimbleBerry.Screensavers.Saver.Scenes;

/// <summary>
/// One animated scene drawn into one window. Scenes are drawn with GDI+ on a timer;
/// nothing runs between frames, which is what keeps CPU and GPU use low.
/// </summary>
public abstract class Scene
{
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
        _ => new ClockScene(),
    };

    public static string DisplayName(string id) => id switch
    {
        "starfield" => "Starfield",
        "constellations" => "Constellations",
        "text" => "Custom text",
        "text3d" => "3D text",
        _ => "Drifting clock (lowest power)",
    };
}
