using System.Runtime.InteropServices;

namespace Jerry.App.Platform;

internal static class Native
{
    internal const int GwlExStyle = -20;
    internal const long Transparent = 0x20, ToolWindow = 0x80, NoActivate = 0x08000000;
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] internal struct MouseData
    { public Point Point; public uint Mouse, Flags, Time; public nuint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] internal struct KeyboardData
    { public uint Key, ScanCode, Flags, Time; public nuint ExtraInfo; }
    internal delegate nint HookProc(int code, nint message, nint data);

    [DllImport("user32.dll")] internal static extern nint SetWindowsHookExW(int id, HookProc callback, nint module, uint thread);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] internal static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern nint GetModuleHandleW(string? module);
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("user32.dll")] internal static extern nint WindowFromPoint(Point point);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] internal static extern nint GetWindowLongPtr(nint window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] internal static extern nint SetWindowLongPtr(nint window, int index, nint value);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint window, nint after, int x, int y, int w, int h, uint flags);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(nint window);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetWindowDisplayAffinity(nint window, uint affinity);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] internal static extern short GetKeyState(int key);
    [DllImport("user32.dll")] internal static extern nint GetKeyboardLayout(uint thread);
    [DllImport("user32.dll")] internal static extern uint MapVirtualKeyW(uint code, uint mapType);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int ToUnicodeEx(uint key, uint scan, byte[] state, System.Text.StringBuilder buffer, int length, uint flags, nint layout);
    [DllImport("magnification.dll")] internal static extern bool MagInitialize();
    [DllImport("magnification.dll")] internal static extern bool MagUninitialize();
    [DllImport("magnification.dll")] internal static extern bool MagSetFullscreenTransform(float scale, int x, int y);
    [DllImport("magnification.dll")] internal static extern bool MagGetFullscreenTransform(out float scale, out int x, out int y);
    [DllImport("magnification.dll")] internal static extern bool MagShowSystemCursor(bool show);
    [DllImport("magnification.dll")] internal static extern bool MagSetWindowTransform(nint window, [In] float[] transform);
    [DllImport("magnification.dll")] internal static extern bool MagSetWindowSource(nint window, Rect rect);
    [DllImport("magnification.dll")] internal static extern bool MagSetWindowFilterList(nint window, uint mode, int count, nint[] windows);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern nint CreateWindowExW(uint exStyle, string className, string? title, uint style, int x, int y, int w, int h, nint parent, nint menu, nint instance, nint param);
    [DllImport("user32.dll")] internal static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")] internal static extern nint SetWindowRgn(nint window, nint region, bool redraw);
    [DllImport("gdi32.dll")] internal static extern nint CreateEllipticRgn(int left, int top, int right, int bottom);
}
