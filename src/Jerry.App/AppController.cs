using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Jerry.App.Captions;
using Jerry.App.Effects;
using Jerry.App.Media;
using Jerry.App.Platform;
using Jerry.App.UI;
using Jerry.Core.Input;
using Jerry.Core.Recording;
using Jerry.Core.Settings;
using Forms = System.Windows.Forms;

namespace Jerry.App;

public sealed class AppController : IDisposable
{
    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private readonly SettingsStore store;
    private readonly ToolchainService tools = new();
    private readonly RecordingService recording;
    private readonly WebcamService webcam = new();
    private readonly CaptionService captions;
    private readonly GlobalInput input;
    private readonly HotkeyManager hotkeys = new();
    private readonly MagnificationService magnification = new();
    private readonly ClickSoundService sound;
    private readonly EffectScene scene;
    private readonly ShakeDetector shake = new();
    private readonly KeyTapGesture tap = new();
    private readonly EffectActivation activation;
    private readonly ZoomMotion zoomMotion = new();
    private readonly List<EffectWindow> overlays = new();
    private readonly DispatcherTimer clock, saveTimer, focusTimer, holdTimer;
    private readonly SemaphoreSlim recordTransition = new(1, 1);
    private readonly Forms.NotifyIcon tray;
    private readonly System.Drawing.Icon applicationIcon = AppIcon.Create();
    private readonly System.Drawing.Icon trayIcon = AppIcon.CreateTray();
    private readonly MainWindow main;
    private RecordingControlWindow? recordingHud;
    private AnnotationToolbar? toolbar;
    private QuickMenuWindow? quickMenu;
    private CameraOverlayWindow? cameraWindow;
    private CancellationTokenSource? recordCancellation;
    private bool disposed, drawingStroke, textInput, heldSpotlight, originalSpotlight;
    private bool zoomActive, zoomFailureReported;
    private bool cursorFailureReported;
    private long autoZoomUntil;
    private bool zoomCycle;
    private Task? shutdownTask;
    private Point spotlightOrigin;
    private string shortcutSignature = "";
    private bool startupValue;
    private int busyCount;
    private nint foreground;
    private Rect? foregroundBounds;
    public AppSettings Settings { get; }
    public bool IsRecording => recording.IsRecording;
    public bool IsBusy => busyCount > 0;
    public bool IsRecordingTransition => recordTransition.CurrentCount == 0;
    public bool DrawingMode => scene.Drawing;
    public bool IsZoomActive => zoomActive;
    public bool IsShuttingDown { get; private set; }
    public string Status { get; private set; } = "Prêt";
    public string? LastRecording { get; private set; }
    public event Action? StateChanged;
    public event Action<string>? StatusChanged;

