using System.Drawing.Drawing2D;

namespace ThimbleBerry.Screensavers.Saver.Scenes;

/// <summary>
/// Your custom text as solid, extruded 3D letters that rotate (spin, seesaw, wobble or tumble)
/// while bouncing around the screen (or, with bouncing off, drifting slowly).
///
/// Rendering is plain GDI+ with a small hand-written 3D pipeline: the text outline is
/// flattened into polygons once, then each frame the front face, back face and side walls
/// are rotated, projected and painted far-to-near with flat shading. No GPU 3D engine is
/// started, which keeps power use modest; the frame rate is capped like every other scene.
/// </summary>
public sealed class Text3DScene : Scene
{
    private const int ShadeLevels = 32;
    private static readonly (double X, double Y, double Z) Light = Normalize(-0.35, -0.55, -0.76);

    private struct SideFace
    {
        public int Contour;
        public int Index;
        public float Depth;
        public float Shade;
    }

    private readonly string _text;
    private readonly Color _color;
    private readonly double _sizePercent;
    private readonly string _motion;
    private readonly double _speed;
    private readonly bool _bounce;
    private readonly Drifter _drift = new();

    // Model: one closed polygon per glyph contour, centred on the origin, in pixels.
    private PointF[][] _contours = [];
    private (double X, double Y)[][] _edgeNormals = [];
    private double _depth;
    private double _cameraDistance;
    private SizeF _block;

    // Per-frame buffers, reused to avoid allocations.
    private PointF[][] _projFront = [];
    private PointF[][] _projBack = [];
    private float[][] _zFront = [];
    private float[][] _zBack = [];
    private SideFace[] _faces = [];
    private readonly PointF[] _quad = new PointF[4];
    private SolidBrush[] _sideBrushes = [];
    private SolidBrush[] _capBrushes = [];
    private double _time;

    public Text3DScene(SaverSettings settings)
    {
        _text = string.IsNullOrWhiteSpace(settings.CustomText) ? "ThimbleBerry" : settings.CustomText.Replace("\r\n", "\n").Trim();
        _color = SaverSettings.ParseColor(settings.CustomTextColor);
        _sizePercent = settings.CustomTextSize;
        _motion = settings.Text3DMotion;
        _speed = settings.Text3DSpeed / 5.0;
        _bounce = settings.Text3DBounce;
    }

    // 20 fps even when bouncing: 30 fps more than doubled CPU use in testing.
    public override int PreferredFps => 20;

    protected override void OnResize()
    {
        BuildModel();
        BuildBrushes();
        // Bouncing: about 90 px/s on a 1080p screen at the default speed (~4.5 px per frame).
        _drift.Reset(Size, _block, Rng, _bounce ? (float)(Size.Height / 12.0 * _speed) : null);
    }

    private void BuildModel()
    {
        using var path = new GraphicsPath(FillMode.Alternate);
        using var family = new FontFamily("Segoe UI");
        using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        const float em = 100f;
        path.AddString(_text, family, (int)FontStyle.Bold, em, PointF.Empty, format);

        var bounds = path.GetBounds();
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            _contours = [];
            _block = new SizeF(1, 1);
            return;
        }

        // Size: the em height is the chosen % of the screen height, then shrink to fit.
        // Rotation and perspective make the text appear up to ~30% bigger, so leave room.
        float scale = (float)(Size.Height * _sizePercent / 100.0 / em);
        bool tumble = _motion == "tumble";
        float diagonal = MathF.Sqrt(bounds.Width * bounds.Width + bounds.Height * bounds.Height);
        if (tumble)
        {
            scale = Math.Min(scale, Size.Height * 0.7f / diagonal);
        }
        else
        {
            scale = Math.Min(scale, Size.Width * 0.7f / bounds.Width);
            scale = Math.Min(scale, Size.Height * 0.55f / bounds.Height);
        }

        using var m = new Matrix();
        m.Scale(scale, scale);
        m.Translate(-(bounds.X + bounds.Width / 2), -(bounds.Y + bounds.Height / 2));
        path.Flatten(m, 0.6f);

        _contours = SplitContours(path.PathPoints, path.PathTypes, minSegment: Math.Max(1.5f, em * scale * 0.012f));
        _edgeNormals = _contours.Select(EdgeNormals).ToArray();
        _depth = em * scale * 0.22;

