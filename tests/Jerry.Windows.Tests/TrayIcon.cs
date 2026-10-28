using System.IO;
using System.Reflection;
using System.Windows.Media.Imaging;
using Jerry.App;
using Jerry.App.UI;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace Jerry.Windows.Tests;

internal static partial class Program
{
    private static async Task TrayIconAsync(AppController controller, MainWindow main)
    {
        await PumpAsync(60);
        var field = typeof(AppController).GetField("tray", BindingFlags.Instance | BindingFlags.NonPublic);
        var notification = field?.GetValue(controller) as Forms.NotifyIcon;
        Check(notification?.Icon != null, "Controller owns a loaded notification icon");
        var icon = notification.Icon;
        var expected = Forms.SystemInformation.SmallIconSize;
        using var bitmap = icon.ToBitmap();
        bitmap.Save(Path.Combine(output, "tray-native.png"), Drawing.Imaging.ImageFormat.Png);
        var minX = bitmap.Width; var minY = bitmap.Height; var maxX = -1; var maxY = -1; var visible = 0;
        for (var y = 0; y < bitmap.Height; y++)
        for (var x = 0; x < bitmap.Width; x++)
        {
            var pixel = bitmap.GetPixel(x, y);
            // A charcoal tile blends into a dark taskbar; measure the visible
            // foreground instead of counting its otherwise opaque padding.
            if (pixel.A < 96 || Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)) < 90) continue;
            minX = Math.Min(minX, x); minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); visible++;
        }
        var foregroundWidth = Math.Max(0, maxX - minX + 1);
        var foregroundHeight = Math.Max(0, maxY - minY + 1);
        results.Add(new { trayIcon = true, icon.Width, icon.Height, expectedWidth = expected.Width, expectedHeight = expected.Height, foregroundWidth, foregroundHeight, visiblePixels = visible });
        Console.WriteLine($"TRAY_ICON actual={icon.Width}x{icon.Height}; system={expected.Width}x{expected.Height}; foreground={foregroundWidth}x{foregroundHeight}; pixels={visible}");
        Save(Snapshot(main), "main-window.png");
        Check(icon.Size == expected, "Notification icon uses the native Windows small-icon dimensions");
        Check(foregroundWidth >= Math.Ceiling(bitmap.Width * .80) && foregroundHeight >= Math.Ceiling(bitmap.Height * .80), "Visible foreground fills at least 80% of both notification-icon dimensions");
        Check(main.Icon is BitmapSource image && image.PixelWidth >= 32 && image.PixelHeight >= 32, "Application icon retains a separate image of at least 32 pixels");
        Console.WriteLine("TRAY_ICON_CHECKS_PASSED");
    }
}
