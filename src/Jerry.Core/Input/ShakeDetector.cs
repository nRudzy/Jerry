namespace Jerry.Core.Input;

public sealed class ShakeDetector
{
    private double lastX, lastY;
    private int direction;
    private bool initialized;
    private long lastTrigger = -2000;
    private readonly Queue<long> reversals = new();
    public bool Observe(double x, double y, long milliseconds, double sensitivity)
    {
        if (!initialized) { initialized = true; lastX = x; lastY = y; return false; }
        var dx = x - lastX; var dy = y - lastY;
        lastX = x; lastY = y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        while (reversals.TryPeek(out var time) && milliseconds - time > 420) reversals.Dequeue();
        if (distance < 8 + 45 * (1 - Math.Clamp(sensitivity, 0, 1))) return false;
        var next = Math.Abs(dx) >= Math.Abs(dy) ? Math.Sign(dx) : Math.Sign(dy);
        if (direction != 0 && next != direction) reversals.Enqueue(milliseconds);
        direction = next;
        if (reversals.Count < 4 || milliseconds - lastTrigger < 1200) return false;
        reversals.Clear(); lastTrigger = milliseconds; return true;
    }
}
