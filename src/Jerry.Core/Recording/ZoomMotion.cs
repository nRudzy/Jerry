namespace Jerry.Core.Recording;
public readonly record struct ZoomFrame(double Scale, double X, double Y, bool IsMoving);
public sealed class ZoomMotion
{
    private double currentScale = 1, fromScale = 1, targetScale = 1, currentX, currentY;
    private long transitionStart, lastUpdate;
    public void Reset(double x, double y, long time)
    { currentScale = fromScale = targetScale = 1; currentX = x; currentY = y; lastUpdate = transitionStart = time; }
    public ZoomFrame Advance(double scale, double x, double y, long time, int duration, double speed)
    {
        if (targetScale != scale) { fromScale = currentScale; targetScale = scale; transitionStart = time; }
        var progress = duration <= 0 ? 1 : Math.Clamp((time - transitionStart) / (double)duration, 0, 1);
        var eased = progress * progress * (3 - 2 * progress);
        currentScale = fromScale + (targetScale - fromScale) * eased;
        var delta = Math.Clamp(time - lastUpdate, 0, 100);
        var follow = 1 - Math.Exp(-Math.Clamp(speed, .01, 1) * 30 * delta / 1000d);
        currentX += (x - currentX) * follow; currentY += (y - currentY) * follow;
        if (Math.Abs(x - currentX) < .1) currentX = x;
        if (Math.Abs(y - currentY) < .1) currentY = y;
        lastUpdate = time;
        return new(currentScale, currentX, currentY, progress < 1 || Math.Abs(x - currentX) > .1 || Math.Abs(y - currentY) > .1);
    }
}
public static class MagnificationGeometry
{
    public static PixelRect LensBounds(PixelRect monitor, int x, int y, int diameter)
    {
        var size = Math.Min(diameter, Math.Min(monitor.Width, monitor.Height));
        var left = x + 28; var top = y + 28;
        if (left + size > monitor.Right) left = x - size - 28;
        if (top + size > monitor.Bottom) top = y - size - 28;
        return new(Math.Clamp(left, monitor.X, monitor.Right - size), Math.Clamp(top, monitor.Y, monitor.Bottom - size), size, size);
    }
}
