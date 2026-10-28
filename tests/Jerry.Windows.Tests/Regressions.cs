using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using Jerry.App;
using Jerry.App.Effects;
using Jerry.App.UI;
using Jerry.Core.Recording;
using Jerry.Core.Settings;
using Application = System.Windows.Application;

namespace Jerry.Windows.Tests;

internal static partial class Program
{
    private static async Task ExecuteRegressionAsync(Application app, string mode)
    {
        AppController? controller = null;
        try
        {
            if (mode == "native-services")
            {
                await NativeServicesAsync(); app.Shutdown(); return;
            }
            var directory = Path.Combine(Path.GetTempPath(), "Jerry-Regression-" + Guid.NewGuid().ToString("N"));
            controller = new AppController(directory, background: false);
            controller.StatusChanged += text => Console.WriteLine("STATUS=" + text);
            var main = app.Windows.OfType<MainWindow>().Single();
            controller.Settings.Captions.AutoOpenAfterRecording = false;
            controller.Settings.Recording.SystemAudio = controller.Settings.Recording.Microphone = false;
            controller.Settings.Recording.Resolution = OutputResolution.P720;
            controller.Settings.Recording.OutputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Jerry", "qa", Guid.NewGuid().ToString("N"));
            controller.Settings.CountdownForMode(mode);
            controller.SettingsChanged();
            if (mode == "tray-icon")
            {
                await TrayIconAsync(controller, main); controller.Quit(); return;
            }
            if (mode == "minimal-menu")
            {
                await QuickMenuChecksAsync(controller); controller.Quit(); return;
            }
            if (mode == "minimal-ui")
            {
                await MinimalUiAsync(controller, main); controller.Quit(); return;
            }
            if (mode == "quit-editor-operation")
            {
                await QuitEditorOperationAsync(controller); return;
            }
            if (mode == "idle")
            {
                await MeasureIdleAsync(controller); controller.Quit(); return;
            }
            if (mode == "render-regressions")
            {
                await ForcedClickAsync();
                await FocusReenableAsync(controller, main);
                Console.WriteLine("RENDER_REGRESSIONS_PASSED");
                controller.Quit();
                return;
            }
            if (mode == "close-no-tray")
            {
                controller.Settings.MinimizeToTray = false;
                controller.SettingsChanged();
                afterExit = () =>
                {
                    Check(controller.IsShuttingDown, "Closing without tray invoked controller shutdown");
                    Check(!main.IsVisible && !controller.IsRecording && !controller.IsRecordingTransition, "Closing without tray released the accessible application window and recording state");
                };
                main.Close();
                await PumpAsync(800);
                Check(false, "Closing without tray must exit Application.Run without leaving an inaccessible process");
            }
            if (mode is not ("quit-countdown" or "quit-start" or "session-countdown" or "session-recording")) throw new ArgumentException("Unknown regression mode: " + mode);
            var start = controller.ToggleRecordingAsync();
            if (mode == "session-recording")
            {
                await start.WaitAsync(TimeSpan.FromSeconds(20));
                Check(controller.IsRecording, "Actual controller recording started before session-end test");
                await Task.Delay(650);
            }
            else
            {
                await Task.Delay(mode == "quit-start" ? 20 : 150);
                Check(controller.IsRecordingTransition && !start.IsCompleted, "Shutdown begins during pending startup/countdown");
            }
            var captureDirectory = controller.Settings.Recording.OutputDirectory;
            var stopwatch = Stopwatch.StartNew();
            if (mode.StartsWith("quit-", StringComparison.Ordinal))
            {
                afterExit = () =>
                {
                    Check(start.IsCompleted && !controller.IsRecordingTransition, "Quit awaited the pending recording transition");
                    Check(!controller.IsRecording, "Quit left no active recording");
                    CollectOwnedProcesses(captureDirectory);
                    Console.WriteLine("SHUTDOWN_MS=" + stopwatch.ElapsedMilliseconds);
                };
                controller.Quit();
                return;
            }
            controller.PrepareForSessionEnd();
            Console.WriteLine("SESSION_END_MS=" + stopwatch.ElapsedMilliseconds);
            Check(stopwatch.Elapsed < TimeSpan.FromSeconds(8), "Session-end service shutdown finished within its bounded dispatcher pump");
            Check(start.IsCompleted && !controller.IsRecordingTransition && !controller.IsRecording, "Session-end completed pending transition and stopped capture");
            Check(controller.IsShuttingDown && !main.IsVisible, "Session-end disposed UI and controller");
            if (mode == "session-recording") Check(controller.LastRecording != null && File.Exists(controller.LastRecording), "Session-end finalized established recording into a real MP4");
            CollectOwnedProcesses(captureDirectory);
            Console.WriteLine("SESSION_REGRESSION_PASSED=" + mode);
            app.Shutdown();
        }
        catch (Exception ex)
        {
            Console.WriteLine("REGRESSION_FAIL=" + ex); exitCode = 1;
            try { controller?.Dispose(); } catch (Exception cleanup) { Console.WriteLine("CLEANUP=" + cleanup); }
            app.Shutdown(1);
        }
    }

