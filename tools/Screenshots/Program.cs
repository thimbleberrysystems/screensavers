using System.Drawing.Imaging;
using ThimbleBerry.Screensavers;
using ThimbleBerry.Screensavers.Saver;
using ThimbleBerry.Screensavers.Saver.Scenes;

// Renders every scene (and the settings dialog) to PNGs for the README.
// Usage: dotnet run --project tools/Screenshots -- [output-dir]
// Scenes are simulated off-screen; nothing is shown full screen.

var outDir = args.Length > 0 ? args[0] : Path.Combine("docs", "images");
Directory.CreateDirectory(outDir);
var size = new Size(1280, 720);
const double brightness = 0.85; // a little brighter than the 0.6 default so details show on GitHub

// Seconds of animation to simulate before capturing, so build-up scenes have content.
var warmUp = new Dictionary<string, double>
{
    ["ribbons"] = 22, ["pipes"] = 28, ["polygons"] = 4, ["bubbles"] = 3, ["starfield"] = 6,
    ["constellations"] = 4, ["text3d"] = 2.2, ["marquee"] = 3.5,
};

foreach (var id in SaverSettings.SceneIds)
{
    var settings = new SaverSettings
    {
        Scene = id,
        CustomText = id == "text" ? "ThimbleBerry Systems\nBack in 10 minutes" : "ThimbleBerry",
        CustomTextColor = id == "text" ? "#E6E6EB" : "#E05A8C",
        CustomTextSize = id == "text3d" ? 22 : 12,
        Text3DMotion = "wobble",
    }.Normalize();

    using var scene = SceneFactory.Create(settings);
    scene.Configure(size, false, brightness);
    using var bmp = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);

    double dt = 1.0 / Math.Max(1, scene.PreferredFps);
    int frames = (int)(warmUp.GetValueOrDefault(id, 1) / dt);
    for (int i = 0; i < Math.Max(1, frames); i++) { scene.Update(dt); scene.Render(g); }
    scene.Render(g);

    var file = Path.Combine(outDir, $"scene-{id}.png");
    bmp.Save(file, ImageFormat.Png);
    Console.WriteLine(file);
}

// Settings dialog, captured off-screen with neutral example settings (never this PC's
// saved settings). Windows Forms needs an STA thread.
var ui = new Thread(() =>
{
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);
    using var form = new SettingsForm(new SaverSettings
    {
        Scene = "text3d",
        CustomText = "ThimbleBerry",
        CustomTextColor = "#E05A8C",
        Text3DMotion = "wobble",
    }.Normalize());
    form.StartPosition = FormStartPosition.Manual;
    form.Location = new Point(-20000, -20000);
    form.ShowInTaskbar = false;
    form.Show();
    Application.DoEvents();
    using var bmp = new Bitmap(form.Width, form.Height);
    form.DrawToBitmap(bmp, new Rectangle(Point.Empty, form.Size));
    var file = Path.Combine(outDir, "settings.png");
    bmp.Save(file, ImageFormat.Png);
    Console.WriteLine(file);
    form.Close();
});
ui.SetApartmentState(ApartmentState.STA);
ui.Start();
ui.Join();
