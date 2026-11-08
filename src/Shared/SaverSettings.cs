using System.Text.Json;
using System.Text.Json.Serialization;

namespace ThimbleBerry.Screensavers;

/// <summary>Settings shared by the screensaver and the idle watcher, stored as JSON in %APPDATA%.</summary>
public sealed class SaverSettings
{
    public static readonly string[] SceneIds =
        ["clock", "text", "text3d", "marquee", "starfield", "constellations", "polygons", "bubbles", "ribbons", "pipes"];
    public static readonly string[] Text3DMotions = ["spin", "seesaw", "wobble", "tumble"];

    /// <summary>Which scene to show; one of <see cref="SceneIds"/>.</summary>
    public string Scene { get; set; } = "ribbons";

    /// <summary>Message for the custom text scene. Line breaks are kept; long lines wrap.</summary>
    public string CustomText { get; set; } = "ThimbleBerry";

    /// <summary>Colour of the custom text, as #RRGGBB.</summary>
    public string CustomTextColor { get; set; } = "#E6E6EB";

    /// <summary>Height of the custom text as a percentage of the screen height (shrinks to fit if needed).</summary>
    public int CustomTextSize { get; set; } = 12;

    /// <summary>How the 3D text moves: spin, seesaw, wobble or tumble.</summary>
    public string Text3DMotion { get; set; } = "spin";

    /// <summary>3D text rotation and marquee scroll speed, 1 (slow) to 10 (fast).</summary>
    public int Text3DSpeed { get; set; } = 5;

    /// <summary>Move the 3D text around the screen, bouncing off the edges (off: a slow drift).</summary>
    public bool Text3DBounce { get; set; } = true;

    /// <summary>Upper limit on frames per second. Each scene draws at its own, usually lower, rate.</summary>
    public int MaxFps { get; set; } = 30;

    /// <summary>Halve the frame rate when running on battery.</summary>
    public bool ReduceOnBattery { get; set; } = true;

    /// <summary>Overall brightness, 0.1 to 1.0. Lower is kinder to OLED panels and uses less power on them.</summary>
    public double Brightness { get; set; } = 0.6;

    /// <summary>Minutes of real (human) inactivity before the idle watcher starts the screensaver.</summary>
    public int IdleMinutes { get; set; } = 10;

    /// <summary>Ignore synthetic input from keep-awake tools (Caffeine, mouse jigglers, scripts).</summary>
    public bool IgnoreInjectedInput { get; set; } = true;

    /// <summary>Ignore F13–F24 even when they look physical. Keep-awake tools commonly press F15.</summary>
    public bool IgnoreF13ToF24 { get; set; } = true;

    /// <summary>Pixels the mouse must move before the screensaver closes.</summary>
    public int MouseMoveThreshold { get; set; } = 12;

    /// <summary>Lock the PC when the screensaver is dismissed.</summary>
    public bool LockOnExit { get; set; } = false;

    /// <summary>Don't start the screensaver while a full-screen app, game or presentation is in front.</summary>
    public bool SkipWhenFullScreen { get; set; } = true;

    [JsonIgnore]
    public static string SettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ThimbleBerry", "Screensavers");

    [JsonIgnore]
    public static string SettingsPath => Path.Combine(SettingsDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static SaverSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var loaded = JsonSerializer.Deserialize<SaverSettings>(File.ReadAllText(SettingsPath), JsonOptions);
                if (loaded is not null) return loaded.Normalize();
            }
        }
        catch
        {
            // A corrupt settings file should never stop the screensaver from running.
        }
        return new SaverSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(Normalize(), JsonOptions));
    }

    public SaverSettings Normalize()
    {
        if (!SceneIds.Contains(Scene)) Scene = "ribbons";
        MaxFps = Math.Clamp(MaxFps, 1, 60);
        Brightness = Math.Clamp(Brightness, 0.1, 1.0);
        IdleMinutes = Math.Clamp(IdleMinutes, 1, 240);
        MouseMoveThreshold = Math.Clamp(MouseMoveThreshold, 0, 200);
        CustomText ??= "";
        if (CustomText.Length > 500) CustomText = CustomText[..500];
        CustomTextColor = ColorTranslator.ToHtml(ParseColor(CustomTextColor));
        CustomTextSize = Math.Clamp(CustomTextSize, 3, 40);
        if (!Text3DMotions.Contains(Text3DMotion)) Text3DMotion = "spin";
        Text3DSpeed = Math.Clamp(Text3DSpeed, 1, 10);
        return this;
    }

    /// <summary>Reads #RRGGBB or a colour name; falls back to soft white.</summary>
    public static Color ParseColor(string? value)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                var c = ColorTranslator.FromHtml(value.Trim());
                return Color.FromArgb(c.R, c.G, c.B);
            }
        }
        catch
        {
            // fall through
        }
        return Color.FromArgb(0xE6, 0xE6, 0xEB);
    }
}