        float w = bounds.Width * scale, h = bounds.Height * scale;
        float extent = tumble ? diagonal * scale : Math.Max(w, h);
        _cameraDistance = extent * 2.4 + _depth;
        // Room for rotation and perspective. Wide text that rolls or tilts sweeps up and down
        // by a fraction of its width, so the height allowance grows with the width.
        _block = tumble ? new SizeF(extent * 1.3f, extent * 1.3f) : new SizeF(w * 1.3f, h * 1.5f + w * 0.35f);
        _block = new SizeF(Math.Min(_block.Width, Size.Width), Math.Min(_block.Height, Size.Height));

        _projFront = _contours.Select(c => new PointF[c.Length]).ToArray();
        _projBack = _contours.Select(c => new PointF[c.Length]).ToArray();
        _zFront = _contours.Select(c => new float[c.Length]).ToArray();
        _zBack = _contours.Select(c => new float[c.Length]).ToArray();
        _faces = new SideFace[_contours.Sum(c => c.Length)];
    }

    /// <summary>Splits a flattened path into closed polygons, dropping points too close together.</summary>
    private static PointF[][] SplitContours(PointF[] points, byte[] types, float minSegment)
    {
        var result = new List<PointF[]>();
        var current = new List<PointF>();
        float min2 = minSegment * minSegment;

        void Flush()
        {
            if (current.Count >= 3)
            {
                // Also drop a closing point that duplicates the first.
                var last = current[^1];
                float dx = last.X - current[0].X, dy = last.Y - current[0].Y;
                if (dx * dx + dy * dy < min2) current.RemoveAt(current.Count - 1);
                if (current.Count >= 3) result.Add(current.ToArray());
            }
            current.Clear();
        }

        for (int i = 0; i < points.Length; i++)
        {
            bool start = (types[i] & 0x07) == 0;
            if (start) Flush();
            var p = points[i];
            if (current.Count == 0)
            {
                current.Add(p);
            }
            else
            {
                var q = current[^1];
                float dx = p.X - q.X, dy = p.Y - q.Y;
                if (dx * dx + dy * dy >= min2) current.Add(p);
            }
        }
        Flush();
        return result.ToArray();
    }

    private static (double X, double Y)[] EdgeNormals(PointF[] c)
    {
        var normals = new (double, double)[c.Length];
        for (int i = 0; i < c.Length; i++)
        {
            var a = c[i];
            var b = c[(i + 1) % c.Length];
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len = Math.Sqrt(dx * dx + dy * dy);
            normals[i] = len > 0 ? (dy / len, -dx / len) : (0, 0);
        }
        return normals;
    }

    private void BuildBrushes()
    {
        foreach (var b in _sideBrushes) b.Dispose();
        foreach (var b in _capBrushes) b.Dispose();
        _sideBrushes = new SolidBrush[ShadeLevels];
        _capBrushes = new SolidBrush[ShadeLevels];
        for (int i = 0; i < ShadeLevels; i++)
        {
            double s = i / (double)(ShadeLevels - 1);
            _sideBrushes[i] = new SolidBrush(Dim(_color.R, _color.G, _color.B, 0.18 + 0.55 * s));
            _capBrushes[i] = new SolidBrush(Dim(_color.R, _color.G, _color.B, 0.45 + 0.55 * s));
        }
    }

    public override void Update(double dt)
    {
        _time += dt * _speed;
        _drift.Update(dt);
    }

    private (double Yaw, double Pitch, double Roll) Angles()
    {
        double t = _time;
        return _motion switch
        {
            "seesaw" => (Math.Sin(t * 0.7) * 0.9, Math.Sin(t * 0.4) * 0.1, 0),
            "wobble" => (Math.Sin(t * 0.6) * 0.6, Math.Sin(t * 0.83) * 0.35, Math.Sin(t * 0.37) * 0.15),
            "tumble" => (t * 0.55, t * 0.41, t * 0.23),
            _ => (t * 0.8, 0.15, 0), // spin
        };
    }

    public override void Render(Graphics g)
    {
        g.Clear(Color.Black);
        if (_contours.Length == 0) return;

        var (yaw, pitch, roll) = Angles();
        var r = RotationMatrix(yaw, pitch, roll);
        var pos = _drift.Position;
        float cx = pos.X + _block.Width / 2, cy = pos.Y + _block.Height / 2;
        double half = _depth / 2, cam = _cameraDistance;

        // Rotate and project every vertex of the front (towards viewer) and back faces.
        for (int c = 0; c < _contours.Length; c++)
        {
            var poly = _contours[c];
            for (int i = 0; i < poly.Length; i++)
            {
                Project(r, poly[i].X, poly[i].Y, -half, cx, cy, cam, out _projFront[c][i], out _zFront[c][i]);
                Project(r, poly[i].X, poly[i].Y, half, cx, cy, cam, out _projBack[c][i], out _zBack[c][i]);
            }
        }

        // Side walls: one quad per outline edge, shaded by the angle to the light.
        int n = 0;
        for (int c = 0; c < _contours.Length; c++)
        {
            int len = _contours[c].Length;
            for (int i = 0; i < len; i++)
            {
                int j = (i + 1) % len;
                var (nx, ny) = _edgeNormals[c][i];
                var (rx, ry, rz) = Rotate(r, nx, ny, 0);
                _faces[n++] = new SideFace
                {
                    Contour = c,
                    Index = i,
                    Depth = _zFront[c][i] + _zFront[c][j] + _zBack[c][i] + _zBack[c][j],
                    Shade = (float)Math.Abs(rx * Light.X + ry * Light.Y + rz * Light.Z),
                };
            }
        }
        Array.Sort(_faces, 0, n, FarthestFirst.Instance);

        // The face pointing at the viewer is drawn last, the one pointing away first.
        var (capX, capY, capZ) = Rotate(r, 0, 0, -1);
        bool frontVisible = capZ < 0;
        int capShade = ShadeIndex(Math.Abs(capX * Light.X + capY * Light.Y + capZ * Light.Z));

        g.SmoothingMode = SmoothingMode.HighSpeed; // no anti-aliasing, so walls have no seams
        FillCap(g, frontVisible ? _projBack : _projFront, _capBrushes[capShade / 2]);

        for (int k = 0; k < n; k++)
        {
            var f = _faces[k];
            int j = (f.Index + 1) % _contours[f.Contour].Length;
            _quad[0] = _projFront[f.Contour][f.Index];
            _quad[1] = _projFront[f.Contour][j];
            _quad[2] = _projBack[f.Contour][j];
            _quad[3] = _projBack[f.Contour][f.Index];
            g.FillPolygon(_sideBrushes[ShadeIndex(f.Shade)], _quad);
        }

        g.SmoothingMode = SmoothingMode.AntiAlias; // smooth letter outlines on the visible face
        FillCap(g, frontVisible ? _projFront : _projBack, _capBrushes[capShade]);
    }

    private static void FillCap(Graphics g, PointF[][] contours, Brush brush)
    {
        using var path = new GraphicsPath(FillMode.Alternate);
        foreach (var poly in contours) path.AddPolygon(poly);
        g.FillPath(brush, path);
    }

    private static int ShadeIndex(double shade) => Math.Clamp((int)(shade * (ShadeLevels - 1)), 0, ShadeLevels - 1);

    private static void Project(double[] r, double x, double y, double z, float cx, float cy, double cam,
        out PointF screen, out float depth)
    {
        var (rx, ry, rz) = Rotate(r, x, y, z);
        double k = cam / (cam + rz); // simple perspective; larger z is farther away
        screen = new PointF(cx + (float)(rx * k), cy + (float)(ry * k));
        depth = (float)rz;
    }

    private static (double X, double Y, double Z) Rotate(double[] m, double x, double y, double z) =>
        (m[0] * x + m[1] * y + m[2] * z,
         m[3] * x + m[4] * y + m[5] * z,
         m[6] * x + m[7] * y + m[8] * z);

    /// <summary>Row-major 3x3 matrix for roll(Z) · pitch(X) · yaw(Y).</summary>
    private static double[] RotationMatrix(double yaw, double pitch, double roll)
    {
        double ca = Math.Cos(yaw), sa = Math.Sin(yaw);
        double cb = Math.Cos(pitch), sb = Math.Sin(pitch);
        double cc = Math.Cos(roll), sc = Math.Sin(roll);
        double[] ry = [ca, 0, sa, 0, 1, 0, -sa, 0, ca];
        double[] rx = [1, 0, 0, 0, cb, -sb, 0, sb, cb];
        double[] rz = [cc, -sc, 0, sc, cc, 0, 0, 0, 1];
        return Multiply(rz, Multiply(rx, ry));
    }

    private static double[] Multiply(double[] a, double[] b)
    {
        var m = new double[9];
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                m[i * 3 + j] = a[i * 3] * b[j] + a[i * 3 + 1] * b[3 + j] + a[i * 3 + 2] * b[6 + j];
        return m;
    }

    private static (double, double, double) Normalize(double x, double y, double z)
    {
        double len = Math.Sqrt(x * x + y * y + z * z);
        return (x / len, y / len, z / len);
    }

    private sealed class FarthestFirst : IComparer<SideFace>
    {
        public static readonly FarthestFirst Instance = new();
        public int Compare(SideFace a, SideFace b) => b.Depth.CompareTo(a.Depth);
    }
}
