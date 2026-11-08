using System.Diagnostics;
using Microsoft.Win32;
using ThimbleBerry.Screensavers.Saver.Scenes;

namespace ThimbleBerry.Screensavers.Saver;

/// <summary>The settings dialog Windows opens from Screen Saver Settings → Settings.</summary>
public sealed class SettingsForm : Form
{
    private readonly SaverSettings _settings = SaverSettings.Load();

    private readonly ComboBox _scene = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly TrackBar _brightness = new() { Minimum = 10, Maximum = 100, TickFrequency = 10, Width = 260 };
    private readonly NumericUpDown _maxFps = new() { Minimum = 1, Maximum = 60, Width = 80 };
    private readonly CheckBox _reduceOnBattery = new() { Text = "Halve the frame rate on battery", AutoSize = true };
    private readonly NumericUpDown _idleMinutes = new() { Minimum = 1, Maximum = 240, Width = 80 };
    private readonly CheckBox _ignoreInjected = new() { Text = "Ignore simulated input (Caffeine, mouse jigglers, scripts)", AutoSize = true };
    private readonly CheckBox _ignoreF13 = new() { Text = "Ignore F13–F24 keys", AutoSize = true };
    private readonly NumericUpDown _mouseThreshold = new() { Minimum = 0, Maximum = 200, Width = 80 };
    private readonly CheckBox _lockOnExit = new() { Text = "Lock the PC when the screensaver closes", AutoSize = true };
    private readonly CheckBox _skipFullScreen = new() { Text = "Don't start over full-screen apps or presentations", AutoSize = true };

    public SettingsForm()
    {
        Text = "ThimbleBerry Screensaver Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);
        Font = SystemFonts.MessageBoxFont ?? Font;

        foreach (var id in SaverSettings.SceneIds) _scene.Items.Add(new SceneItem(id));

        var grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        void Row(string label, Control control)
        {
            grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 8, 12, 3) });
            grid.Controls.Add(control);
        }
        void Full(Control control)
        {
            grid.Controls.Add(control);
            grid.SetColumnSpan(control, 2);
        }
        void Heading(string text) => Full(new Label
        {
            Text = text, AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(3, 12, 3, 3),
        });

        Heading("Look");
        Row("Scene", _scene);
        Row("Brightness", _brightness);

        Heading("Power");
        Row("Max frames per second", _maxFps);
        Full(_reduceOnBattery);

        Heading("Starting and stopping");
        Row("Start after idle (minutes)", _idleMinutes);
        Full(_ignoreInjected);
        Full(_ignoreF13);
        Row("Mouse movement to close (px)", _mouseThreshold);
        Full(_skipFullScreen);
        Full(_lockOnExit);

        var preview = new Button { Text = "Save && Preview", AutoSize = true };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        preview.Click += (_, _) => { ApplyAndSave(); Process.Start(Environment.ProcessPath!, "/s"); };
        ok.Click += (_, _) => { ApplyAndSave(); Close(); };
        AcceptButton = ok;
        CancelButton = cancel;

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 0),
        };
        buttons.Controls.AddRange([cancel, ok, preview]);
        Full(buttons);

        Controls.Add(grid);
        LoadValues();
    }

    private void LoadValues()
    {
        _scene.SelectedIndex = Math.Max(0, Array.IndexOf(SaverSettings.SceneIds, _settings.Scene));
        _brightness.Value = (int)Math.Round(_settings.Brightness * 100);
        _maxFps.Value = _settings.MaxFps;
        _reduceOnBattery.Checked = _settings.ReduceOnBattery;
        _idleMinutes.Value = _settings.IdleMinutes;
        _ignoreInjected.Checked = _settings.IgnoreInjectedInput;
        _ignoreF13.Checked = _settings.IgnoreF13ToF24;
        _mouseThreshold.Value = _settings.MouseMoveThreshold;
        _lockOnExit.Checked = _settings.LockOnExit;
        _skipFullScreen.Checked = _settings.SkipWhenFullScreen;
    }

    private void ApplyAndSave()
    {
        _settings.Scene = ((SceneItem)_scene.SelectedItem!).Id;
        _settings.Brightness = _brightness.Value / 100.0;
        _settings.MaxFps = (int)_maxFps.Value;
        _settings.ReduceOnBattery = _reduceOnBattery.Checked;
        _settings.IdleMinutes = (int)_idleMinutes.Value;
        _settings.IgnoreInjectedInput = _ignoreInjected.Checked;
        _settings.IgnoreF13ToF24 = _ignoreF13.Checked;
        _settings.MouseMoveThreshold = (int)_mouseThreshold.Value;
        _settings.LockOnExit = _lockOnExit.Checked;
        _settings.SkipWhenFullScreen = _skipFullScreen.Checked;
        _settings.Save();
        SyncWindowsTimeout();
    }

    /// <summary>If Windows is set to use this screensaver, keep its own timeout in step with ours.</summary>
    private void SyncWindowsTimeout()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            var current = key?.GetValue("SCRNSAVE.EXE") as string;
            if (current is null || !current.EndsWith(SaverInstance.SaverFileName, StringComparison.OrdinalIgnoreCase)) return;
            Native.SystemParametersInfo(Native.SPI_SETSCREENSAVETIMEOUT, (uint)(_settings.IdleMinutes * 60), IntPtr.Zero,
                Native.SPIF_UPDATEINIFILE | Native.SPIF_SENDCHANGE);
        }
        catch
        {
            // Not essential.
        }
    }

    private sealed record SceneItem(string Id)
    {
        public override string ToString() => SceneFactory.DisplayName(Id);
    }
}
