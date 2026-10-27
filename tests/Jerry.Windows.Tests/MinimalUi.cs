using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using Jerry.App;
using Jerry.App.UI;
using Jerry.Core.Settings;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using Control = System.Windows.Controls.Control;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;
using Panel = System.Windows.Controls.Panel;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using ListBox = System.Windows.Controls.ListBox;
using Point = System.Windows.Point;
using TextBox = System.Windows.Controls.TextBox;

namespace Jerry.Windows.Tests;

internal static partial class Program
{
    private static readonly string[] MinimalPageLabels = ["Général", "Anneau", "Effets de clic", "Sons", "Traînée", "Projecteur", "Secouer", "Annotations", "Touches à l’écran", "Zoom et focus", "Enregistrement", "Caméra", "Sous-titres", "Raccourcis", "Menu rapide"];

    private static async Task MinimalUiAsync(AppController controller, MainWindow main)
    {
        var navigation = Descendants(main).OfType<ListBox>().Single();
        Check(navigation.Items.Count == MinimalPageLabels.Length, "Minimal UI exposes all fifteen settings pages");
        var labels = navigation.Items.Cast<ListBoxItem>().Select(OwnControlLabel).ToArray();
        Check(labels.SequenceEqual(MinimalPageLabels), "Minimal page labels and ordering match the agreed French navigation");
        Check(main.Background is SolidColorBrush background && Math.Max(background.Color.R, Math.Max(background.Color.G, background.Color.B)) < 65 && Math.Max(background.Color.R, Math.Max(background.Color.G, background.Color.B)) - Math.Min(background.Color.R, Math.Min(background.Color.G, background.Color.B)) <= 15, "Settings window uses a neutral charcoal background");
        Check(Theme.Accent.Color.B > Theme.Accent.Color.R + 50 && Theme.Accent.Color.B > Theme.Accent.Color.G, "Interactive accent is blue");
        var regularWidth = main.Width; var regularHeight = main.Height;
        Check(main.MinWidth <= 780 && main.MinHeight <= 560, "Smallest supported settings window permits 780 by 560 DIPs");
        foreach (var size in new[] { (Name: "regular", Width: regularWidth, Height: regularHeight), (Name: "small", Width: 780d, Height: 560d) })
        {
            main.WindowState = WindowState.Normal; main.Width = size.Width; main.Height = size.Height;
            await PumpAsync(100);
            Check(Math.Abs(main.ActualWidth - size.Width) < 2 && Math.Abs(main.ActualHeight - size.Height) < 2, size.Name + " settings window reached requested dimensions");
            for (var i = 0; i < MinimalPageLabels.Length; i++)
            {
                navigation.SelectedIndex = i; navigation.ScrollIntoView(navigation.Items[i]);
                await PumpAsync(130);
                var scroll = MainPageScroll(main);
                Check(scroll.ViewportWidth > 180 && scroll.ViewportHeight > 100, size.Name + " page " + i + " has usable scroll viewport");
                Check(Descendants(scroll).OfType<TextBlock>().Any(t => !string.IsNullOrWhiteSpace(t.Text)), size.Name + " page " + i + " has real rendered page content");
                var parts = 0;
                for (double top = 0; ; top += Math.Max(100, scroll.ViewportHeight * .85))
                {
                    scroll.ScrollToVerticalOffset(Math.Min(top, scroll.ScrollableHeight)); await PumpAsync(50);
                    CheckHorizontalLayout(scroll, size.Name + " page " + i + " scroll " + parts);
                    var bitmap = Snapshot(main); Save(bitmap, $"minimal-{size.Name}-page-{i:D2}-{parts:D2}.png");
                    Check(VisiblePixels(bitmap) > 10000, size.Name + " page " + i + " screenshot is nonempty");
                    parts++;
                    if (top >= scroll.ScrollableHeight || parts > 16) break;
                }
                results.Add(new { minimalPage = i, label = MinimalPageLabels[i], size = size.Name, width = main.ActualWidth, height = main.ActualHeight, screenshotParts = parts });
            }
        }
        main.Width = regularWidth; main.Height = regularHeight;
        await MinimalLiveActionsAsync(controller, main, navigation);
        await QuickMenuChecksAsync(controller);
        await CompactToolbarChecksAsync(controller);
        var hud = new RecordingControlWindow(controller);
        try
        {
            hud.Show(); hud.SetCountdown(3); await PumpAsync(70);
            CheckCompactHeight(hud, "Recording HUD"); CheckAffinity(hud, "Compact recording HUD");
            Check(Descendants(hud).OfType<Button>().Any(b => !string.IsNullOrWhiteSpace(OwnControlLabel(b))), "Compact HUD icon control has an accessible name");
            Save(Snapshot(hud), "minimal-recording-hud-countdown.png");
            hud.SetCountdown(null); await PumpAsync(40); Save(Snapshot(hud), "minimal-recording-hud-idle.png");
        }
        finally { hud.Close(); }
        Console.WriteLine("MINIMAL_UI_COMPONENT_CHECKS_PASSED");
    }

