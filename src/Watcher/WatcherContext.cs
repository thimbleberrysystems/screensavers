using System.Diagnostics;
using System.Drawing.Drawing2D;
using Microsoft.Win32;

namespace ThimbleBerry.Screensavers.Watcher;

/// <summary>
/// Tray app that starts the screensaver after a period with no real (human) input.
///
/// Why this exists: keep-awake tools stop Windows from starting the screensaver on its own.
/// Caffeine presses F15 every minute, which resets the Windows idle timer, and tools such as
/// PowerToys Awake hold a power request. This watcher counts only physical input, so the
/// screensaver still starts while those tools keep the PC and display awake.
/// </summary>
public sealed class WatcherContext : ApplicationContext
{
    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(5);

    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly InputMonitor _input;
    private readonly ToolStripMenuItem _pauseItem;
    private SaverSettings _settings = SaverSettings.Load();
    private DateTime _settingsWriteTime = GetSettingsWriteTime();
    private bool _sessionLocked;
    private DateTime? _pausedUntil;
    private long _launchedForInputTick = -1;

    public WatcherContext()
    {
        _input = new InputMonitor(_settings);

        var menu = new ContextMenuStrip();
        menu.Items.Add("Start screensaver now", null, (_, _) => Launch());
        _pauseItem = new ToolStripMenuItem("Pause for 1 hour", null, (_, _) => TogglePause());
        menu.Items.Add(_pauseItem);
        menu.Items.Add("Settings…", null, (_, _) => StartSaver("/c"));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _tray = new NotifyIcon
        {
            Icon = CreateIcon(),
            Text = "ThimbleBerry Screensaver",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.DoubleClick += (_, _) => Launch();

        SystemEvents.SessionSwitch += OnSessionSwitch;

        _timer.Interval = (int)CheckEvery.TotalMilliseconds;
        _timer.Tick += (_, _) => Check();
        _timer.Start();
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
            case SessionSwitchReason.ConsoleDisconnect:
            case SessionSwitchReason.RemoteDisconnect:
                _sessionLocked = true;
                break;
            case SessionSwitchReason.SessionUnlock:
            case SessionSwitchReason.ConsoleConnect:
            case SessionSwitchReason.RemoteConnect:
                _sessionLocked = false;
                break;
        }
    }

    private void Check()
    {
        ReloadSettingsIfChanged();

        if (_pausedUntil is { } until)
        {
            if (DateTime.Now < until) return;
            _pausedUntil = null;
            _pauseItem.Checked = false;
        }
        if (_sessionLocked || SaverInstance.IsSaverRunning()) return;

        long lastReal = _input.LastRealInputTick;
        long idleMs = Environment.TickCount64 - lastReal;

        // Low-level hooks can't see input sent to elevated (admin) windows. When one is in
        // front, also trust Windows' own idle timer so we never start over someone typing.
        if (!_input.IsActive || PowerUtil.ForegroundIsElevatedOrInaccessible())
            idleMs = Math.Min(idleMs, PowerUtil.SystemIdleMilliseconds());

        if (idleMs < _settings.IdleMinutes * 60_000L) return;
        if (_launchedForInputTick == lastReal) return; // already started once for this idle period

        if (_settings.SkipWhenFullScreen && IsFullScreenAppInFront()) return;

        _launchedForInputTick = lastReal;
        Launch();
    }

    private static bool IsFullScreenAppInFront()
    {
        if (Native.SHQueryUserNotificationState(out var state) != 0) return false;
        return state is Native.UserNotificationState.Busy
            or Native.UserNotificationState.RunningD3DFullScreen
            or Native.UserNotificationState.PresentationMode;
    }

    private void Launch()
    {
        if (!SaverInstance.IsSaverRunning()) StartSaver("/s");
    }

    private void StartSaver(string args)
    {
        var path = FindSaver();
        if (path is null)
        {
            _tray.ShowBalloonTip(5000, "ThimbleBerry Screensaver",
                $"Couldn't find {SaverInstance.SaverFileName}. Reinstall with scripts\\install.ps1.", ToolTipIcon.Warning);
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(path, args) { UseShellExecute = false });
        }
        catch (Exception ex)
        {
            _tray.ShowBalloonTip(5000, "ThimbleBerry Screensaver", ex.Message, ToolTipIcon.Error);
        }
    }

    /// <summary>Looks next to this exe first, then at whatever Windows is configured to use.</summary>
    private static string? FindSaver()
    {
        string dir = AppContext.BaseDirectory;
        foreach (var name in new[] { SaverInstance.SaverFileName, "ThimbleBerry.exe" })
        {
            var candidate = Path.Combine(dir, name);
            if (File.Exists(candidate)) return candidate;
        }
        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
        return key?.GetValue("SCRNSAVE.EXE") is string configured
               && configured.EndsWith(SaverInstance.SaverFileName, StringComparison.OrdinalIgnoreCase)
               && File.Exists(configured)
            ? configured
            : null;
    }

    private void TogglePause()
    {
        _pausedUntil = _pausedUntil is null ? DateTime.Now.AddHours(1) : null;
        _pauseItem.Checked = _pausedUntil is not null;
    }

    private void ReloadSettingsIfChanged()
    {
        var stamp = GetSettingsWriteTime();
        if (stamp == _settingsWriteTime) return;
        _settingsWriteTime = stamp;
        _settings = SaverSettings.Load();
        _input.Settings = _settings;
    }

    private static DateTime GetSettingsWriteTime() =>
        File.Exists(SaverSettings.SettingsPath) ? File.GetLastWriteTimeUtc(SaverSettings.SettingsPath) : DateTime.MinValue;

    /// <summary>A small berry drawn at runtime, so there's no binary icon file to maintain.</summary>
    private static Icon CreateIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var berry = new SolidBrush(Color.FromArgb(196, 40, 90));
            using var shine = new SolidBrush(Color.FromArgb(240, 150, 185));
            using var leaf = new SolidBrush(Color.FromArgb(70, 150, 80));
            g.FillEllipse(berry, 4, 7, 24, 23);
            g.FillEllipse(shine, 10, 12, 6, 6);
            g.FillEllipse(leaf, 11, 1, 10, 8);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    protected override void ExitThreadCore()
    {
        _timer.Stop();
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        _input.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        base.ExitThreadCore();
    }
}