    public AppController(string dataDirectory, bool background)
    {
        Directory.CreateDirectory(dataDirectory);
        store = new SettingsStore(Path.Combine(dataDirectory, "settings.json"));
        Settings = store.Load();
        Settings.Camera.Enabled = false;
        Settings.Captions.LiveEnabled = false;
        activation = new(Settings);
        startupValue = Settings.LaunchAtLogin;
        recording = new(Settings, tools); captions = new(Settings, tools);
        sound = new(dataDirectory);
        scene = new(Settings);
        input = new(dispatcher);
        input.Moved += PointerMoved; input.Pressed += PointerPressed; input.Released += PointerReleased;
        input.Scrolled += tap.Invalidate;
        input.KeyPressed += KeyDown; input.KeyReleased += KeyUp;
        hotkeys.Triggered += Toggle;
        clock = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(1000d / Settings.UpdateRate) };
        clock.Tick += (_, _) => Animate();
        saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        saveTimer.Tick += (_, _) => { saveTimer.Stop(); TrySave(); };
        focusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        focusTimer.Tick += (_, _) => RefreshFocus();
        holdTimer = new DispatcherTimer();
        holdTimer.Tick += (_, _) =>
        {
            holdTimer.Stop(); originalSpotlight = Settings.Spotlight.Enabled; heldSpotlight = true;
            Settings.Spotlight.Enabled = true; spotlightOrigin = scene.Cursor; Refresh();
        };
        recording.StatusChanged += SetStatus;
        recording.RecordingChanged += running => Dispatch(() =>
        {
            if (!running)
            {
                recordingHud?.Hide();
                if (recording.LastOutputPath is { } output && LastRecording != output)
                {
                    LastRecording = output;
                    if (Settings.Captions.AutoOpenAfterRecording && !IsShuttingDown) ShowCaptionEditor(output);
                }
                if (!IsRecordingTransition) { recordingHud?.Close(); recordingHud = null; }
            }
            RebuildTray(); StateChanged?.Invoke();
        });
        webcam.StatusChanged += SetStatus;
        webcam.FrameReady += frame => Dispatch(() => cameraWindow?.UpdateFrame(frame));
        webcam.RunningChanged += running => Dispatch(() =>
        {
            if (!running && !IsShuttingDown) { Settings.Camera.Enabled = false; cameraWindow?.Hide(); SettingsChanged(); }
        });
        captions.StatusChanged += SetStatus;
        captions.LiveText += text => Dispatch(() => { scene.SetCaption(text); Refresh(); });
        captions.LiveError += ex => Dispatch(() => { Settings.Captions.LiveEnabled = false; SetStatus("Sous-titres live : " + ex.Message); SettingsChanged(); });
        main = new(this);
        tray = new Forms.NotifyIcon { Icon = trayIcon, Text = "Jerry — effets de curseur", Visible = true };
        main.Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(applicationIcon.Handle, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
        tray.DoubleClick += (_, _) => Dispatch(ShowSettings);
        tray.MouseClick += (_, e) => { if (e.Button is Forms.MouseButtons.Left or Forms.MouseButtons.Right) Dispatch(ShowQuickMenu); };
        tray.BalloonTipClicked += (_, _) => Dispatch(OpenOutputFolder);
        RebuildTray();
        RecreateOverlays();
        Native.GetCursorPos(out var cursor); scene.Move(cursor.X, cursor.Y);
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += DisplayChanged;
        Microsoft.Win32.SystemEvents.SessionSwitch += SessionSwitch;
        ApplySettings();
        if (store.RecoveryMessage != null) SetStatus(store.RecoveryMessage);
        if (Directory.Exists(Settings.Recording.OutputDirectory))
            LastRecording = Directory.EnumerateFiles(Settings.Recording.OutputDirectory, "*.mp4").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        if (!background) ShowSettings();
    }

    public void ShowSettings()
    { quickMenu?.Hide(); if (!main.IsVisible) main.Show(); if (main.WindowState == WindowState.Minimized) main.WindowState = WindowState.Normal; main.Activate(); }

    public void ShowQuickMenu()
    {
        if (disposed || IsShuttingDown) return;
        quickMenu ??= new QuickMenuWindow(this, main.Icon);
        if (quickMenu.IsVisible) { quickMenu.Hide(); return; }
        quickMenu.ShowAtCursor();
    }