    private static ScrollViewer MainPageScroll(MainWindow main)
        => Descendants(main).OfType<ScrollViewer>().Where(s => s.Content is Panel).OrderByDescending(s => s.ViewportWidth).First();

    private static void CheckHorizontalLayout(ScrollViewer scroll, string label)
    {
        Check(scroll.ExtentWidth <= scroll.ViewportWidth + 2, label + " has no horizontal content overflow");
        var presenter = Descendants(scroll).OfType<ScrollContentPresenter>().First(p => p.TemplatedParent == scroll);
        var controls = Descendants(scroll).OfType<Control>().Where(c => c.IsVisible && c.ActualWidth > 0 && c.TemplatedParent == null && c is ButtonBase or ComboBox or TextBox or Slider).ToArray();
        Check(controls.Length > 0, label + " contains accessible settings controls");
        foreach (var control in controls)
        {
            var name = OwnControlLabel(control);
            Check(!string.IsNullOrWhiteSpace(name), label + " owner control has an accessible label: " + control.GetType().Name);
            if (control.IsEnabled) Check(control.Focusable && control.IsTabStop, label + " enabled control is keyboard reachable: " + name);
            var position = control.TranslatePoint(new Point(0, 0), presenter);
            Check(position.X >= -1 && position.X + control.ActualWidth <= presenter.ActualWidth + 1, label + " control fits horizontally: " + name);
        }
        foreach (var text in Descendants(scroll).OfType<TextBlock>().Where(t => t.IsVisible && t.ActualWidth > 0 && t.TextWrapping == TextWrapping.NoWrap && t.TextTrimming == TextTrimming.None && t.Text.Length > 1 && t.TemplatedParent == null))
        {
            var natural = Math.Max(0, text.DesiredSize.Width - text.Margin.Left - text.Margin.Right);
            var measured = new FormattedText(text.Text, System.Globalization.CultureInfo.CurrentUICulture, text.FlowDirection,
                new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch), text.FontSize, text.Foreground, VisualTreeHelper.GetDpi(text).PixelsPerDip);
            Check(natural <= text.ActualWidth + 2 && measured.Width <= text.ActualWidth + 3, label + " unwrapped text fits without unintended clipping: " + text.Text);
        }
    }

    private static async Task QuickMenuChecksAsync(AppController controller)
    {
        foreach (var key in new[] { "ring", "click", "sound", "trail", "spotlight", "locate", "annotation" }) controller.Settings.TrayItems[key] = true;
        foreach (var key in new[] { "zoom", "loupe", "focus", "camera", "captions", "keystrokes" }) controller.Settings.TrayItems[key] = false;
        controller.SettingsChanged();
        controller.ShowQuickMenu(); await PumpAsync(100);
        var menu = Application.Current.Windows.OfType<QuickMenuWindow>().Single(w => w.IsVisible);
        var labels = new[] { "Activer Jerry", "Anneau du curseur", "Effets de clic", "Sons de clic", "Traînée", "Projecteur", "Secouer pour localiser", "Mode annotation" };
        foreach (var label in labels) Check(MenuSwitch(menu, label).IsVisible, "Quick menu exposes switch: " + label);
        Check(VisibleMenuSwitches(menu).Length == 8, "Quick menu hides all deselected additional features");
        CheckAffinity(menu, "Quick menu"); CheckWithinMonitorWorkArea(menu, "Quick menu");
        Check(menu.ActualHeight <= SystemParameters.WorkArea.Height, "Quick menu maximum rendered height fits work area");
        Save(Snapshot(menu), "minimal-quick-menu-core.png");
        var features = new (string Label, Func<bool> Read)[]
        {
            ("Activer Jerry", () => controller.Settings.Enabled), ("Anneau du curseur", () => controller.Settings.Ring.Enabled),
            ("Effets de clic", () => controller.Settings.Click.Enabled), ("Sons de clic", () => controller.Settings.Sound.Enabled),
            ("Traînée", () => controller.Settings.Trail.Enabled), ("Projecteur", () => controller.Settings.Spotlight.Enabled),
            ("Secouer pour localiser", () => controller.Settings.Locate.Enabled), ("Mode annotation", () => controller.DrawingMode)
        };
        foreach (var feature in features)
        {
            var initial = feature.Read();
            ClickOwnSwitch(MenuSwitch(menu, feature.Label), !initial); await PumpAsync(40);
            Check(menu.IsVisible, "Quick menu remains open while switching " + feature.Label);
            Check(feature.Read() == !initial && MenuSwitch(menu, feature.Label).IsChecked == !initial, "Quick menu click route changes actual state: " + feature.Label);
            ClickOwnSwitch(MenuSwitch(menu, feature.Label), initial); await PumpAsync(30);
            Check(feature.Read() == initial, "Quick menu restores initial state: " + feature.Label);
        }
        var ring = controller.Settings.Ring.Enabled; controller.Toggle("ring"); await PumpAsync(50);
        Check(menu.IsVisible && MenuSwitch(menu, "Anneau du curseur").IsChecked == !ring, "External controller.Toggle updates visible quick menu without closing it");
        controller.Toggle("ring");
        controller.Settings.TrayItems["ring"] = false; controller.SettingsChanged(); await PumpAsync(50);
        Check(!VisibleMenuSwitches(menu).Any(c => OwnControlLabel(c) == "Anneau du curseur"), "TrayItems hides deselected quick menu row while menu is open");
        controller.Settings.TrayItems["ring"] = true; controller.Settings.TrayItems["focus"] = true; controller.SettingsChanged(); await PumpAsync(50);
        Check(Descendants(menu).OfType<CheckBox>().Single(c => OwnControlLabel(c) == "Focus de fenêtre").Visibility == Visibility.Visible, "TrayItems selects additional feature independently of collapsed tools group");
        Descendants(menu).OfType<Button>().Single(b => OwnControlLabel(b) == "Autres outils").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await PumpAsync(50);
        Check(VisibleMenuSwitches(menu).Length == 9 && VisibleMenuSwitches(menu).Any(c => OwnControlLabel(c).Contains("Focus", StringComparison.OrdinalIgnoreCase)), "Expanding own additional tools reveals selected feature switch");
        CheckWithinMonitorWorkArea(menu, "Quick menu after feature changes");
        foreach (var check in VisibleMenuSwitches(menu))
        {
            var tracks = Descendants(check).OfType<Border>().Where(b => b.CornerRadius.TopLeft >= 6 && b.ActualWidth is >= 26 and <= 52 && b.ActualHeight is >= 12 and <= 30).ToArray();
            Check(tracks.Length > 0 && tracks.Any(t => t.TranslatePoint(new Point(t.ActualWidth / 2, 0), check).X > check.ActualWidth * .6), "Switch appears on right side of quick menu row: " + OwnControlLabel(check));
        }
        Save(Snapshot(menu), "minimal-quick-menu.png");
        results.Add(new { minimalQuickMenu = true, width = menu.ActualWidth, height = menu.ActualHeight, selectedLabels = VisibleMenuSwitches(menu).Select(OwnControlLabel).ToArray() });
        menu.Close(); await PumpAsync(30);
        Check(!menu.IsVisible, "Own quick-menu Close route hides cached menu");
        controller.ShowQuickMenu(); await PumpAsync(40);
        Check(menu.IsVisible && Application.Current.Windows.OfType<QuickMenuWindow>().Single(w => w.IsVisible) == menu, "Public menu action safely reopens same cached window after Close");
        controller.ShowQuickMenu(); await PumpAsync(30); Check(!menu.IsVisible, "Repeated public quick-menu action toggles the visible menu closed");
        await QuickMenuPointerRoutingAsync(controller, menu);
    }

    private static async Task CompactToolbarChecksAsync(AppController controller)
    {
        if (!controller.DrawingMode) controller.ToggleDrawing(); await PumpAsync(60);
        var toolbar = Application.Current.Windows.OfType<AnnotationToolbar>().Single(w => w.IsVisible);
        try
        {
            CheckCompactHeight(toolbar, "Annotation toolbar"); CheckAffinity(toolbar, "Compact annotation toolbar");
            var buttons = Descendants(toolbar).OfType<Button>().Where(b => b.IsVisible).ToArray();
            Check(buttons.Length >= 18 && buttons.All(b => OwnControlLabel(b).Length > 0), "Toolbar tools, colors and actions all have accessible names");
            var centers = buttons.Select(b => b.TranslatePoint(new Point(0, b.ActualHeight / 2), toolbar).Y).ToArray();
            Check(centers.Max() - centers.Min() <= 6, "All annotation buttons remain in one physical row");
            controller.Settings.Annotation.Tool = AnnotationTool.Arrow; controller.Settings.Annotation.Color = "#1278CE"; controller.Settings.Annotation.Width = 9; controller.SettingsChanged(); await PumpAsync(40);
            CheckSelectedTool(toolbar, "Flèche"); CheckToolbarColor(toolbar, "#1278CE");
            Check(Descendants(toolbar).OfType<Slider>().Single().Value == 9, "External annotation width updates compact toolbar");
            controller.Settings.Annotation.Tool = AnnotationTool.Highlighter; controller.Settings.Annotation.HighlighterColor = "#BB44CC"; controller.Settings.Annotation.HighlighterWidth = 44; controller.SettingsChanged(); await PumpAsync(40);
            CheckSelectedTool(toolbar, "Surligneur"); CheckToolbarColor(toolbar, "#BB44CC");
            Check(Descendants(toolbar).OfType<Slider>().Single().Value == 44, "Highlighter uses its independent width in compact toolbar");
            controller.Settings.Annotation.Tool = AnnotationTool.Arrow; controller.SettingsChanged(); await PumpAsync(40);
            CheckSelectedTool(toolbar, "Flèche"); CheckToolbarColor(toolbar, "#1278CE");
            Check(Descendants(toolbar).OfType<Slider>().Single().Value == 9, "Returning tools preserves ordinary width");
            buttons.Single(b => OwnControlLabel(b) == "Couleur : Rouge").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var slider = Descendants(toolbar).OfType<Slider>().Single(); slider.Value = 11; controller.SettingsChanged(); await PumpAsync(40);
            Check(controller.Settings.Annotation.Color == "#EF4444" && controller.Settings.Annotation.Width == 11, "Actual palette click and width control update annotation settings");
            controller.Toggle("ring"); await PumpAsync(30);
            CheckToolbarColor(toolbar, "#EF4444"); Check(slider.Value == 11, "Unrelated external refresh preserves selected width");
            Save(Snapshot(toolbar), "minimal-annotation-toolbar.png");
        }
        finally { if (controller.DrawingMode) controller.ToggleDrawing(); }
    }

    private static void CheckSelectedTool(AnnotationToolbar toolbar, string label)
    {
        var selected = Descendants(toolbar).OfType<Button>().Where(b => AutomationProperties.GetItemStatus(b) == "Sélectionné").ToArray();
        Check(selected.Length == 1 && OwnControlLabel(selected[0]).StartsWith(label, StringComparison.Ordinal), "External SettingsChanged updates selected annotation tool: " + label);
    }
    private static void CheckToolbarColor(AnnotationToolbar toolbar, string hex)
    {
        var custom = Descendants(toolbar).OfType<Button>().Single(b => OwnControlLabel(b) == "Choisir une couleur…");
        Check(custom.Content is Ellipse ellipse && ellipse.Fill is SolidColorBrush color && color.Color == (Color)ColorConverter.ConvertFromString(hex), "Custom color swatch tracks active tool color " + hex);
    }
    private static void CheckCompactHeight(Window window, string label)
    {
        Check(window.ActualHeight <= 54, label + " height stays at most 54 DIPs");
        Check(Native.GetWindowRect(new WindowInteropHelper(window).Handle, out var rect), label + " native rectangle is observable");
        var dpi = VisualTreeHelper.GetDpi(window);
        Check((rect.Bottom - rect.Top) / dpi.DpiScaleY <= 54.5, label + " actual native height respects compact size at current DPI");
        CheckWithinMonitorWorkArea(window, label);
    }
    private static void CheckWithinMonitorWorkArea(Window window, string label)
    {
        var handle = new WindowInteropHelper(window).Handle; Check(Native.GetWindowRect(handle, out var rect), label + " native bounds available");
        var area = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
        Check(rect.Left >= area.Left && rect.Top >= area.Top && rect.Right <= area.Right && rect.Bottom <= area.Bottom, label + " is clamped inside physical work area");
    }
    private static CheckBox[] VisibleMenuSwitches(QuickMenuWindow menu) => Descendants(menu).OfType<CheckBox>().Where(c => c.IsVisible).ToArray();
    private static CheckBox MenuSwitch(QuickMenuWindow menu, string label) => VisibleMenuSwitches(menu).Single(c => OwnControlLabel(c) == label);
    private static void ClickOwnSwitch(CheckBox check, bool value)
    {
        check.SetCurrentValue(ToggleButton.IsCheckedProperty, value);
        check.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    }
    private static string OwnControlLabel(DependencyObject element)
    {
        var accessible = AutomationProperties.GetName(element); if (!string.IsNullOrWhiteSpace(accessible)) return accessible.Trim();
        if (element is ContentControl content)
        {
            if (content.Content is string text) return text.Trim();
            if (content.Content is TextBlock label) return label.Text.Trim();
            if (content.Content is DependencyObject visual) return string.Join(" ", Descendants(visual).OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t))).Trim();
        }
        return "";
    }
}
