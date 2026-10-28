using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Jerry.App;
using Jerry.App.Effects;
using Jerry.App.Media;
using Jerry.App.Platform;
using Jerry.App.UI;
using Jerry.Core.Recording;
using Jerry.Core.Settings;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using ListBox = System.Windows.Controls.ListBox;

namespace Jerry.Windows.Tests;

internal static partial class Program
{
    private static string output = "";
    private static readonly List<object> results = [];
    private static readonly List<int> childProcesses = [];
    private static int exitCode;
    private static Action? afterExit;
    [STAThread]
    public static int Main(string[] args)
    {
        Native.SetProcessDpiAwarenessContext(new IntPtr(-4));
        output = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine(Environment.CurrentDirectory, "evidence", "raw", "windows-component-smoke"));
        Directory.CreateDirectory(output);
        using var log = new StreamWriter(Path.Combine(output, "run.log")) { AutoFlush = true };
        Console.SetOut(log); Console.SetError(log);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) => { Console.WriteLine("UNHANDLED=" + e.Exception); exitCode = 1; e.Handled = true; app.Shutdown(1); };
        var mode = args.Length > 1 ? args[1] : "components";
        app.Startup += async (_, _) => { if (mode == "components") await ExecuteAsync(app); else await ExecuteRegressionAsync(app, mode); };
        var appCode = app.Run();
        try { afterExit?.Invoke(); } catch (Exception ex) { Console.WriteLine("POST_EXIT_FAIL=" + ex); exitCode = 1; }
        foreach (var pid in childProcesses)
        {
            try { using var child = Process.GetProcessById(pid); Check(child.HasExited, "Owned process was released: " + pid); }
            catch (ArgumentException) { Console.WriteLine("PROCESS_RELEASED=" + pid); }
            catch (Exception ex) { Console.WriteLine(ex); exitCode = 1; }
        }
        File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { exitCode, appCode, results }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("EXIT=" + exitCode);
        return exitCode;
    }

    private static async Task ExecuteAsync(Application app)
    {
        AppController? controller = null;
        var temporary = Path.Combine(Path.GetTempPath(), "Jerry-Windows-QA-" + Guid.NewGuid().ToString("N"));
        try
        {
            Console.WriteLine("TEMP_SETTINGS=" + temporary);
            controller = new AppController(temporary, background: false, manageWindowsStartup: false);
            Check(controller != null, "AppController constructed and native hooks installed without throwing");
            controller.StatusChanged += status => Console.WriteLine("STATUS=" + status);
            var main = app.Windows.OfType<MainWindow>().Single();
            Check(main.IsVisible, "MainWindow visible");
            await PumpAsync();
            var navigation = Descendants(main).OfType<ListBox>().Single();
            Check(navigation.Items.Count == 15, "All fifteen settings pages exist");
            for (var i = 0; i < navigation.Items.Count; i++)
            {
                navigation.SelectedIndex = i;
                await PumpAsync(180);
                var scroll = Descendants(main).OfType<ScrollViewer>().Where(s => s.Content is StackPanel).OrderByDescending(s => s.ViewportWidth).First();
                Check(Descendants(scroll).OfType<TextBlock>().Any(t => !string.IsNullOrWhiteSpace(t.Text)), "Page " + i + " has rendered text");
                var part = 0;
                for (double top = 0; ; top += Math.Max(100, scroll.ViewportHeight * .85))
                {
                    scroll.ScrollToVerticalOffset(Math.Min(top, scroll.ScrollableHeight));
                    await PumpAsync(70);
                    var image = Snapshot(main);
                    Save(image, $"ui-{i:D2}-{part:D2}.png");
                    Check(VisiblePixels(image) > 10000, "Page " + i + " screenshot " + part + " is nonempty");
                    part++;
                    if (top >= scroll.ScrollableHeight || part > 14) break;
                }
                results.Add(new { page = i, title = OwnControlLabel((ListBoxItem)navigation.Items[i]), parts = part });
            }
            var hud = new RecordingControlWindow(controller);
            hud.Show(); await PumpAsync();
            CheckAffinity(hud, "Recording HUD");
            Save(Snapshot(hud), "recording-hud.png");
            hud.Close();
            controller.Settings.Enabled = false;
            controller.SettingsChanged();
            main.Hide();
            await RenderEffectsAsync();
            await CaptureFixtureAsync();
            Check(File.Exists(Path.Combine(temporary, "settings.json")), "Controller persisted isolated settings");
            Console.WriteLine("ALL_COMPONENT_CHECKS_PASSED");
        }
        catch (Exception ex) { Console.WriteLine("FAIL=" + ex); exitCode = 1; }
        finally
        {
            if (controller != null) controller.Quit();
            else app.Shutdown(exitCode);
        }
    }

    private static async Task RenderEffectsAsync()
    {
        var bounds = new PixelRect(80, 80, 1000, 650);
        await ForcedClickAsync();
        await StyleGroup("spotlight", Enum.GetValues<SpotlightShape>(), (s, scene, value) => { s.Spotlight.Enabled = true; s.Spotlight.Shape = value; });
        await StyleGroup("trail", Enum.GetValues<TrailStyle>(), (s, scene, value) => { s.Trail.Enabled = true; s.Trail.Style = value; s.Trail.FadeMilliseconds = 3000; s.Trail.SpawnIntervalMilliseconds = 1; s.Trail.Width = 12; });
        await StyleGroup("click", Enum.GetValues<ClickStyle>(), (s, scene, value) => { s.Click.Enabled = true; s.Click.Style = value; s.Click.Size = 180; s.Click.Thickness = 8; });
        await StyleGroup("annotation", Enum.GetValues<AnnotationTool>(), (s, scene, value) => { s.Annotation.Enabled = true; s.Annotation.Tool = value; s.Annotation.Width = 10; s.Annotation.ClearOnRelease = false; scene.Drawing = true; });
        await StyleGroup("keys", Enum.GetValues<KeyDisplayStyle>(), (s, scene, value) => { s.Keystrokes.Enabled = true; s.Keystrokes.Style = value; scene.ShowKey("Ctrl + Shift + R"); });
        await StyleGroup("caption", Enum.GetValues<CaptionStyle>(), (s, scene, value) => { s.Captions.LiveEnabled = true; s.Captions.Style = value; scene.SetCaption("Les sous-titres restent locaux sur cet ordinateur."); });

        async Task StyleGroup<T>(string group, T[] styles, Action<AppSettings, EffectScene, T> configure) where T : Enum
        {
            var hashes = new HashSet<string>();
            foreach (var style in styles)
            {
                var settings = new AppSettings(); settings.Ring.Enabled = false;
                var scene = new EffectScene(settings);
                scene.Move(bounds.X + 550, bounds.Y + 270);
                configure(settings, scene, style);
                var window = new EffectWindow(scene, bounds);
                try
                {
                    window.Show(); await PumpAsync(30);
                    if (group == "trail") for (var j = 0; j < 18; j++) { scene.Move(bounds.X + 220 + j * 25, bounds.Y + 290 + Math.Sin(j * .5) * 90); await Task.Delay(3); }
                    if (group == "click") scene.AddClick();
                    if (group == "annotation")
                    {
                        if (settings.Annotation.Tool == AnnotationTool.Text) scene.AddText(new Point(bounds.X + 260, bounds.Y + 240), "Jerry · annotation");
                        else { scene.BeginStroke(new(bounds.X + 250, bounds.Y + 200)); scene.ContinueStroke(new(bounds.X + 390, bounds.Y + 280)); scene.ContinueStroke(new(bounds.X + 620, bounds.Y + 350)); scene.EndStroke(); }
                    }
                    await PumpAsync(group == "click" ? 130 : 110);
                    window.Refresh(); await PumpAsync(35);
                    var bitmap = Snapshot(window);
                    var visible = VisiblePixels(bitmap);
                    Check(visible > 20, group + "/" + style + " actually paints pixels");
                    var hash = Hash(bitmap); hashes.Add(hash);
                    Save(bitmap, $"effect-{group}-{style}.png");
                    results.Add(new { group, style = style.ToString(), visiblePixels = visible, hash });
                }
                finally { window.Close(); }
            }
            // Live caption WordFill/WordPop/SlideUp currently share the static live
            // presentation: the timed export owns word motion. Report this honestly.
            if (group != "caption") Check(hashes.Count == styles.Length, group + " styles produce distinct rendered images");
            else Console.WriteLine("LIVE_CAPTION_DISTINCT_IMAGES=" + hashes.Count + "/" + styles.Length);
        }
    }

    private static async Task CaptureFixtureAsync()
    {
        var bounds = new PixelRect(80, 80, 1200, 700);
        var background = Marker("Fixture background", Color.FromRgb(20, 24, 30), bounds);
        var included = Marker("Included green marker", Colors.Lime, new(bounds.X + 120, bounds.Y + 100, 160, 160));
        var excluded = Marker("Excluded magenta control", Colors.Magenta, new(bounds.X + 450, bounds.Y + 100, 160, 160));
        WindowTools.ExcludeFromCapture(excluded);
        var settings = new AppSettings(); settings.Ring.Enabled = false; settings.Annotation.Enabled = true; settings.Annotation.Tool = AnnotationTool.Rectangle; settings.Annotation.Color = "#00FFFF"; settings.Annotation.Width = 20; settings.Annotation.ClearOnRelease = false;
        var scene = new EffectScene(settings) { Drawing = true };
        scene.BeginStroke(new(bounds.X + 820, bounds.Y + 130)); scene.ContinueStroke(new(bounds.X + 1000, bounds.Y + 310)); scene.EndStroke();
        var effect = new EffectWindow(scene, bounds);
        RecordingService? recorder = null;
        try
        {
            background.Show(); included.Show(); excluded.Show(); effect.Show();
            WindowTools.Position(background, bounds); WindowTools.Position(included, new(bounds.X + 120, bounds.Y + 100, 160, 160)); WindowTools.Position(excluded, new(bounds.X + 450, bounds.Y + 100, 160, 160));
            WindowTools.Position(effect, bounds);
            await PumpAsync(200); effect.Refresh(); await PumpAsync(100);
            CheckAffinity(excluded, "Fixture magenta control");
            Save(Snapshot(excluded), "fixture-control-before-capture.png");
            Save(Snapshot(effect), "fixture-effect-before-capture.png");
            var tools = new ToolchainService();
            var captureSettings = new AppSettings(); captureSettings.Recording.OutputDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Jerry", "qa", Guid.NewGuid().ToString("N")); captureSettings.Recording.Resolution = OutputResolution.Native; captureSettings.Recording.SystemAudio = false; captureSettings.Recording.Microphone = false;
            recorder = new RecordingService(captureSettings, tools); recorder.StatusChanged += s => Console.WriteLine("FIXTURE=" + s);
            await recorder.StartAsync(bounds);
            if (recorder.OwnedProcessId is int pid) childProcesses.Add(pid);
            await Task.Delay(1800);
            var video = await recorder.StopAsync();
            Check(video != null && File.Exists(video), "Fixture recording finalized");
            var image = Path.Combine(output, "encoded-fixture-frame.png");
            var ffmpeg = await tools.EnsureFfmpegAsync();
            await RunTool(ffmpeg, ["-hide_banner", "-nostdin", "-y", "-ss", "0.7", "-i", video!, "-frames:v", "1", "-update", "1", image], "extract-frame.log");
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(image); bitmap.EndInit(); bitmap.Freeze();
            var green = CountColor(bitmap, new(140, 120, 100, 100), (r, g, b) => g > 170 && r < 85 && b < 85);
            var magenta = CountColor(bitmap, new(470, 120, 100, 100), (r, g, b) => r > 170 && b > 170 && g < 85);
            var cyan = CountColor(bitmap, new(800, 110, 220, 220), (r, g, b) => r < 85 && g > 160 && b > 160);
            results.Add(new { encodedFixture = video, green, magenta, cyan });
            Console.WriteLine($"ENCODED_PIXELS green={green}; excludedMagenta={magenta}; effectCyan={cyan}");
            Check(green > 8000, "Encoded video includes known unexcluded green marker");
            Check(magenta < 30, "Encoded video excludes known magenta control region");
            Check(cyan > 3000, "Encoded video includes actual EffectWindow cyan annotation");
            await RunTool(await tools.EnsureFfprobeAsync(), ["-v", "error", "-show_streams", "-show_format", "-of", "json", video!], "fixture-probe.json");
        }
        finally
        {
            if (recorder?.IsRecording == true) await recorder.StopAsync();
            effect.Close(); excluded.Close(); included.Close(); background.Close();
        }
    }

    private static Window Marker(string title, Color color, PixelRect bounds)
    {
        var window = new Window { Title = title, WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, Background = new SolidColorBrush(color), Left = bounds.X, Top = bounds.Y, Width = bounds.Width, Height = bounds.Height, ShowInTaskbar = false, ShowActivated = false, Topmost = true };
        window.SourceInitialized += (_, _) => WindowTools.Position(window, bounds);
        return window;
    }
    private static void CheckAffinity(Window window, string label)
    {
        Check(Native.GetWindowDisplayAffinity(new WindowInteropHelper(window).Handle, out var affinity), label + " affinity query succeeds");
        Check(affinity == 0x11, label + " has WDA_EXCLUDEFROMCAPTURE (actual=" + affinity + ")");
    }
    private static async Task RunTool(string executable, string[] arguments, string logName)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!; childProcesses.Add(process.Id);
        var stderr = process.StandardError.ReadToEndAsync(); var stdout = process.StandardOutput.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        var text = await stdout; var errors = await stderr;
        await File.WriteAllTextAsync(Path.Combine(output, logName), text.Length > 0 ? text : errors);
        Check(process.ExitCode == 0, Path.GetFileName(executable) + " completed: " + errors);
    }
    private static async Task PumpAsync(int milliseconds = 100)
    { await Task.Delay(milliseconds); await Dispatcher.Yield(DispatcherPriority.ApplicationIdle); }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static RenderTargetBitmap Snapshot(FrameworkElement element)
    {
        element.UpdateLayout(); var dpi = VisualTreeHelper.GetDpi(element);
        var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(element.ActualWidth * dpi.DpiScaleX)), Math.Max(1, (int)Math.Ceiling(element.ActualHeight * dpi.DpiScaleY)), 96 * dpi.DpiScaleX, 96 * dpi.DpiScaleY, PixelFormats.Pbgra32);
        bitmap.Render(element); bitmap.Freeze(); return bitmap;
    }
    private static byte[] Pixels(BitmapSource bitmap)
    { var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0); var pixels = new byte[converted.PixelWidth * converted.PixelHeight * 4]; converted.CopyPixels(pixels, converted.PixelWidth * 4, 0); return pixels; }
    private static int VisiblePixels(BitmapSource bitmap)
    { var pixels = Pixels(bitmap); var count = 0; for (var i = 3; i < pixels.Length; i += 4) if (pixels[i] > 10) count++; return count; }
    private static string Hash(BitmapSource bitmap) => Convert.ToHexString(SHA256.HashData(Pixels(bitmap)));
    private static int CountColor(BitmapSource bitmap, Int32Rect roi, Func<byte, byte, byte, bool> predicate)
    { var pixels = Pixels(bitmap); var count = 0; for (var y = roi.Y; y < roi.Y + roi.Height; y++) for (var x = roi.X; x < roi.X + roi.Width; x++) { var p = (y * bitmap.PixelWidth + x) * 4; if (predicate(pixels[p + 2], pixels[p + 1], pixels[p])) count++; } return count; }
    private static void Save(BitmapSource bitmap, string name)
    { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(Path.Combine(output, name)); encoder.Save(file); }
    private static void Check([DoesNotReturnIf(false)] bool condition, string message)
    { Console.WriteLine((condition ? "PASS=" : "FAIL=") + message); if (!condition) throw new InvalidOperationException(message); }
    private static partial class Native
    {
        [DllImport("user32.dll")] internal static extern bool SetProcessDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window, out WindowRect rect);
        [StructLayout(LayoutKind.Sequential)] internal struct WindowRect { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool GetWindowDisplayAffinity(IntPtr window, out uint affinity);
    }
}