    public void SettingsChanged()
    {
        if (disposed) return;
        SettingsStore.Validate(Settings);
        if (activation.Apply(Settings).SpotlightActivated) spotlightOrigin = scene.Cursor;
        ApplySettings(); saveTimer.Stop(); saveTimer.Start(); StateChanged?.Invoke();
    }
    private void ApplySettings()
    {
        clock.Interval = TimeSpan.FromMilliseconds(1000d / Math.Clamp(Settings.UpdateRate, 15, 120));
        var signature = string.Join(";", Settings.Hotkeys.OrderBy(x => x.Key).Select(x => x.Key + "=" + x.Value));
        if (signature != shortcutSignature)
        {
            shortcutSignature = signature;
            var errors = hotkeys.Apply(Settings.Hotkeys);
            if (errors.Count > 0) SetStatus(string.Join("  ", errors));
        }
        if (startupValue != Settings.LaunchAtLogin)
        {
            try { StartupService.SetEnabled(Settings.LaunchAtLogin); startupValue = Settings.LaunchAtLogin; }
            catch (Exception ex) { Settings.LaunchAtLogin = startupValue; SetStatus("Démarrage Windows : " + ex.Message); }
        }
        input.HoldKeyToCapture = Settings.Enabled && Settings.Annotation.Enabled ? GlobalInput.KeyCode(Settings.Annotation.HoldKey) : 0;
        if (!Settings.Enabled || !Settings.Annotation.Enabled) SetDrawing(false);
        if (!Settings.Enabled || !Settings.Zoom.Enabled) { zoomActive = false; autoZoomUntil = 0; }
        if (Settings.Enabled && Settings.Focus.Enabled) { focusTimer.Start(); RefreshFocus(); } else { focusTimer.Stop(); scene.FocusBounds = null; }
        if (!Settings.Enabled) magnification.ShowLoupe(false, 0, 0, 0, 1, []);
        if (cameraWindow != null)
        {
            if (Settings.Enabled && Settings.Camera.Enabled) { cameraWindow.MoveToMonitor(CursorMonitor()); if (!cameraWindow.IsVisible) cameraWindow.Show(); }
            else cameraWindow.Hide();
        }
        RebuildTray(); Refresh();
    }
    public void Toggle(string feature)
    {
        if (IsShuttingDown) return;
        switch (feature)
        {
            case "master": Settings.Enabled = !Settings.Enabled; break;
            case "spotlight": Settings.Spotlight.Enabled = !Settings.Spotlight.Enabled; spotlightOrigin = scene.Cursor; break;
            case "ring": Settings.Ring.Enabled = !Settings.Ring.Enabled; break;
            case "trail": Settings.Trail.Enabled = !Settings.Trail.Enabled; break;
            case "click": Settings.Click.Enabled = !Settings.Click.Enabled; break;
            case "sound": Settings.Sound.Enabled = !Settings.Sound.Enabled; break;
            case "keystrokes": Settings.Keystrokes.Enabled = !Settings.Keystrokes.Enabled; break;
            case "locate": Settings.Locate.Enabled = !Settings.Locate.Enabled; break;
            case "annotation": ToggleDrawing(); return;
            case "clear": ClearDrawing(); return;
            case "flash": Settings.Enabled = true; scene.AddClick(force: true); Refresh(); return;
            case "zoom": Settings.Zoom.Enabled = true; zoomActive = !zoomActive; autoZoomUntil = 0; break;
            case "zoom-in": Settings.Zoom.Scale = Math.Min(5, Settings.Zoom.Scale + .25); Settings.Zoom.Enabled = zoomActive = true; break;
            case "zoom-out": Settings.Zoom.Scale = Math.Max(1, Settings.Zoom.Scale - .25); break;
            case "loupe": Settings.Zoom.LoupeEnabled = !Settings.Zoom.LoupeEnabled; break;
            case "focus": Settings.Focus.Enabled = !Settings.Focus.Enabled; break;
            case "record": _ = ToggleRecordingAsync(); return;
            case "camera": _ = SetCameraEnabledAsync(!Settings.Camera.Enabled); return;
            case "captions": _ = SetLiveCaptionsAsync(!Settings.Captions.LiveEnabled); return;
            default: return;
        }
        if (feature != "master") Settings.Enabled = true;
        SettingsChanged();
    }

