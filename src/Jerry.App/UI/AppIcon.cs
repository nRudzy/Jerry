using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Jerry.App.UI;

internal static class AppIcon
{
    public static Icon Create()
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var blue = new SolidBrush(Color.FromArgb(59, 130, 246));
        using var outline = new Pen(Color.White, 1.6f) { LineJoin = LineJoin.Round };
        graphics.FillEllipse(blue, 2, 2, 28, 28);
        using var cursor = new GraphicsPath();
        cursor.AddPolygon([new PointF(11, 7), new PointF(22, 17), new PointF(17, 18), new PointF(15, 24), new PointF(11, 7)]);
        using var fill = new SolidBrush(Color.FromArgb(29, 29, 29));
        graphics.FillPath(fill, cursor); graphics.DrawPath(outline, cursor);
        var handle = bitmap.GetHicon();
        try { using var borrowed = Icon.FromHandle(handle); return (Icon)borrowed.Clone(); }
        finally { DestroyIcon(handle); }
    }
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(nint icon);
}