    private static void CollectOwnedProcesses(string directory)
    {
        if (!Directory.Exists(directory)) return;
        foreach (var manifest in Directory.EnumerateFiles(directory, "owned-process.json", SearchOption.AllDirectories))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifest));
            childProcesses.Add(document.RootElement.GetProperty("pid").GetInt32());
        }
        Console.WriteLine("OWNED_PID_COUNT=" + childProcesses.Count);
    }

    private static async Task ForcedClickAsync()
    {
        var settings = new AppSettings(); settings.Ring.Enabled = false; settings.Click.Enabled = false; settings.Click.Size = 160;
        var scene = new EffectScene(settings); scene.Move(360, 260);
        scene.AddClick();
        Check(!scene.NeedsAnimation, "Ordinary disabled click creates no animation");
        var window = new EffectWindow(scene, new PixelRect(80, 80, 650, 450));
        try
        {
            window.Show(); await PumpAsync(40);
            Check(VisiblePixels(Snapshot(window)) == 0, "Disabled ordinary click paints no effect");
            scene.AddClick(force: true);
            Check(scene.NeedsAnimation, "Forced click animates even while automatic clicks are disabled");
            await PumpAsync(130); window.Refresh(); await PumpAsync(25);
            var bitmap = Snapshot(window); Save(bitmap, "forced-click-disabled-setting.png");
            Check(VisiblePixels(bitmap) > 20, "Forced click paints actual effect pixels");
            scene.Tick(Environment.TickCount64 + 1000);
            Check(!scene.NeedsAnimation, "Forced click animation expires cleanly");
        }
        finally { window.Close(); }
    }

    private static async Task FocusReenableAsync(AppController controller, MainWindow main)
    {
        controller.Settings.Enabled = true; controller.Settings.Ring.Enabled = false;
        controller.Settings.Focus.Enabled = true; controller.Settings.Focus.Opacity = .8;
        main.Show(); main.Activate(); controller.SettingsChanged(); await PumpAsync(250);
        var handle = Native.GetForegroundWindow();
        Check(handle != IntPtr.Zero && Native.GetWindowRect(handle, out var rectangle), "Actual foreground physical rectangle is available without input automation");
        Native.GetWindowRect(handle, out rectangle);
        Console.WriteLine($"FOREGROUND={handle}; rect={rectangle.Left},{rectangle.Top},{rectangle.Right},{rectangle.Bottom}");
        var overlay = Application.Current.Windows.OfType<EffectWindow>().OrderByDescending(w => w.ActualWidth).First();
        var before = Snapshot(overlay); Save(before, "focus-enabled.png");
        var left = Math.Max(0, rectangle.Left); var top = Math.Max(0, rectangle.Top);
        var right = Math.Min(before.PixelWidth, rectangle.Right); var bottom = Math.Min(before.PixelHeight, rectangle.Bottom);
        Check(right > left && bottom > top, "Foreground rectangle intersects the tested real monitor");
        var centerX = (left + right) / 2; var centerY = (top + bottom) / 2;
        Check(Alpha(before, centerX, centerY) < 10, "Initial focus leaves foreground cutout transparent");
        var outside = new[] { (X: 5, Y: 5), (X: before.PixelWidth - 6, Y: 5), (X: 5, Y: before.PixelHeight - 6), (X: before.PixelWidth - 6, Y: before.PixelHeight - 6) }
            .Where(p => p.X < rectangle.Left || p.X >= rectangle.Right || p.Y < rectangle.Top || p.Y >= rectangle.Bottom).ToArray();
        if (outside.Length > 0) Check(Alpha(before, outside[0].X, outside[0].Y) > 100, "Initial focus darkens an available outside region");
        else Console.WriteLine("FOCUS_OUTSIDE_REGION=unavailable because foreground covers real monitor");
        controller.Settings.Focus.Enabled = false; controller.SettingsChanged(); await PumpAsync(40);
        Check(Alpha(Snapshot(overlay), 5, 5) < 10, "Focus disable removes dimming");
        Check(Native.GetForegroundWindow() == handle, "Foreground stays unchanged while toggling focus");
        controller.Settings.Focus.Enabled = true; controller.SettingsChanged(); await PumpAsync(180);
        var after = Snapshot(overlay); Save(after, "focus-reenabled-same-foreground.png");
        Check(Alpha(after, centerX, centerY) < 10, "Focus reenable restores the same foreground cutout");
        if (outside.Length > 0) Check(Alpha(after, outside[0].X, outside[0].Y) > 100, "Focus reenable restores outside dimming");
    }
    private static byte Alpha(System.Windows.Media.Imaging.BitmapSource bitmap, int x, int y) => Pixels(bitmap)[(y * bitmap.PixelWidth + x) * 4 + 3];
}

internal static class RegressionSettings
{
    public static void CountdownForMode(this AppSettings settings, string mode)
        => settings.Recording.CountdownSeconds = mode is "quit-start" or "session-recording" ? 0 : 3;
}
