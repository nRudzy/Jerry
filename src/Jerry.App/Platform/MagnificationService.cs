using System.Windows;
using System.Windows.Interop;
using Jerry.Core.Recording;

namespace Jerry.App.Platform;

public sealed class MagnificationService : IDisposable
{
    private readonly bool initialized = Native.MagInitialize();
    private bool ownsZoom, cursorHidden;
    private float previousScale = 1;
    private int previousX, previousY;
    private Window? loupe;
    private nint magnifier;
    public bool Zoom(double scale, int x, int y, PixelRect monitor)
    {
        if (!initialized) return false;
        if (!ownsZoom) Native.MagGetFullscreenTransform(out previousScale, out previousX, out previousY);
        var viewWidth = monitor.Width / scale; var viewHeight = monitor.Height / scale;
        var offsetX = (int)Math.Clamp(x - viewWidth / 2, monitor.X, monitor.Right - viewWidth);
        var offsetY = (int)Math.Clamp(y - viewHeight / 2, monitor.Y, monitor.Bottom - viewHeight);
        var ok = Native.MagSetFullscreenTransform((float)scale, offsetX, offsetY);
        ownsZoom |= ok; return ok;
    }
    public void StopZoom()
    {
        if (ownsZoom) Native.MagSetFullscreenTransform(previousScale, previousX, previousY);
        ownsZoom = false;
    }
    public bool HideCursor(bool hidden)
    {
        if (!initialized) return false;
        if (hidden == cursorHidden) return true;
        if (!Native.MagShowSystemCursor(!hidden)) return false;
        cursorHidden = hidden; return true;
    }
    public void ShowLoupe(bool show, int x, int y, int diameter, double scale, IEnumerable<nint> exclude)
    {
        if (!initialized || !show) { loupe?.Hide(); return; }
        if (loupe == null)
        {
            loupe = new Window { WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, ShowActivated = false, Topmost = true, Width = diameter, Height = diameter, Background = System.Windows.Media.Brushes.Black };
            loupe.SourceInitialized += (_, _) =>
            {
                WindowTools.MakeOverlay(loupe);
                var handle = new WindowInteropHelper(loupe).Handle;
                magnifier = Native.CreateWindowExW(0, "Magnifier", null, 0x50000001, 0, 0, diameter, diameter, handle, 0, 0, 0);
            };
            loupe.Show();
        }
        else if (!loupe.IsVisible) loupe.Show();
        var hwnd = new WindowInteropHelper(loupe).Handle;
        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(x, y)).Bounds;
        var destination = MagnificationGeometry.LensBounds(new(screen.X, screen.Y, screen.Width, screen.Height), x, y, diameter);
        diameter = destination.Width;
        Native.SetWindowPos(hwnd, new nint(-1), destination.X, destination.Y, diameter, diameter, 0x10 | 0x40);
        Native.SetWindowRgn(hwnd, Native.CreateEllipticRgn(0, 0, diameter, diameter), true);
        if (magnifier == 0) return;
        Native.SetWindowPos(magnifier, 0, 0, 0, diameter, diameter, 0x10 | 0x40);
        Native.MagSetWindowTransform(magnifier, [(float)scale, 0, 0, 0, (float)scale, 0, 0, 0, 1]);
        var filtered = exclude.Append(hwnd).ToArray();
        Native.MagSetWindowFilterList(magnifier, 0, filtered.Length, filtered);
        var half = diameter / scale / 2;
        Native.MagSetWindowSource(magnifier, new Native.Rect { Left = (int)(x - half), Top = (int)(y - half), Right = (int)(x + half), Bottom = (int)(y + half) });
    }
    public void Dispose()
    {
        StopZoom(); HideCursor(false);
        if (magnifier != 0) Native.DestroyWindow(magnifier);
        loupe?.Close();
        if (initialized) Native.MagUninitialize();
    }
}
