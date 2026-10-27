using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Jerry.App;
using Jerry.App.Platform;
using Jerry.Core.Recording;
using Application = System.Windows.Application;

namespace Jerry.Windows.Tests;

internal static partial class Program
{
    private static async Task NativeServicesAsync()
    {
        HotkeyManager? first = new();
        using (var second = new HotkeyManager())
        {
            try
            {
                var binding = new Dictionary<string, string> { ["qa-unused"] = "Ctrl+Alt+F24" };
                var registered = first.Apply(binding); Console.WriteLine("HOTKEY_FIRST_ERRORS=" + string.Join(";", registered));
                Check(registered.Count == 0, "Own manager registered Ctrl+Alt+F24 without pressing it");
                var conflict = second.Apply(binding); Console.WriteLine("HOTKEY_CONFLICT_ERRORS=" + string.Join(";", conflict));
                Check(conflict.Count == 1, "Second own manager reports actual native hotkey conflict");
                first.Dispose(); first = null;
                var released = second.Apply(binding); Console.WriteLine("HOTKEY_AFTER_RELEASE_ERRORS=" + string.Join(";", released));
                Check(released.Count == 0, "Disposing first manager releases native binding for second manager");
                results.Add(new { nativeHotkey = "Ctrl+Alt+F24", registered = true, conflictReported = true, releaseVerified = true, keysSent = 0 });
            }
            finally { first?.Dispose(); }
        }
        using var service = new MagnificationService();
        var beforeAvailable = Native.MagGetFullscreenTransform(out var beforeScale, out var beforeX, out var beforeY);
        var beforeError = beforeAvailable ? 0 : Marshal.GetLastPInvokeError();
        var cursorBefore = CursorState();
        Console.WriteLine($"ZOOM_BEFORE available={beforeAvailable}; error={beforeError}; scale={beforeScale}; x={beforeX}; y={beforeY}");
        Console.WriteLine($"CURSOR_BEFORE available={cursorBefore.Available}; flags={cursorBefore.Info.Flags}");
        bool? zoomResult = null; bool? transformAfterAvailable = null;
        uint? hiddenCursorFlags = null;
        bool? cursorHideAccepted = null;
        var windowsBefore = Application.Current.Windows.Cast<Window>().ToHashSet();
        try
        {
            Check(Native.GetCursorPos(out var pointer), "Actual pointer position is available without moving it");
            var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(pointer.X, pointer.Y)).Bounds;
            var monitor = new PixelRect(screen.X, screen.Y, screen.Width, screen.Height);
            if (beforeAvailable)
            {
                zoomResult = service.Zoom(1.05, pointer.X, pointer.Y, monitor);
                Console.WriteLine("ZOOM_SERVICE_RETURN=" + zoomResult);
                transformAfterAvailable = Native.MagGetFullscreenTransform(out var afterScale, out var afterX, out var afterY);
                Console.WriteLine($"ZOOM_AFTER available={transformAfterAvailable}; scale={afterScale}; x={afterX}; y={afterY}");
                if (zoomResult == true && transformAfterAvailable == true) Check(Math.Abs(afterScale - 1.05f) < .0001f, "Native fullscreen query confirms actual 1.05 zoom");
                else Console.WriteLine("NATIVE_LIMITATION=Fullscreen zoom was unavailable or unobservable; no successful zoom is claimed.");
                service.StopZoom();
                var restored = Native.MagGetFullscreenTransform(out var restoredScale, out var restoredX, out var restoredY);
                Check(restored && Math.Abs(restoredScale - beforeScale) < .0001f && restoredX == beforeX && restoredY == beforeY, "StopZoom restored exact original scale and offsets");
            }
            else Console.WriteLine("NATIVE_LIMITATION=Original zoom state unavailable; reversible zoom trial skipped.");
            if (cursorBefore.Available && (cursorBefore.Info.Flags & 1) != 0)
            {
                cursorHideAccepted = service.HideCursor(true);
                Console.WriteLine("CURSOR_HIDE_NATIVE_ACCEPTED=" + cursorHideAccepted);
                await PumpAsync(50);
                var hidden = CursorState(); hiddenCursorFlags = hidden.Available ? hidden.Info.Flags : null;
                Console.WriteLine($"CURSOR_AFTER_HIDE available={hidden.Available}; flags={hiddenCursorFlags}");
                if (hidden.Available && (hidden.Info.Flags & 1) == 0) Console.WriteLine("CURSOR_HIDE_OBSERVED=true");
                else Console.WriteLine("NATIVE_LIMITATION=Cursor hide not confirmed by GetCursorInfo; no visibility success is claimed.");
                service.HideCursor(false); await PumpAsync(50);
                var shown = CursorState();
                Check(shown.Available && (shown.Info.Flags & 1) != 0, "Cursor visible state restored after public HideCursor(false)");
            }
            else Console.WriteLine("NATIVE_LIMITATION=Cursor was not initially reported visible; visibility trial skipped to preserve its state.");
            var edge = new System.Drawing.Point(screen.Right - 2, screen.Bottom - 2);
            service.ShowLoupe(true, edge.X, edge.Y, 180, 1.4, []);
            await PumpAsync(120);
            var loupe = Application.Current.Windows.Cast<Window>().Except(windowsBefore).SingleOrDefault();
            if (loupe == null) Console.WriteLine("NATIVE_LIMITATION=Loupe window was not created; no successful loupe is claimed.");
            else
            {
                var handle = new WindowInteropHelper(loupe).Handle;
                Check(Native.GetWindowRect(handle, out var rect), "Own loupe actual native rectangle is observable");
                Console.WriteLine($"LOUPE_BOUNDS={rect.Left},{rect.Top},{rect.Right},{rect.Bottom}");
                Check(rect.Left >= screen.Left && rect.Top >= screen.Top && rect.Right <= screen.Right && rect.Bottom <= screen.Bottom, "Own loupe stays wholly within real monitor at bottom-right pointer edge");
                var child = Native.FindWindowEx(handle, IntPtr.Zero, "Magnifier", null);
                Console.WriteLine("LOUPE_MAGNIFIER_CHILD=" + child);
                var transform = new float[9];
                var transformAvailable = child != IntPtr.Zero && Native.MagGetWindowTransform(child, transform);
                Console.WriteLine("LOUPE_TRANSFORM_AVAILABLE=" + transformAvailable + "; scale=" + transform[0]);
                if (transformAvailable) Check(Math.Abs(transform[0] - 1.4f) < .0001f, "Own native magnifier control received requested lens scale");
                results.Add(new { loupe = new { rect.Left, rect.Top, rect.Right, rect.Bottom }, magnifierChild = child.ToInt64(), transformAvailable, scale = transform[0], magnifiedPixelContentVerified = false });
                service.ShowLoupe(false, edge.X, edge.Y, 180, 1.4, []);
                Check(!loupe.IsVisible, "Own loupe hides after ShowLoupe(false)");
            }
        }
        finally
        {
            service.StopZoom();
            if (cursorBefore.Available && (cursorBefore.Info.Flags & 1) != 0)
            {
                service.HideCursor(false);
                var current = CursorState();
                if (current.Available && (current.Info.Flags & 1) == 0) Console.WriteLine("EMERGENCY_CURSOR_RESTORE=" + Native.MagShowSystemCursor(true));
            }
            if (beforeAvailable)
            {
                var observed = Native.MagGetFullscreenTransform(out var scale, out var x, out var y);
                if (!observed || scale != beforeScale || x != beforeX || y != beforeY)
                    Console.WriteLine("EMERGENCY_ZOOM_RESTORE=" + Native.MagSetFullscreenTransform(beforeScale, beforeX, beforeY));
                Check(Native.MagGetFullscreenTransform(out scale, out x, out y) && scale == beforeScale && x == beforeX && y == beforeY, "Final exact fullscreen state restoration verified");
            }
        }
        results.Add(new { fullscreenOriginalStateAvailable = beforeAvailable, beforeScale, beforeX, beforeY, zoomServiceReturn = zoomResult, fullscreenAfterStateAvailable = transformAfterAvailable, cursorInitiallyAvailable = cursorBefore.Available, cursorBeforeFlags = cursorBefore.Info.Flags, cursorHideAccepted, hiddenCursorFlags });
        Console.WriteLine("NATIVE_SERVICES_COMPLETED_WITH_RAW_CAPABILITY_FLAGS");
    }

    private static async Task MeasureIdleAsync(AppController controller)
    {
        var settings = controller.Settings;
        settings.Enabled = true; settings.Ring.Enabled = true; settings.Ring.Animate = false; settings.Ring.FollowSpeed = 1;
        settings.Spotlight.Enabled = settings.Trail.Enabled = settings.Click.Enabled = settings.Sound.Enabled = settings.Keystrokes.Enabled = settings.Focus.Enabled = false;
        settings.Zoom.Enabled = settings.Zoom.LoupeEnabled = settings.Camera.Enabled = settings.Captions.LiveEnabled = false;
        controller.SettingsChanged();
        await PumpAsync(1000);
        using var process = Process.GetCurrentProcess(); process.Refresh();
        Native.GetCursorPos(out var startPointer);
        var initialCpu = process.TotalProcessorTime; var initialPrivate = process.PrivateMemorySize64; var initialWorkingSet = process.WorkingSet64;
        var timer = Stopwatch.StartNew();
        await Task.Delay(5000);
        timer.Stop(); process.Refresh(); Native.GetCursorPos(out var endPointer);
        var cpuMilliseconds = (process.TotalProcessorTime - initialCpu).TotalMilliseconds;
        var logicalCores = Environment.ProcessorCount;
        var nativeSpeechModules = process.Modules.Cast<ProcessModule>().Where(m => m.ModuleName.Contains("whisper", StringComparison.OrdinalIgnoreCase) || m.ModuleName.Contains("ggml", StringComparison.OrdinalIgnoreCase)).Select(m => m.ModuleName).ToArray();
        var observation = new
        {
            idle = true, wallMilliseconds = timer.Elapsed.TotalMilliseconds, cpuMilliseconds, logicalCores,
            percentOfOneLogicalCore = cpuMilliseconds / timer.Elapsed.TotalMilliseconds * 100,
            percentOfAllLogicalCores = cpuMilliseconds / timer.Elapsed.TotalMilliseconds / logicalCores * 100,
            initialPrivateBytes = initialPrivate, finalPrivateBytes = process.PrivateMemorySize64, privateByteChange = process.PrivateMemorySize64 - initialPrivate,
            initialWorkingSetBytes = initialWorkingSet, finalWorkingSetBytes = process.WorkingSet64,
            pointerStart = new { startPointer.X, startPointer.Y }, pointerEnd = new { endPointer.X, endPointer.Y }, pointerStayedAtEndpoints = startPointer.X == endPointer.X && startPointer.Y == endPointer.Y,
            nativeSpeechModules, loadedSpeechModel = false, ownInputInjected = false, ringStatic = true, mainWindowVisible = true, warmupMilliseconds = 1000
        };
        results.Add(observation);
        Console.WriteLine("IDLE_OBSERVATION=" + System.Text.Json.JsonSerializer.Serialize(observation));
        Check(nativeSpeechModules.Length == 0, "Fresh idle process has no loaded native Whisper/GGML module");
    }

    private static (bool Available, Native.CursorInfo Info) CursorState()
    {
        var info = new Native.CursorInfo { Size = (uint)Marshal.SizeOf<Native.CursorInfo>() };
        return (Native.GetCursorInfo(ref info), info);
    }
    private static partial class Native
    {
        [StructLayout(LayoutKind.Sequential)] internal struct NativePoint { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] internal struct CursorInfo { public uint Size, Flags; public IntPtr Cursor; public NativePoint Position; }
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool GetCursorPos(out NativePoint point);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool GetCursorInfo(ref CursorInfo info);
        [DllImport("user32.dll", EntryPoint = "FindWindowExW", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string? title);
        [DllImport("magnification.dll", SetLastError = true)] internal static extern bool MagGetFullscreenTransform(out float scale, out int x, out int y);
        [DllImport("magnification.dll", SetLastError = true)] internal static extern bool MagSetFullscreenTransform(float scale, int x, int y);
        [DllImport("magnification.dll", SetLastError = true)] internal static extern bool MagShowSystemCursor(bool show);
        [DllImport("magnification.dll", SetLastError = true)] internal static extern bool MagGetWindowTransform(IntPtr window, [Out] float[] transform);
    }
}
