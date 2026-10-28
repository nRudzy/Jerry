using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Jerry.App.Platform;

namespace Jerry.App.UI;

/// <summary>Notification-area controls that stay open while a switch is changed.</summary>
public sealed class QuickMenuWindow : Window
{
    private readonly AppController controller;
    private readonly Dictionary<string, (CheckBox Box, Func<bool> Read)> switches = new();
    private readonly StackPanel additional = new();
    private readonly TextBlock recordLabel;
    private readonly Button recordButton;
    private readonly Button more;
    private bool syncing;
    private System.Drawing.Point anchor;

    public QuickMenuWindow(AppController controller, ImageSource? applicationIcon = null)
    {
        this.controller = controller;
        Title = "Jerry — Menu rapide"; Width = 282; SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; Topmost = true;
        Resources.MergedDictionaries.Add(Theme.CreateResources());
        var body = new StackPanel { Margin = new Thickness(5) };
        var master = Switch("master", "Activer Jerry", "cursor", () => controller.Settings.Enabled);
        var header = new StackPanel { Orientation = Orientation.Horizontal };
        header.Children.Add(applicationIcon != null
            ? new Image { Source = applicationIcon, Width = 18, Height = 18 }
            : Icons.Create("cursor", 18, Theme.Accent));
        header.Children.Add(new TextBlock { Text = "Jerry", FontWeight = FontWeights.SemiBold, Margin = new Thickness(9, 0, 0, 0) });
        master.Content = header; master.MinHeight = 38; body.Children.Add(master); Separator(body);
        var s = controller.Settings;
        body.Children.Add(Switch("ring", "Anneau du curseur", "ring", () => s.Ring.Enabled));
        body.Children.Add(Switch("click", "Effets de clic", "click", () => s.Click.Enabled));
        body.Children.Add(Switch("sound", "Sons de clic", "sound", () => s.Sound.Enabled));
        body.Children.Add(Switch("trail", "Traînée", "trail", () => s.Trail.Enabled));
        body.Children.Add(Switch("spotlight", "Projecteur", "spotlight", () => s.Spotlight.Enabled));
        body.Children.Add(Switch("locate", "Secouer pour localiser", "locate", () => s.Locate.Enabled));
        body.Children.Add(Switch("annotation", "Mode annotation", "annotation", () => controller.DrawingMode));
        more = MenuAction("Autres outils", "menu", () => { additional.Visibility = additional.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; UpdateLayout(); Reposition(); });
        more.Foreground = Theme.Muted; body.Children.Add(more);
        additional.Visibility = Visibility.Collapsed;
        additional.Children.Add(Switch("keystrokes", "Touches à l’écran", "keys", () => s.Keystrokes.Enabled));
        additional.Children.Add(Switch("zoom", "Zoom en direct", "zoom", () => controller.IsZoomActive));
        additional.Children.Add(Switch("loupe", "Loupe", "loupe", () => s.Zoom.LoupeEnabled));
        additional.Children.Add(Switch("focus", "Focus de fenêtre", "focus", () => s.Focus.Enabled));
        additional.Children.Add(Switch("camera", "Caméra", "camera", () => s.Camera.Enabled));
        additional.Children.Add(Switch("captions", "Sous-titres en direct", "captions", () => s.Captions.LiveEnabled));
        Separator(additional);
        recordLabel = Theme.Text("Enregistrer l’écran");
        recordButton = MenuAction(recordLabel, "record", async () => { Hide(); try { await controller.ToggleRecordingAsync(); } catch (Exception ex) { MessageBox.Show(ex.Message, "Jerry"); } });
        additional.Children.Add(recordButton);
        additional.Children.Add(MenuAction("Sous-titrer une vidéo…", "captions", () => { Hide(); controller.ShowCaptionEditor(controller.LastRecording); }));
        additional.Children.Add(MenuAction("Ouvrir les captures", "folder", () => { Hide(); controller.OpenOutputFolder(); }));
        body.Children.Add(additional); Separator(body);
        body.Children.Add(MenuAction("Réglages…", "settings", () => { Hide(); controller.ShowSettings(); }));
        body.Children.Add(MenuAction("Quitter Jerry", "power", () => { Hide(); controller.Quit(); }));
        Content = new Border { Background = Theme.Brush("#272727"), BorderBrush = Theme.Brush("#505050"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Child = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled } };
        SourceInitialized += (_, _) => WindowTools.ExcludeFromCapture(this);
        Deactivated += (_, _) => Hide();
        Closing += (_, e) => { if (!controller.IsShuttingDown) { e.Cancel = true; Hide(); } };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Hide(); e.Handled = true; } };
        controller.StateChanged += Sync;
        Closed += (_, _) => controller.StateChanged -= Sync;
        Sync();
    }
    public void ShowAtCursor()
    {
        anchor = System.Windows.Forms.Cursor.Position;
        Sync();
        if (!IsVisible) Show();
        Reposition(); Activate();
        switches["master"].Box.Focus();
    }
    internal void DismissIfOutside(int x, int y)
    {
        if (!IsVisible) return;
        if (Native.GetWindowRect(new WindowInteropHelper(this).Handle, out var bounds)
            && (x < bounds.Left || x >= bounds.Right || y < bounds.Top || y >= bounds.Bottom)) Hide();
    }
    private void Reposition()
    {
        if (!IsVisible) return;
        WindowTools.PlaceNear(this, anchor);
        var area = System.Windows.Forms.Screen.FromPoint(anchor).WorkingArea;
        MaxHeight = Math.Max(180, (area.Height - 16) / VisualTreeHelper.GetDpi(this).DpiScaleY);
        UpdateLayout(); WindowTools.PlaceNear(this, anchor);
    }
    private CheckBox Switch(string key, string label, string icon, Func<bool> read)
    {
        var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25) }); row.ColumnDefinitions.Add(new ColumnDefinition());
        row.Children.Add(Icons.Create(icon, 16, Theme.Muted)); var text = Theme.Text(label, 12); Grid.SetColumn(text, 1); row.Children.Add(text);
        var box = new CheckBox { Content = row, IsChecked = read(), MinHeight = 31, Padding = new Thickness(8, 5, 8, 5), FontSize = 12 };
        AutomationProperties.SetName(box, label);
        box.Click += async (_, _) =>
        {
            if (syncing || controller.IsShuttingDown) return;
            if (key == "camera" || key == "captions")
            {
                box.IsEnabled = false;
                try { if (key == "camera") await controller.SetCameraEnabledAsync(box.IsChecked == true); else await controller.SetLiveCaptionsAsync(box.IsChecked == true); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Jerry"); }
                finally { box.IsEnabled = true; Sync(); }
            }
            else { controller.Toggle(key); Sync(); }
        };
        switches.Add(key, (box, read)); return box;
    }
    private Button MenuAction(string label, string icon, Action action) => MenuAction(Theme.Text(label, 12), icon, action);
    private Button MenuAction(TextBlock label, string icon, Action action)
    {
        var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25) }); row.ColumnDefinitions.Add(new ColumnDefinition());
        row.Children.Add(Icons.Create(icon, 16, Theme.Muted)); Grid.SetColumn(label, 1); row.Children.Add(label);
        var button = new Button { Content = row, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(8, 7, 8, 7), HorizontalContentAlignment = HorizontalAlignment.Stretch, MinHeight = 31 };
        AutomationProperties.SetName(button, label.Text); button.Click += (_, _) => action(); return button;
    }
    private static void Separator(Panel panel) => panel.Children.Add(new Border { Height = 1, Background = Theme.Brush("#454545"), Margin = new Thickness(7, 5, 7, 5) });
    private void Sync()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.InvokeAsync(Sync); return; }
        syncing = true;
        try
        {
            foreach (var (key, item) in switches)
            {
                item.Box.IsChecked = item.Read();
                item.Box.Visibility = key == "master" || !controller.Settings.TrayItems.TryGetValue(key, out var visible) || visible ? Visibility.Visible : Visibility.Collapsed;
            }
            if (recordLabel != null)
            {
                recordLabel.Text = controller.IsRecording ? "Arrêter et sauvegarder" : "Enregistrer l’écran";
                AutomationProperties.SetName(recordButton, recordLabel.Text);
                recordButton.IsEnabled = !controller.IsRecordingTransition && !controller.IsShuttingDown;
            }
        }
        finally { syncing = false; }
        if (IsVisible) Dispatcher.InvokeAsync(Reposition, System.Windows.Threading.DispatcherPriority.Loaded);
    }
}
