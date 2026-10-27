using System.Windows;
using System.Windows.Interop;
using Jerry.Core.Recording;

namespace Jerry.App.Platform;

public static class WindowTools
{
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