    private void PointerMoved(PointerInput p)
    {
        if (IsShuttingDown || disposed) return;
        scene.Move(p.X, p.Y);
        if (Settings.Enabled)
        {
            if (Settings.Locate.Enabled && shake.Observe(p.X, p.Y, p.Milliseconds, Settings.Locate.Sensitivity)) scene.ShowLocate();
            if (Settings.Spotlight.Enabled && Settings.Spotlight.TurnOffWhenMoving && (scene.Cursor - spotlightOrigin).Length > Settings.Spotlight.MoveDistance)
            { Settings.Spotlight.Enabled = false; SettingsChanged(); }
            if (drawingStroke) scene.ContinueStroke(scene.Cursor);
        }
        Refresh();
    }
    private void PointerPressed(PointerInput p)
    {
        if (IsShuttingDown || disposed) return;
        quickMenu?.DismissIfOutside(p.X, p.Y);
        scene.Move(p.X, p.Y); tap.Invalidate();
        if (!Settings.Enabled) return;
        if (Settings.Click.Enabled && Settings.Click.Automatic) scene.AddClick(p.Button);
        if (Settings.Sound.Enabled)
        {
            try { sound.Play(Settings.Sound); }
            catch (Exception ex) { Settings.Sound.Enabled = false; SetStatus(ex.Message); }
        }
        if (Settings.Zoom.Enabled && Settings.Zoom.AutoOnClick) autoZoomUntil = Environment.TickCount64 + Settings.Zoom.AutoDurationMilliseconds;
        if (scene.Drawing && !OwnWindowAt(p.X, p.Y) && p.Button == 1)
        {
            if (Settings.Annotation.Tool == AnnotationTool.Text) PromptText(scene.Cursor);
            else { drawingStroke = true; scene.BeginStroke(scene.Cursor); }
        }
        Refresh();
    }
    private void PointerReleased(PointerInput p)
    { if (IsShuttingDown || disposed) return; if (drawingStroke) { scene.ContinueStroke(new Point(p.X, p.Y)); scene.EndStroke(); drawingStroke = false; } Refresh(); }

    private void KeyDown(KeyboardInput p)
    {
        if (textInput || IsShuttingDown || disposed) return;
        var hold = GlobalInput.KeyCode(Settings.Spotlight.HoldKey);
        var trigger = GlobalInput.KeyCode(Settings.Click.TriggerKey);
        if (!p.Repeat && p.Key == trigger && trigger != 0) tap.Begin(Environment.TickCount64, p.OtherKeysHeld);
        else if (p.Key != trigger) tap.Invalidate();
        if (p.Key == 27)
        {
            if (scene.Drawing) SetDrawing(false);
            zoomActive = false; autoZoomUntil = 0; magnification.StopZoom(); Refresh();
        }
        if (!Settings.Enabled) return;
        if (!p.Repeat && p.Key == hold && hold != 0)
        { holdTimer.Interval = TimeSpan.FromMilliseconds(Settings.Spotlight.HoldDelayMilliseconds); holdTimer.Start(); }
        if (!p.Repeat && Settings.Annotation.Enabled && p.Key == GlobalInput.KeyCode(Settings.Annotation.HoldKey) && p.Key != 0) SetDrawing(true);
        if (scene.Drawing && !p.Repeat)
        {
            if (p.Control && p.Key == 0x5A) UndoDrawing();
            else if (p.Key == 0x2E) ClearDrawing();
            else if (!p.Control && !p.Alt && !p.Windows)
            {
                var tool = p.Key switch { 0x46 => AnnotationTool.Freehand, 0x41 => AnnotationTool.Arrow, 0x52 => AnnotationTool.Rectangle, 0x43 => AnnotationTool.Circle, 0x4C => AnnotationTool.Line, 0x48 => AnnotationTool.Highlighter, 0x54 => AnnotationTool.Text, 0x4E => AnnotationTool.Number, _ => (AnnotationTool?)null };
                if (tool.HasValue) { Settings.Annotation.Tool = tool.Value; StateChanged?.Invoke(); }
            }
        }
        if (Settings.Keystrokes.Enabled && p.Key is not (0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5 or 0x5B or 0x5C))
        {
            if ((!Settings.Keystrokes.ModifiersOnly || p.Control || p.Alt || p.Shift || p.Windows)
                && (p.Key != 32 || Settings.Keystrokes.ShowSpace) && (p.Key is not (8 or 46) || Settings.Keystrokes.ShowDelete) && (p.Key != 13 || Settings.Keystrokes.ShowReturn))
            {
                var text = KeyText(p, Settings.Keystrokes.ModifiersOnly);
                if (text.Length > 0) scene.ShowKey(text, !p.Control && !p.Alt && !p.Windows && text.Length == 1);
            }
        }
        Refresh();
    }
    private void KeyUp(KeyboardInput p)
    {
        if (IsShuttingDown || disposed) return;
        var hold = GlobalInput.KeyCode(Settings.Spotlight.HoldKey);
        var wasHeldSpotlight = heldSpotlight && p.Key == hold;
        if (p.Key == hold && hold != 0)
        { holdTimer.Stop(); if (heldSpotlight) Settings.Spotlight.Enabled = originalSpotlight; heldSpotlight = false; }
        if (p.Key == GlobalInput.KeyCode(Settings.Annotation.HoldKey) && p.Key != 0) SetDrawing(false);
        if (p.Key == GlobalInput.KeyCode(Settings.Click.TriggerKey))
        {
            if (tap.Release(Environment.TickCount64) && Settings.Enabled && Settings.Click.Enabled && !wasHeldSpotlight) scene.AddClick();
        }
        Refresh();
    }

