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
    private readonly System.Windows.Forms.Timer _keepInFront = new();
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
        KeepInFront();

        // Windows only hides the taskbar for a full-screen window that is in front. If the
        // saver was started in the background (by the idle watcher) or something else takes
        // the front later, put it back. Once a second is plenty and costs next to nothing.
        _keepInFront.Interval = 1000;
        _keepInFront.Tick += (_, _) => KeepInFront();
        _keepInFront.Start();

        // Development/testing only: THIMBLEBERRY_TEST_SECONDS=N ignores input and closes the
        // saver by itself after N seconds, so automated checks don't depend on nobody touching
        // the PC. Not set in normal use.
        if (int.TryParse(Environment.GetEnvironmentVariable("THIMBLEBERRY_TEST_SECONDS"), out int testSeconds) && testSeconds > 0)
        {
            _input = new InputMonitor(settings);
            var stop = new System.Windows.Forms.Timer { Interval = Math.Min(testSeconds, 60) * 1000 };
            stop.Tick += (_, _) => { stop.Stop(); Exit(byUser: false); };
            stop.Start();
            return;
        }

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

    private void KeepInFront()
    {
        if (_exiting || _forms.Count == 0) return;
        var foreground = Native.GetForegroundWindow();
        bool oursInFront = _forms.Any(f => f.IsHandleCreated && f.Handle == foreground);

        // Re-assert "always on top" for every monitor's window (cheap, no repaint), and
        // take the foreground back if another window has it.
        foreach (var form in _forms)
        {
            if (!form.IsHandleCreated) continue;
            Native.SetWindowPos(form.Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
                Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        }
        if (!oursInFront && _forms[0].IsHandleCreated) Native.ForceForeground(_forms[0].Handle);
    }

    private void Exit(bool byUser)
    {
        if (_exiting) return;
        _exiting = true;
        _keepInFront.Stop();
        _keepInFront.Dispose();
        DismissedByUser = byUser;
        _input.Dispose();
        foreach (var form in _forms) if (!form.IsDisposed) form.Close();
        ExitThread();
    }
}
