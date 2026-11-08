using System.Diagnostics;
using ThimbleBerry.Screensavers.Saver.Scenes;

namespace ThimbleBerry.Screensavers.Saver;

/// <summary>The settings dialog Windows opens from Screen Saver Settings → Settings.</summary>
public sealed class SettingsForm : Form
{
    private readonly SaverSettings _settings = SaverSettings.Load();

    private readonly ComboBox _scene = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly TrackBar _brightness = new() { Minimum = 10, Maximum = 100, TickFrequency = 10, Width = 260 };
    private readonly TextBox _customText = new()
    {
        Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical, Width = 260, Height = 70, MaxLength = 500,
    };
    private readonly Button _customColor = new() { Text = "Choose…", AutoSize = true };
    private readonly NumericUpDown _customSize = new() { Minimum = 3, Maximum = 40, Width = 80 };
    private readonly ComboBox _motion = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly NumericUpDown _speed = new() { Minimum = 1, Maximum = 10, Width = 80 };
    private Color _customColorValue;
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

        Heading("Text scenes (custom text, 3D text, marquee)");
        Row("Text", _customText);
        Row("Colour", _customColor);
        Row("Size (% of screen height)", _customSize);
        Row("3D motion", _motion);
        Row("Speed (1–10)", _speed);
        _motion.Items.AddRange(["Spin", "Seesaw", "Wobble", "Tumble"]);
        _customColor.Click += (_, _) =>
        {
            using var dlg = new ColorDialog { Color = _customColorValue, FullOpen = true };
            if (dlg.ShowDialog(this) == DialogResult.OK) SetCustomColor(dlg.Color);
        };
        _scene.SelectedIndexChanged += (_, _) => UpdateCustomTextEnabled();

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
        _customText.Text = _settings.CustomText.ReplaceLineEndings("\r\n");
        _customSize.Value = _settings.CustomTextSize;
        _motion.SelectedIndex = Math.Max(0, Array.IndexOf(SaverSettings.Text3DMotions, _settings.Text3DMotion));
        _speed.Value = _settings.Text3DSpeed;
        SetCustomColor(SaverSettings.ParseColor(_settings.CustomTextColor));
        UpdateCustomTextEnabled();
    }

    private void SetCustomColor(Color color)
    {
        _customColorValue = color;
        _customColor.BackColor = color;
        _customColor.ForeColor = color.GetBrightness() > 0.5f ? Color.Black : Color.White;
        _customColor.Text = ColorTranslator.ToHtml(color);
    }

    private void UpdateCustomTextEnabled()
    {
        var id = (_scene.SelectedItem as SceneItem)?.Id;
        bool text = id is "text" or "text3d" or "marquee";
        _customText.Enabled = _customColor.Enabled = _customSize.Enabled = text;
        _motion.Enabled = id == "text3d";
        _speed.Enabled = id is "text3d" or "marquee";
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
        _settings.CustomText = _customText.Text.ReplaceLineEndings("\n");
        _settings.CustomTextColor = ColorTranslator.ToHtml(_customColorValue);
        _settings.CustomTextSize = (int)_customSize.Value;
        _settings.Text3DMotion = SaverSettings.Text3DMotions[Math.Max(0, _motion.SelectedIndex)];
        _settings.Text3DSpeed = (int)_speed.Value;
        _settings.Save();
        WindowsScreensaver.SyncTimeout(_settings.IdleMinutes);
    }

    private sealed record SceneItem(string Id)
    {
        public override string ToString() => SceneFactory.DisplayName(Id);
    }
}
