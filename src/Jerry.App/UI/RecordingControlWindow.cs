using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Jerry.App.Platform;

namespace Jerry.App.UI;

public sealed class RecordingControlWindow : Window
{
    private readonly AppController controller;
    private readonly TextBlock elapsed;
    private readonly Button stop;
    private readonly Ellipse dot;
    private readonly DispatcherTimer timer;
    private long? started;
    private int? countdown;

    public RecordingControlWindow(AppController controller)
    {
        this.controller = controller;
        Title = "Jerry — Commandes de capture"; Width = 210; Height = 46;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        Resources.MergedDictionaries.Add(Theme.CreateResources());
        var row = new Grid { Margin = new Thickness(9, 6, 8, 6) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(17) });
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
        var grip = new Border { Background = Brushes.Transparent, Child = Icons.Create("grip", 13, Theme.Muted), Cursor = Cursors.SizeAll, ToolTip = "Déplacer les commandes" };
        grip.MouseLeftButtonDown += (_, e) => { if (e.LeftButton == MouseButtonState.Pressed) { DragMove(); ClampPlacement(); } };
        AutomationProperties.SetName(grip, "Déplacer les commandes de capture"); row.Children.Add(grip);
        dot = new Ellipse { Width = 7, Height = 7, Fill = Theme.Brush("#EF4444"), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        Grid.SetColumn(dot, 1); row.Children.Add(dot);
        elapsed = new TextBlock { Text = "Prêt", Foreground = Theme.Foreground, FontFamily = new FontFamily("Segoe UI"), FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 2, 0), TextTrimming = TextTrimming.CharacterEllipsis };
        AutomationProperties.SetName(elapsed, "Durée de la capture"); Grid.SetColumn(elapsed, 2); row.Children.Add(elapsed);
        stop = new Button { Content = Icons.Create("stop", 15, Theme.Foreground), Style = AnnotationToolbar.CreateCompactStyle(), Width = 30, Height = 30, ToolTip = "Arrêter et sauvegarder" };
        AutomationProperties.SetName(stop, "Arrêter et sauvegarder l’enregistrement");
        stop.Click += async (_, _) =>
        {
            stop.IsEnabled = false;
            try { await controller.ToggleRecordingAsync(); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Jerry", MessageBoxButton.OK, MessageBoxImage.Warning); }
            finally { Update(); }
        };
        Grid.SetColumn(stop, 3); row.Children.Add(stop);
        Content = new Border { Background = Theme.Brush("#292929"), BorderBrush = Theme.Brush("#4A4A4A"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Child = row };
        Left = Math.Max(SystemParameters.WorkArea.Left, SystemParameters.WorkArea.Right - Width - 20);
        Top = SystemParameters.WorkArea.Top + 20;
        SourceInitialized += (_, _) =>
        {
            WindowTools.ExcludeFromCapture(this);
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook((IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            { if (message == 0x0021) { handled = true; return new IntPtr(3); } return IntPtr.Zero; });
        };
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (_, _) => Update();
        Loaded += (_, _) => { timer.Start(); ClampPlacement(); Update(); };
        controller.StateChanged += Update;
        Closing += (_, e) => { if (!controller.IsShuttingDown && (controller.IsRecording || controller.IsRecordingTransition)) e.Cancel = true; };
        Closed += (_, _) => { timer.Stop(); controller.StateChanged -= Update; };
    }
    public void SetCountdown(int? seconds) { countdown = seconds; if (seconds.HasValue) started = null; Update(); }
    private void Update()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.InvokeAsync(Update); return; }
        if (countdown is int remaining)
        {
            elapsed.Text = remaining > 0 ? $"Départ · {remaining}" : "Départ…";
            elapsed.ToolTip = remaining > 0 ? $"Départ dans {remaining} secondes" : "Démarrage de la capture";
            dot.Fill = Theme.Brush("#F5B94C"); stop.IsEnabled = false; return;
        }
        stop.IsEnabled = controller.IsRecording && !controller.IsRecordingTransition;
        if (controller.IsRecording)
        {
            started ??= Environment.TickCount64;
            var duration = TimeSpan.FromMilliseconds(Environment.TickCount64 - started.Value);
            elapsed.Text = duration.TotalHours >= 1 ? duration.ToString(@"hh\:mm\:ss") : duration.ToString(@"mm\:ss");
            elapsed.ToolTip = "Durée de l’enregistrement"; dot.Fill = Theme.Brush("#EF4444");
        }
        else
        {
            started = null;
            elapsed.Text = controller.IsRecordingTransition ? "Finalisation…" : "Capture terminée";
            elapsed.ToolTip = elapsed.Text; dot.Fill = Theme.Muted;
        }
    }
    private void ClampPlacement() => WindowTools.ClampToCurrentMonitor(this);
}
