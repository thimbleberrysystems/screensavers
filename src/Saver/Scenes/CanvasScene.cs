using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ThimbleBerry.Screensavers.Saver.Scenes;

/// <summary>
/// Base for scenes that build up a picture over time (ribbons, pipes). New pieces are drawn
/// once into a kept image, so each frame only costs the new piece plus one copy to the screen.
/// </summary>
public abstract class CanvasScene : Scene
{
    private Bitmap? _canvas;
    protected Graphics? Canvas { get; private set; }

    protected override void OnResize()
    {
        DisposeCanvas();
        _canvas = new Bitmap(Math.Max(1, Size.Width), Math.Max(1, Size.Height), PixelFormat.Format32bppPArgb);
        Canvas = Graphics.FromImage(_canvas);
        Canvas.SmoothingMode = SmoothingMode.AntiAlias;
        ClearCanvas();
    }

    protected void ClearCanvas() => Canvas?.Clear(Color.Black);

    public override void Render(Graphics g)
    {
        if (_canvas is null) { g.Clear(Color.Black); return; }
        g.CompositingMode = CompositingMode.SourceCopy;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.DrawImageUnscaled(_canvas, 0, 0);
        g.CompositingMode = CompositingMode.SourceOver;
    }

    private void DisposeCanvas()
    {
        Canvas?.Dispose();
        _canvas?.Dispose();
        Canvas = null;
        _canvas = null;
    }

    public override void Dispose() => DisposeCanvas();
}
