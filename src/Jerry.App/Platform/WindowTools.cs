using System.Windows;
using System.Windows.Interop;
using Jerry.Core.Recording;

namespace Jerry.App.Platform;

public static class WindowTools
{
    public static void PlaceNear(Window window, System.Drawing.Point anchor)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == 0) return;
        var area = System.Windows.Forms.Screen.FromPoint(anchor).WorkingArea;
        // SetWindowPos takes physical coordinates, including on a secondary monitor.
        // Re-read the size after a possible WM_DPICHANGED before the final clamp.
        for (var pass = 0; pass < 2; pass++)
        {
            if (!Native.GetWindowRect(handle, out var bounds)) return;
            var x = Math.Clamp(anchor.X - (bounds.Right - bounds.Left) + 12, area.Left + 8, Math.Max(area.Left + 8, area.Right - (bounds.Right - bounds.Left) - 8));
            var y = Math.Clamp(anchor.Y - (bounds.Bottom - bounds.Top) - 10, area.Top + 8, Math.Max(area.Top + 8, area.Bottom - (bounds.Bottom - bounds.Top) - 8));
            Native.SetWindowPos(handle, 0, x, y, 0, 0, 0x01 | 0x04 | 0x10);
        }
    }
    public static void ClampToCurrentMonitor(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == 0) return;
        // Both working area and window rectangle are physical pixels. Keep the
        // monitor selected by the actual window, including negative origins.
        for (var pass = 0; pass < 2; pass++)
        {
            var area = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
            if (!Native.GetWindowRect(handle, out var bounds)) return;
            var width = bounds.Right - bounds.Left;
            var height = bounds.Bottom - bounds.Top;
            var x = Math.Clamp(bounds.Left, area.Left, Math.Max(area.Left, area.Right - width));
            var y = Math.Clamp(bounds.Top, area.Top, Math.Max(area.Top, area.Bottom - height));
            Native.SetWindowPos(handle, 0, x, y, 0, 0, 0x15);
        }
    }
    public static void ExcludeFromCapture(Window window)
    {
        void Apply() => Native.SetWindowDisplayAffinity(new WindowInteropHelper(window).Handle, 0x11);
        if (new WindowInteropHelper(window).Handle != 0) Apply();
        else window.SourceInitialized += (_, _) => Apply();
    }
    public static void MakeOverlay(Window window, bool passThrough = true)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var style = (long)Native.GetWindowLongPtr(handle, Native.GwlExStyle);
        style |= Native.ToolWindow | Native.NoActivate;
        style = passThrough ? style | Native.Transparent : style & ~Native.Transparent;
        Native.SetWindowLongPtr(handle, Native.GwlExStyle, (nint)style);
    }
    public static void Position(Window window, PixelRect bounds)
        => Native.SetWindowPos(new WindowInteropHelper(window).Handle, new nint(-1), bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x10 | 0x40);
}
