using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MotionSicknessHelper;

internal static class NativeMethods
{
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int GWL_EXSTYLE = -20;
    public const int ULW_ALPHA = 0x00000002;
    public const byte AC_SRC_OVER = 0x00;
    public const byte AC_SRC_ALPHA = 0x01;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT32
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SIZE32
    {
        public int Width;
        public int Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BLENDFUNCTION
    {
        public byte BlendOp;
        public byte BlendFlags;
        public byte SourceConstantAlpha;
        public byte AlphaFormat;
    }

    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    public static extern IntPtr CreateCompatibleDC(IntPtr hDC);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteDC(IntPtr hDC);

    [DllImport("gdi32.dll")]
    public static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    public static extern bool UpdateLayeredWindow(
        IntPtr hwnd,
        IntPtr hdcDst,
        ref POINT32 pptDst,
        ref SIZE32 psize,
        IntPtr hdcSrc,
        ref POINT32 pptSrc,
        int crKey,
        ref BLENDFUNCTION pblend,
        int dwFlags);
}

internal sealed class OverlayForm : Form
{
    private OverlayConfig _config;
    private readonly System.Windows.Forms.Timer _flashTimer;
    private bool _flashPhase;

    public OverlayForm(OverlayConfig config)
    {
        _config = config;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Visible = false;

        _flashTimer = new System.Windows.Forms.Timer();
        _flashTimer.Tick += (_, _) =>
        {
            _flashPhase = !_flashPhase;
            RefreshOverlay();
        };

        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

        UpdateFlashTimer();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_LAYERED | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_TOOLWINDOW;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    public void ApplyConfig(OverlayConfig config)
    {
        _config = config;
        UpdateFlashTimer();
        if (IsHandleCreated)
            RefreshOverlay();
    }

    private void UpdateFlashTimer()
    {
        bool anyFlash = _config.Shapes.Any(s => s.FlashEnabled);
        if (anyFlash)
        {
            _flashTimer.Interval = Math.Clamp(_config.FlashIntervalMs, 50, 10000);
            if (!_flashTimer.Enabled)
                _flashTimer.Start();
        }
        else
        {
            _flashTimer.Stop();
            _flashPhase = false;
        }
    }

    public void RefreshOverlay()
    {
        if (!IsHandleCreated)
            return;

        var screen = Screen.FromHandle(Handle) ?? Screen.PrimaryScreen;
        if (screen is null)
            return;

        var bounds = screen.Bounds;
        if (Bounds != bounds)
        {
            Bounds = bounds;
        }

        int width = bounds.Width;
        int height = bounds.Height;

        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            DrawShapes(g, width, height);
        }

        UpdateLayeredWindow(bitmap, bounds.Location);
    }

    private void DrawShapes(Graphics g, int width, int height)
    {
        float centerX = width / 2f;
        float centerY = height / 2f;

        foreach (var shape in _config.Shapes)
        {
            float safeInset = Math.Max(_config.EdgeInset, shape.Thickness / 2f + 2f);
            PointF anchor = GetAnchor(shape.Position, width, height, safeInset);
            float dx = centerX - anchor.X;
            float dy = centerY - anchor.Y;
            float length = MathF.Sqrt(dx * dx + dy * dy);
            if (length <= 0f)
                continue;

            float dirX = dx / length;
            float dirY = dy / length;
            float perpX = -dirY;
            float perpY = dirX;

            string colorText = shape.FlashEnabled && _flashPhase ? shape.Color2 : shape.Color;
            var baseColor = ColorTranslator.FromHtml(colorText);
            var color = Color.FromArgb(Math.Clamp(shape.Opacity, 0, 255), baseColor.R, baseColor.G, baseColor.B);

            if (shape.Shape == ShapeKind.Triangle)
            {
                float halfBase = shape.Thickness / 2f;
                var apex = new PointF(anchor.X + dirX * shape.Size, anchor.Y + dirY * shape.Size);
                var baseLeft = new PointF(anchor.X + perpX * halfBase, anchor.Y + perpY * halfBase);
                var baseRight = new PointF(anchor.X - perpX * halfBase, anchor.Y - perpY * halfBase);

                using var brush = new SolidBrush(color);
                g.FillPolygon(brush, new[] { anchor, baseLeft, apex, baseRight });
            }
            else
            {
                float half = shape.Thickness / 2f;
                float startOffset = Math.Min(half, shape.Size / 2f);
                float endOffset = Math.Max(shape.Size - half, startOffset);
                var start = new PointF(anchor.X + dirX * startOffset, anchor.Y + dirY * startOffset);
                var end = new PointF(anchor.X + dirX * endOffset, anchor.Y + dirY * endOffset);

                using var pen = new Pen(color, shape.Thickness)
                {
                    StartCap = LineCap.Round,
                    EndCap = LineCap.Round
                };
                g.DrawLine(pen, start, end);
            }
        }
    }

    private static PointF GetAnchor(AnchorPosition position, int width, int height, float inset)
    {
        return position switch
        {
            AnchorPosition.TopLeft => new PointF(inset, inset),
            AnchorPosition.TopRight => new PointF(width - inset, inset),
            AnchorPosition.BottomLeft => new PointF(inset, height - inset),
            AnchorPosition.BottomRight => new PointF(width - inset, height - inset),
            AnchorPosition.Left => new PointF(inset, height / 2f),
            AnchorPosition.Top => new PointF(width / 2f, inset),
            AnchorPosition.Right => new PointF(width - inset, height / 2f),
            AnchorPosition.Bottom => new PointF(width / 2f, height - inset),
            _ => new PointF(inset, inset)
        };
    }

    private void UpdateLayeredWindow(Bitmap bitmap, Point screenPosition)
    {
        IntPtr screenDc = NativeMethods.GetDC(IntPtr.Zero);
        IntPtr memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
        IntPtr hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
        IntPtr oldBitmap = NativeMethods.SelectObject(memoryDc, hBitmap);

        var dst = new NativeMethods.POINT32 { X = screenPosition.X, Y = screenPosition.Y };
        var size = new NativeMethods.SIZE32 { Width = bitmap.Width, Height = bitmap.Height };
        var src = new NativeMethods.POINT32 { X = 0, Y = 0 };
        var blend = new NativeMethods.BLENDFUNCTION
        {
            BlendOp = NativeMethods.AC_SRC_OVER,
            SourceConstantAlpha = 255,
            AlphaFormat = NativeMethods.AC_SRC_ALPHA
        };

        NativeMethods.UpdateLayeredWindow(Handle, screenDc, ref dst, ref size, memoryDc, ref src, 0, ref blend, NativeMethods.ULW_ALPHA);

        NativeMethods.SelectObject(memoryDc, oldBitmap);
        NativeMethods.DeleteObject(hBitmap);
        NativeMethods.DeleteDC(memoryDc);
        NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _flashTimer.Stop();
            _flashTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