    public void ToggleDrawing() => SetDrawing(!scene.Drawing);
    private void SetDrawing(bool enabled)
    {
        if (scene.Drawing == enabled) return;
        if (enabled) { Settings.Enabled = true; Settings.Annotation.Enabled = true; }
        if (drawingStroke) { scene.EndStroke(); drawingStroke = false; }
        scene.Drawing = enabled; input.CaptureMouse = input.CaptureDrawingKeys = enabled;
        if (enabled)
        {
            toolbar ??= new AnnotationToolbar(this);
            if (!toolbar.IsVisible) toolbar.Show();
        }
        else toolbar?.Hide();
        Refresh(); StateChanged?.Invoke();
    }
    public void ClearDrawing() { scene.Clear(); Refresh(); }
    public void UndoDrawing() { scene.Undo(); Refresh(); }
    private void PromptText(Point point)
    {
        textInput = true;
        try
        {
            var text = new TextBox { Text = Settings.Annotation.Text, Margin = new Thickness(18), MinWidth = 280, AcceptsReturn = false };
            var prompt = new Window { Title = "Texte de l’annotation", Width = 360, Height = 135, WindowStartupLocation = WindowStartupLocation.CenterScreen, ResizeMode = ResizeMode.NoResize, Content = text, Topmost = true };
            WindowTools.ExcludeFromCapture(prompt);
            text.KeyDown += (_, e) => { if (e.Key == Key.Enter) prompt.DialogResult = true; else if (e.Key == Key.Escape) prompt.DialogResult = false; };
            prompt.Loaded += (_, _) => { text.Focus(); text.SelectAll(); };
            if (prompt.ShowDialog() == true && !string.IsNullOrWhiteSpace(text.Text)) { Settings.Annotation.Text = text.Text; scene.AddText(point, text.Text); }
        }
        finally { textInput = false; }
    }

