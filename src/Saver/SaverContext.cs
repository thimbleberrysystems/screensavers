namespace ThimbleBerry.Screensavers.Saver;

/// <summary>
/// Runs the full-screen screensaver across every monitor and closes it on real input only.
/// Synthetic input from keep-awake tools (Caffeine's F15, mouse jigglers) is ignored, so the
/// screensaver stays up while those tools keep the PC awake.
/// </summary>
public sealed class SaverContext : ApplicationContext
{
    private static readonly TimeSpan Grace = TimeSpan.FromMilliseconds(800);

    private readonly List<SaverForm> _forms;
    private readonly SaverSettings _settings;
    private readonly InputMonitor _input;
    private readonly long _startTick = Environment.TickCount64;
    private readonly Point _startCursor = Cursor.Position;
    private bool _exiting;

    /// <summary>True when the saver closed because someone came back (not because Windows closed it).</summary>
    public bool DismissedByUser { get; private set; }

    public SaverContext(SaverSettings settings, List<SaverForm> forms)
    {
        _settings = settings;
        _forms = forms;

        foreach (var form in _forms)
        {
            form.FormClosed += (_, _) => Exit(byUser: false);
            form.Show();
        }
        _forms.FirstOrDefault()?.Activate();

        _input = new InputMonitor(settings) { SwallowRealInput = true };
        _input.RealInput += OnRealInput;

        if (!_input.IsActive)
        {
            // Hooks unavailable: fall back to ordinary window events (synthetic input can't be told apart here).
            foreach (var form in _forms)
            {
                form.KeyDown += (_, _) => OnRealInput(RealInputKind.Key, Point.Empty);
                form.MouseDown += (_, _) => OnRealInput(RealInputKind.MouseButton, Cursor.Position);
                form.MouseMove += (_, _) => OnRealInput(RealInputKind.MouseMove, Cursor.Position);
            }
        }
    }

    private void OnRealInput(RealInputKind kind, Point pt)
    {
        if (Environment.TickCount64 - _startTick < Grace.TotalMilliseconds) return;

        if (kind == RealInputKind.MouseMove)
        {
            int dx = pt.X - _startCursor.X, dy = pt.Y - _startCursor.Y;
            if (dx * dx + dy * dy < _settings.MouseMoveThreshold * _settings.MouseMoveThreshold) return;
        }

        // Called from inside a hook callback; finish the callback before tearing windows down.
        SynchronizationContext.Current?.Post(_ => Exit(byUser: true), null);
    }

    private void Exit(bool byUser)
    {
        if (_exiting) return;
        _exiting = true;
        DismissedByUser = byUser;
        _input.Dispose();
        foreach (var form in _forms) if (!form.IsDisposed) form.Close();
        ExitThread();
    }
}
