using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Jerry.App.Media;
using Jerry.App.Platform;
using Jerry.Core.Recording;
using Jerry.Core.Settings;

namespace Jerry.App.Effects;

public sealed class CameraOverlayWindow : Window
{
    private readonly AppSettings settings;
    private readonly CameraVisual visual;
    private PixelRect monitor;
    private PixelRect bounds;
    private bool dragging;
    private System.Drawing.Point dragStart;
    private PixelRect dragBounds;
    public event Action? PositionChanged;
    public CameraOverlayWindow(AppSettings settings)
    {
        this.settings = settings;
        Title = "Jerry — Caméra"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; Topmost = true;
        ShowActivated = false; ShowInTaskbar = false; Cursor = Cursors.SizeAll;
        visual = new CameraVisual(settings); Content = visual;
        monitor = new PixelRect((int)SystemParameters.WorkArea.X, (int)SystemParameters.WorkArea.Y, (int)SystemParameters.WorkArea.Width, (int)SystemParameters.WorkArea.Height);
        Width = settings.Camera.Size; Height = settings.Camera.Size;
        SourceInitialized += (_, _) =>
        {
            WindowTools.MakeOverlay(this, false);
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook((IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            {
                if (message == 0x0021) { handled = true; return new IntPtr(3); }
                return IntPtr.Zero;
            });
            MoveToMonitor(monitor);
        };
        MouseLeftButtonDown += BeginDrag; MouseMove += ContinueDrag; MouseLeftButtonUp += EndDrag;
        LostMouseCapture += (_, _) => { if (dragging) FinishDrag(); };
    }
    public void UpdateFrame(WebcamFrame frame)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.InvokeAsync(() => UpdateFrame(frame)); return; }
        if (frame.Width <= 0 || frame.Height <= 0 || frame.Stride < frame.Width * 4 || frame.Pixels.Length < (long)frame.Stride * frame.Height) return;
        var bitmap = BitmapSource.Create(frame.Width, frame.Height, 96, 96, PixelFormats.Pbgra32, null, frame.Pixels, frame.Stride);
        bitmap.Freeze(); visual.Frame = bitmap;
        if (!dragging) MoveToMonitor(monitor);
        visual.InvalidateVisual();
    }
    public void MoveToMonitor(PixelRect next)
    {
        monitor = next; if (dragging) return;
        var width = Math.Clamp((int)Math.Round(settings.Camera.Size), 40, Math.Max(40, next.Width));
        var height = settings.Camera.Shape == CameraShape.Card ? Math.Max(24, (int)Math.Round(width * 9d / 16)) : width;
        height = Math.Min(height, Math.Max(1, next.Height));
        var x = next.X + (int)Math.Round(Math.Clamp(settings.Camera.X, 0, 1) * Math.Max(0, next.Width - width));
        var y = next.Y + (int)Math.Round(Math.Clamp(settings.Camera.Y, 0, 1) * Math.Max(0, next.Height - height));
        bounds = new(x, y, width, height);
        if (new WindowInteropHelper(this).Handle != IntPtr.Zero) WindowTools.Position(this, bounds);
        visual.InvalidateVisual();
    }
    private void BeginDrag(object sender, MouseButtonEventArgs e)
    {
        dragging = true; dragStart = System.Windows.Forms.Cursor.Position; dragBounds = bounds; CaptureMouse(); e.Handled = true;
    }
    private void ContinueDrag(object sender, MouseEventArgs e)
    {
        if (!dragging || e.LeftButton != MouseButtonState.Pressed) return;
        var point = System.Windows.Forms.Cursor.Position;
        bounds = bounds with
        {
            X = Math.Clamp(dragBounds.X + point.X - dragStart.X, monitor.X, Math.Max(monitor.X, monitor.Right - bounds.Width)),
            Y = Math.Clamp(dragBounds.Y + point.Y - dragStart.Y, monitor.Y, Math.Max(monitor.Y, monitor.Bottom - bounds.Height))
        };
        WindowTools.Position(this, bounds); e.Handled = true;
    }
    private void EndDrag(object sender, MouseButtonEventArgs e) { if (!dragging) return; FinishDrag(); ReleaseMouseCapture(); e.Handled = true; }
    private void FinishDrag()
    {
        dragging = false;
        if (settings.Camera.SnapToCorners)
        {
            var margin = Math.Min(20, Math.Max(0, (Math.Min(monitor.Width, monitor.Height) - Math.Max(bounds.Width, bounds.Height)) / 2));
            var left = monitor.X + margin; var right = Math.Max(left, monitor.Right - bounds.Width - margin);
            var top = monitor.Y + margin; var bottom = Math.Max(top, monitor.Bottom - bounds.Height - margin);
            var x = Math.Abs(bounds.X - left) <= Math.Abs(bounds.X - right) ? left : right;
            var y = Math.Abs(bounds.Y - top) <= Math.Abs(bounds.Y - bottom) ? top : bottom;
            if (Math.Abs(bounds.X - x) < 100 && Math.Abs(bounds.Y - y) < 100) bounds = bounds with { X = x, Y = y };
        }
        settings.Camera.X = Math.Clamp((bounds.X - monitor.X) / (double)Math.Max(1, monitor.Width - bounds.Width), 0, 1);
        settings.Camera.Y = Math.Clamp((bounds.Y - monitor.Y) / (double)Math.Max(1, monitor.Height - bounds.Height), 0, 1);
        WindowTools.Position(this, bounds); PositionChanged?.Invoke();
    }
}

internal sealed class CameraVisual(AppSettings settings) : FrameworkElement
{
    public BitmapSource? Frame { get; set; }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var rect = new Rect(2, 2, Math.Max(1, ActualWidth - 4), Math.Max(1, ActualHeight - 4));
        Geometry clip = settings.Camera.Shape == CameraShape.Circle
            ? new EllipseGeometry(rect) : new RectangleGeometry(rect, 16, 16);
        dc.PushClip(clip);
        dc.DrawRectangle(Brushes.Black, null, rect);
        if (Frame is { } frame)
        {
            var scale = Math.Max(rect.Width / frame.PixelWidth, rect.Height / frame.PixelHeight);
            var width = frame.PixelWidth * scale; var height = frame.PixelHeight * scale;
            if (settings.Camera.Mirror) dc.PushTransform(new ScaleTransform(-1, 1, rect.Left + rect.Width / 2, 0));
            dc.DrawImage(frame, new Rect(rect.Left + (rect.Width - width) / 2, rect.Top + (rect.Height - height) / 2, width, height));
            if (settings.Camera.Mirror) dc.Pop();
        }
        dc.Pop();
        Brush? border = settings.Camera.Border switch
        {
            CameraBorderStyle.White => Brushes.White,
            CameraBorderStyle.Gradient => new LinearGradientBrush(Color.FromRgb(125, 229, 195), Color.FromRgb(125, 103, 235), 40),
            _ => null
        };
        if (border != null) dc.DrawGeometry(null, new Pen(border, 3), clip);
    }
}