    private void Animate()
    {
        var active = scene.Tick(Environment.TickCount64);
        RefreshWindows();
        var motion = UpdateMagnification();
        if (!active && autoZoomUntil <= Environment.TickCount64 && !motion) clock.Stop();
    }
    private void Refresh()
    {
        RefreshWindows(); var motion = UpdateMagnification();
        if (scene.NeedsAnimation || autoZoomUntil > Environment.TickCount64 || motion) clock.Start();
    }
    private void RefreshWindows() { foreach (var overlay in overlays) overlay.Refresh(); }
    private bool UpdateMagnification()
    {
        var now = Environment.TickCount64;
        var active = Settings.Enabled && Settings.Zoom.Enabled && (zoomActive || autoZoomUntil > now);
        var moving = false;
        if (active && !zoomCycle) { zoomMotion.Reset(scene.Cursor.X, scene.Cursor.Y, now); zoomCycle = true; }
        if (zoomCycle)
        {
            var scale = active ? (autoZoomUntil > now ? Settings.Zoom.AutoScale : Settings.Zoom.Scale) : 1;
            var frame = zoomMotion.Advance(scale, scene.Cursor.X, scene.Cursor.Y, now, Settings.Zoom.AnimationMilliseconds, Settings.Zoom.MovementSpeed);
            moving = frame.IsMoving;
            if (!active && !moving) { magnification.StopZoom(); zoomCycle = false; }
            else if (!magnification.Zoom(frame.Scale, (int)frame.X, (int)frame.Y, CursorMonitor()) && !zoomFailureReported)
            { zoomFailureReported = true; SetStatus("Le grossissement Windows n’a pas pu être activé. Les autres effets restent utilisables."); }
        }
        var hideCursor = Settings.Enabled && Settings.Spotlight.Enabled && Settings.Spotlight.HideCursor;
        if (!magnification.HideCursor(hideCursor) && hideCursor && !cursorFailureReported)
        { cursorFailureReported = true; SetStatus("Windows n’a pas accepté le masquage du curseur. Désactivez puis réactivez l’option après avoir vérifié vos outils de loupe."); }
        input.CaptureEscape = active || zoomCycle || scene.Drawing;
        magnification.ShowLoupe(Settings.Enabled && Settings.Zoom.LoupeEnabled, (int)scene.Cursor.X, (int)scene.Cursor.Y, (int)Settings.Zoom.LoupeDiameter, Settings.Zoom.LoupeScale, overlays.Select(w => new WindowInteropHelper(w).Handle));
        return moving;
    }
    private void RefreshFocus()
    {
        var hwnd = Native.GetForegroundWindow();
        Rect? rectangle = null;
        if (Native.GetWindowRect(hwnd, out var rect) && rect.Right > rect.Left && rect.Bottom > rect.Top)
            rectangle = new Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        if (hwnd == foreground && rectangle == foregroundBounds && scene.FocusBounds == rectangle) return;
        foreground = hwnd; foregroundBounds = rectangle; scene.FocusBounds = rectangle; Refresh();
    }
    private PixelRect CursorMonitor()
    {
        var bounds = Forms.Screen.FromPoint(new System.Drawing.Point((int)scene.Cursor.X, (int)scene.Cursor.Y)).Bounds;
        return new(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }
    private void RecreateOverlays()
    {
        foreach (var overlay in overlays) overlay.Close(); overlays.Clear();
        foreach (var screen in Forms.Screen.AllScreens)
        {
            var b = screen.Bounds; var overlay = new EffectWindow(scene, new(b.X, b.Y, b.Width, b.Height));
            overlays.Add(overlay); overlay.Show();
        }
    }

    public async Task ToggleRecordingAsync()
    {
        if (recordCancellation != null && !recording.IsRecording) { recordCancellation.Cancel(); return; }
        if (!await recordTransition.WaitAsync(0)) return;
        busyCount++; StateChanged?.Invoke();
        try
        {
            if (recording.IsRecording)
            {
                SetStatus("Finalisation de la prise…");
                LastRecording = await recording.StopAsync();
                if (LastRecording != null)
                {
                    SetStatus("Prise enregistrée : " + LastRecording);
                    tray.ShowBalloonTip(4000, "Prise enregistrée", "Votre vidéo est prête. Cliquez pour ouvrir le dossier, ou choisissez Sous-titrer la dernière prise.", Forms.ToolTipIcon.Info);
                }
            }
            else
            {
                recordCancellation = new();
                var token = recordCancellation.Token;
                await tools.EnsureFfmpegAsync(cancellationToken: token);
                var bounds = CursorMonitor();
                recordingHud = new RecordingControlWindow(this); recordingHud.Show();
                for (var remaining = Settings.Recording.CountdownSeconds; remaining > 0; remaining--)
                { recordingHud.SetCountdown(remaining); await Task.Delay(1000, token); }
                recordingHud.SetCountdown(0);
                await recording.StartAsync(bounds, token);
                recordingHud.SetCountdown(null);
                SetStatus("Enregistrement en cours — les effets restent modifiables.");
            }
        }
        catch (OperationCanceledException) { SetStatus("Départ de l’enregistrement annulé."); }
        catch (Exception ex) { SetStatus("Capture : " + ex.Message); }
        finally
        {
            recordCancellation?.Dispose(); recordCancellation = null;
            busyCount--; recordTransition.Release(); StateChanged?.Invoke(); RebuildTray();
            if (!recording.IsRecording) { recordingHud?.Close(); recordingHud = null; }
        }
    }
    public async Task SetCameraEnabledAsync(bool enabled)
    {
        try
        {
            if (enabled)
            {
                Settings.Enabled = true;
                cameraWindow ??= new CameraOverlayWindow(Settings);
                cameraWindow.PositionChanged -= SettingsChanged; cameraWindow.PositionChanged += SettingsChanged;
                cameraWindow.MoveToMonitor(CursorMonitor());
                await webcam.StartAsync(Settings.Camera.DeviceId);
                Settings.Camera.Enabled = true; cameraWindow.Show();
            }
            else { await webcam.StopAsync(); Settings.Camera.Enabled = false; cameraWindow?.Hide(); }
        }
        catch (Exception ex) { Settings.Camera.Enabled = false; SetStatus("Webcam : " + ex.Message); }
        SettingsChanged();
    }
    public async Task SetLiveCaptionsAsync(bool enabled)
    {
        busyCount++; StateChanged?.Invoke();
        try
        {
            if (enabled) { Settings.Enabled = true; await captions.StartLiveAsync(); Settings.Captions.LiveEnabled = true; }
            else { await captions.StopLiveAsync(); Settings.Captions.LiveEnabled = false; scene.SetCaption(""); }
        }
        catch (Exception ex) { Settings.Captions.LiveEnabled = false; SetStatus("Sous-titres : " + ex.Message); }
        finally { busyCount--; SettingsChanged(); }
    }
    public Task EnsureToolsAsync() => RunOperationAsync(() => tools.EnsureFfmpegAsync(new Progress<double>(p => SetStatus($"Installation locale de FFmpeg : {p:P0}"))));
    public Task EnsureCaptionModelAsync() => RunOperationAsync(() => captions.EnsureModelAsync(new Progress<double>(p => SetStatus($"Modèle local : {p:P0}"))));
    private async Task RunOperationAsync(Func<Task> action)
    {
        busyCount++; StateChanged?.Invoke();
        try { await action(); SetStatus("Préparation locale terminée."); }
        catch (Exception ex) { SetStatus(ex.Message); }
        finally { busyCount--; StateChanged?.Invoke(); }
    }
    public void ShowCaptionEditor(string? file = null)
    {
        if (IsShuttingDown || disposed) return;
        var editor = new CaptionEditorWindow(Settings, captions, tools, file);
        editor.Show();
    }
    public void OpenOutputFolder()
    { Directory.CreateDirectory(Settings.Recording.OutputDirectory); Process.Start(new ProcessStartInfo("explorer.exe", "\"" + Settings.Recording.OutputDirectory + "\"") { UseShellExecute = true }); }
    public void ResetSettings()
    {
        SetDrawing(false); scene.Clear(); zoomActive = false; autoZoomUntil = 0;
        var fresh = new AppSettings();
        foreach (var property in typeof(AppSettings).GetProperties()) property.SetValue(Settings, property.GetValue(fresh));
        _ = SetCameraEnabledAsync(false); _ = SetLiveCaptionsAsync(false); SettingsChanged();
        SetStatus("Réglages réinitialisés. Aucun raccourci global n’est imposé.");
    }
    private void RebuildTray()
    {
        if (tray == null) return;
        tray.Text = IsRecording ? "Jerry — enregistrement en cours" : Settings.Enabled ? "Jerry — effets activés" : "Jerry — effets désactivés";
    }
    private void DisplayChanged(object? sender, EventArgs args) => Dispatch(async () =>
    { recordCancellation?.Cancel(); if (recording.IsRecording) await ToggleRecordingAsync(); RecreateOverlays(); cameraWindow?.MoveToMonitor(CursorMonitor()); Refresh(); });
    private void SessionSwitch(object sender, Microsoft.Win32.SessionSwitchEventArgs args)
    { if (args.Reason == Microsoft.Win32.SessionSwitchReason.SessionLock) Dispatch(async () => { recordCancellation?.Cancel(); if (recording.IsRecording) await ToggleRecordingAsync(); }); }
    private void SetStatus(string message) => Dispatch(() => { Status = message; StatusChanged?.Invoke(message); StateChanged?.Invoke(); });
    private void Dispatch(Action action) { if (disposed || dispatcher.HasShutdownStarted) return; if (!dispatcher.CheckAccess()) dispatcher.BeginInvoke(new Action(() => { if (!disposed) action(); })); else action(); }
    private void TrySave() { try { store.Save(Settings); } catch (Exception ex) { SetStatus("Sauvegarde : " + ex.Message); } }
    private static bool OwnWindowAt(int x, int y)
    { Native.GetWindowThreadProcessId(Native.WindowFromPoint(new Native.Point { X = x, Y = y }), out var pid); return pid == Environment.ProcessId; }

    private static string KeyText(KeyboardInput input, bool modifiersOnly)
    {
        var key = input.Key;
        var text = key switch { 32 => "Espace", 13 => "Entrée", 8 => "Retour", 46 => "Suppr", 9 => "Tab", 27 => "Échap", 37 => "←", 38 => "↑", 39 => "→", 40 => "↓", _ => KeyInterop.KeyFromVirtualKey((int)key).ToString() };
        if (!input.Control && !input.Alt && !input.Windows && key >= 0x30)
        {
            var state = new byte[256];
            for (var i = 0; i < 256; i++) state[i] = (byte)((Native.GetAsyncKeyState(i) & 0x8000) != 0 ? 128 : 0);
            state[0x14] |= (byte)(Native.GetKeyState(0x14) & 1);
            var thread = Native.GetWindowThreadProcessId(Native.GetForegroundWindow(), out _);
            var buffer = new StringBuilder(8);
            var count = Native.ToUnicodeEx(key, Native.MapVirtualKeyW(key, 0), state, buffer, buffer.Capacity, 4, Native.GetKeyboardLayout(thread));
            if (count > 0) text = buffer.ToString();
        }
        var parts = new List<string>();
        if (input.Control) parts.Add("Ctrl"); if (input.Alt) parts.Add("Alt"); if (input.Windows) parts.Add("Win"); if (input.Shift && (modifiersOnly || text.Length != 1)) parts.Add("Shift");
        parts.Add(text); return string.Join(" + ", parts);
    }
    public async void Quit()
    {
        if (IsShuttingDown) return;
        try { await (shutdownTask ??= ShutdownServicesAsync()); }
        finally { Dispose(); Application.Current.Shutdown(); }
    }
    private async Task ShutdownServicesAsync()
    {
        IsShuttingDown = true; recordCancellation?.Cancel();
        // Closing an editor cancels its owned recognition/export operation
        // before DisposeAsync waits for the shared native engine.
        foreach (var editor in Application.Current.Windows.OfType<CaptionEditorWindow>().ToArray()) editor.Close();
        try
        {
            await recordTransition.WaitAsync();
            try { if (recording.IsRecording || recording.OwnedProcessId != null) LastRecording = await recording.StopAsync(); }
            finally { recordTransition.Release(); }
            await captions.DisposeAsync(); await webcam.StopAsync();
        }
        catch (Exception ex) { SetStatus("Arrêt : " + ex.Message); }
    }
    public void PrepareForSessionEnd()
    {
        var completion = shutdownTask ??= ShutdownServicesAsync();
        if (!completion.IsCompleted)
        {
            var frame = new DispatcherFrame();
            var deadline = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
            deadline.Tick += (_, _) => { deadline.Stop(); frame.Continue = false; };
            _ = completion.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)), TaskScheduler.Default);
            deadline.Start(); Dispatcher.PushFrame(frame); deadline.Stop();
        }
        Dispose();
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true; IsShuttingDown = true;
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= DisplayChanged;
        Microsoft.Win32.SystemEvents.SessionSwitch -= SessionSwitch;
        clock.Stop(); saveTimer.Stop(); focusTimer.Stop(); holdTimer.Stop();
        input.Dispose(); hotkeys.Dispose(); magnification.Dispose(); sound.Dispose();
        TrySave(); tray.Visible = false; tray.Dispose();
        quickMenu?.Close(); recordingHud?.Close(); toolbar?.Close(); cameraWindow?.Close();
        foreach (var window in overlays) window.Close(); main.Close();
        trayIcon.Dispose(); applicationIcon.Dispose();
    }
}
