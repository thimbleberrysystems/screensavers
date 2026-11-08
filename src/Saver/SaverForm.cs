using System.Diagnostics;
using System.Runtime.InteropServices;
using ThimbleBerry.Screensavers.Saver.Scenes;

namespace ThimbleBerry.Screensavers.Saver;

/// <summary>
/// A borderless window that hosts one scene: full screen on one monitor, or as a child of
/// the small preview box in Windows' Screen Saver Settings.
/// </summary>
public sealed class SaverForm : Form
{
    private readonly SaverSettings _settings;
    private readonly Scene _scene;
    private readonly Rectangle _screenBounds;
    private readonly IntPtr _previewParent;
    private readonly System.Windows.Forms.Timer _timer = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan _lastFrame;
    private IntPtr _powerNotification;
    private bool _displayOff;

    public bool IsPreview => _previewParent != IntPtr.Zero;

    public SaverForm(SaverSettings settings, Scene scene, Rectangle screenBounds, IntPtr previewParent = default)
    {
        _settings = settings;
        _scene = scene;
        _screenBounds = screenBounds;
        _previewParent = previewParent;

        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        BackColor = Color.Black;
        Text = "ThimbleBerry Screensaver";
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.Opaque, true);

        if (IsPreview)
        {
            Native.GetClientRect(_previewParent, out var r);
            Bounds = new Rectangle(0, 0, r.Right - r.Left, r.Bottom - r.Top);
        }
        else
        {
            Bounds = _screenBounds;
            TopMost = true;
        }

        _timer.Tick += (_, _) => Frame();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            if (_previewParent != IntPtr.Zero)
            {
                // Live inside the Screen Saver Settings preview box.
                cp.Parent = _previewParent;
                cp.Style = Native.WS_CHILD | 0x10000000 /* WS_VISIBLE */;
            }
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        var guid = Native.GUID_CONSOLE_DISPLAY_STATE;
        _powerNotification = Native.RegisterPowerSettingNotification(Handle, ref guid, 0 /* DEVICE_NOTIFY_WINDOW_HANDLE */);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (!IsPreview)
        {
            Bounds = _screenBounds; // re-apply after any per-monitor DPI adjustment
            Cursor.Hide();
        }
        _scene.Configure(ClientSize, IsPreview, _settings.Brightness);
        _lastFrame = _clock.Elapsed;
        UpdateInterval();
        _timer.Start();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_timer.Enabled && ClientSize.Width > 0 && ClientSize.Height > 0)
            _scene.Configure(ClientSize, IsPreview, _settings.Brightness);
    }

    private void UpdateInterval()
    {
        int fps = Math.Min(_scene.PreferredFps, _settings.MaxFps);
        if (_settings.ReduceOnBattery && PowerUtil.OnBattery) fps = Math.Max(1, fps / 2);
        if (IsPreview) fps = Math.Min(fps, 10);
        int interval = 1000 / Math.Max(1, fps);
        if (_timer.Interval != interval) _timer.Interval = interval;
    }

    private void Frame()
    {
        if (IsPreview && !Native.IsWindow(_previewParent))
        {
            Close();
            return;
        }

        var now = _clock.Elapsed;
        double dt = Math.Min(1.0, (now - _lastFrame).TotalSeconds);
        _lastFrame = now;
        UpdateInterval();

        // Don't spend power drawing to a screen that is switched off.
        if (_displayOff) return;

        _scene.Update(dt);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e) => _scene.Render(e.Graphics);

    protected override void OnPaintBackground(PaintEventArgs e) { /* scene clears the frame itself */ }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == Native.WM_POWERBROADCAST && (int)m.WParam == Native.PBT_POWERSETTINGCHANGE && m.LParam != IntPtr.Zero)
        {
            var setting = Marshal.PtrToStructure<Native.POWERBROADCAST_SETTING>(m.LParam);
            if (setting.PowerSetting == Native.GUID_CONSOLE_DISPLAY_STATE)
            {
                bool wasOff = _displayOff;
                _displayOff = setting.Data == 0;
                if (wasOff && !_displayOff) _lastFrame = _clock.Elapsed; // avoid a big jump on wake
            }
        }
        base.WndProc(ref m);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();
        if (_powerNotification != IntPtr.Zero) Native.UnregisterPowerSettingNotification(_powerNotification);
        if (!IsPreview) Cursor.Show();
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _scene.Dispose();
        }
        base.Dispose(disposing);
    }
}
