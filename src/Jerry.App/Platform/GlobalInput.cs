using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Jerry.App.Platform;

public readonly record struct PointerInput(int X, int Y, int Button, long Milliseconds);
public readonly record struct KeyboardInput(uint Key, bool Repeat, bool Control, bool Alt, bool Shift, bool Windows, bool OtherKeysHeld = false);

public sealed class GlobalInput : IDisposable
{
    private readonly Dispatcher dispatcher;
    private readonly Native.HookProc mouseProc, keyProc;
    private readonly HashSet<uint> held = new();
    private nint mouseHook, keyHook;
    private Native.Point latestPoint;
    private int movePending;
    public bool CaptureMouse { get; set; }
    public bool CaptureDrawingKeys { get; set; }
    public bool CaptureEscape { get; set; }
    public uint HoldKeyToCapture { get; set; }
    public event Action<PointerInput>? Moved, Pressed, Released;
    public event Action? Scrolled;
    public event Action<KeyboardInput>? KeyPressed, KeyReleased;

    public GlobalInput(Dispatcher dispatcher)
    {
        this.dispatcher = dispatcher;
        mouseProc = MouseCallback; keyProc = KeyCallback;
        var module = Native.GetModuleHandleW(null);
        mouseHook = Native.SetWindowsHookExW(14, mouseProc, module, 0);
        keyHook = Native.SetWindowsHookExW(13, keyProc, module, 0);
        if (mouseHook == 0 || keyHook == 0) { Dispose(); throw new InvalidOperationException("Impossible d'écouter les entrées Windows."); }
    }
    private nint MouseCallback(int code, nint message, nint data)
    {
        if (code < 0) return Native.CallNextHookEx(mouseHook, code, message, data);
        var value = Marshal.PtrToStructure<Native.MouseData>(data);
        var msg = (int)message;
        var input = new PointerInput(value.Point.X, value.Point.Y, msg switch { 0x204 or 0x205 => 2, 0x207 or 0x208 => 3, 0x20B or 0x20C => 4, _ => 1 }, Environment.TickCount64);
        if (msg == 0x200)
        {
            latestPoint = value.Point;
            if (Interlocked.Exchange(ref movePending, 1) == 0)
                dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    var p = latestPoint; Interlocked.Exchange(ref movePending, 0);
                    Moved?.Invoke(new(p.X, p.Y, 0, Environment.TickCount64));
                }));
        }
        else if (msg is 0x201 or 0x204 or 0x207 or 0x20B)
            dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => Pressed?.Invoke(input)));
        else if (msg is 0x202 or 0x205 or 0x208 or 0x20C)
            dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => Released?.Invoke(input)));
        else if (msg is 0x20A or 0x20E)
            dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => Scrolled?.Invoke()));
        if (CaptureMouse && (msg is 0x201 or 0x202 or 0x204 or 0x205 or 0x207 or 0x208) && !OwnWindowAt(value.Point)) return 1;
        return Native.CallNextHookEx(mouseHook, code, message, data);
    }
    private nint KeyCallback(int code, nint message, nint data)
    {
        if (code < 0) return Native.CallNextHookEx(keyHook, code, message, data);
        var value = Marshal.PtrToStructure<Native.KeyboardData>(data);
        var down = (int)message is 0x100 or 0x104;
        var repeat = down && !held.Add(value.Key);
        if (!down) held.Remove(value.Key);
        bool Any(params uint[] keys) => keys.Any(held.Contains);
        var input = new KeyboardInput(value.Key, repeat, Any(0xA2, 0xA3, 0x11), Any(0xA4, 0xA5, 0x12), Any(0xA0, 0xA1, 0x10), Any(0x5B, 0x5C), held.Any(k => k != value.Key));
        dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => { if (down) KeyPressed?.Invoke(input); else KeyReleased?.Invoke(input); }));
        var drawingKey = value.Key is 0x1B or 0x46 or 0x41 or 0x52 or 0x43 or 0x4C or 0x48 or 0x54 or 0x4E or 0x5A or 0x2E;
        if (CaptureDrawingKeys && drawingKey && !OwnForeground()) return 1;
        if (CaptureEscape && value.Key == 0x1B && !OwnForeground()) return 1;
        if (HoldKeyToCapture != 0 && value.Key == HoldKeyToCapture && !OwnForeground()) return 1;
        return Native.CallNextHookEx(keyHook, code, message, data);
    }
    private static bool OwnWindowAt(Native.Point point)
    { Native.GetWindowThreadProcessId(Native.WindowFromPoint(point), out var process); return process == Environment.ProcessId; }
    private static bool OwnForeground()
    { Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out var process); return process == Environment.ProcessId; }
    public static uint KeyCode(string? key) => key switch
    {
        "LeftControl" => 0xA2, "RightControl" => 0xA3, "LeftAlt" => 0xA4, "RightAlt" => 0xA5,
        "LeftShift" => 0xA0, "RightShift" => 0xA1, "LeftWindows" => 0x5B, "RightWindows" => 0x5C, _ => 0
    };
    public void Dispose()
    {
        if (mouseHook != 0) Native.UnhookWindowsHookEx(mouseHook);
        if (keyHook != 0) Native.UnhookWindowsHookEx(keyHook);
        mouseHook = keyHook = 0;
    }
}
