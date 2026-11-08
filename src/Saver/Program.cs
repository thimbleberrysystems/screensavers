using ThimbleBerry.Screensavers.Saver.Scenes;

namespace ThimbleBerry.Screensavers.Saver;

internal static class Program
{
    /// <summary>
    /// Windows starts a .scr with one of:
    ///   /s            run full screen
    ///   /p &lt;hwnd&gt;     draw inside the preview box in Screen Saver Settings
    ///   /c[:&lt;hwnd&gt;]   show the settings dialog (also used when there are no arguments)
    /// Flags may be upper or lower case and may use "-" instead of "/".
    /// </summary>
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var (mode, hwnd) = ParseArgs(args);

        return mode switch
        {
            'S' => RunScreensaver(),
            'P' => RunPreview(hwnd),
            _ => RunSettings(),
        };
    }

    private static (char Mode, IntPtr Hwnd) ParseArgs(string[] args)
    {
        if (args.Length == 0) return ('C', IntPtr.Zero);

        string first = args[0].Trim().TrimStart('/', '-');
        if (first.Length == 0) return ('C', IntPtr.Zero);
        char mode = char.ToUpperInvariant(first[0]);

        // The window handle comes either after a colon ("/p:1234") or as the next argument ("/p 1234").
        string? handleText = first.Length > 2 && first[1] == ':' ? first[2..] : args.Length > 1 ? args[1] : null;
        IntPtr hwnd = long.TryParse(handleText, out long h) ? new IntPtr(h) : IntPtr.Zero;
        return (mode, hwnd);
    }

    private static int RunScreensaver()
    {
        using var running = new Mutex(true, SaverInstance.RunningMutexName, out bool createdNew);
        if (!createdNew) return 0; // already showing (Windows and the idle watcher can both start us)

        PowerUtil.EnableEfficiencyMode();
        var settings = SaverSettings.Load();

        var forms = Screen.AllScreens
            .Select(s => new SaverForm(settings, SceneFactory.Create(settings), s.Bounds))
            .ToList();

        var context = new SaverContext(settings, forms);
        Application.Run(context);

        if (context.DismissedByUser && settings.LockOnExit)
            Native.LockWorkStation();
        return 0;
    }

    private static int RunPreview(IntPtr parent)
    {
        if (parent == IntPtr.Zero || !Native.IsWindow(parent)) return 0;
        PowerUtil.EnableEfficiencyMode();
        var settings = SaverSettings.Load();
        Application.Run(new SaverForm(settings, SceneFactory.Create(settings), Rectangle.Empty, parent));
        return 0;
    }

    private static int RunSettings()
    {
        Application.Run(new SettingsForm());
        return 0;
    }
}
