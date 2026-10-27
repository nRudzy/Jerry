using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
using Jerry.Core.Settings;
using Jerry.App.Media;
using Microsoft.Win32;

namespace Jerry.App.UI;

public sealed partial class MainWindow : Window
{
    private readonly AppController controller;
    private readonly StackPanel page = new();
    private readonly TextBlock status;
    private readonly ListBox navigation;
    private readonly ScrollViewer scroll;
    private readonly List<StackPanel> groups = [];
    private readonly List<Action> refreshControls = [];
    private bool building;
    private readonly string[] titles = { "Général", "Anneau", "Effets de clic", "Sons", "Traînée", "Projecteur", "Secouer", "Annotations", "Touches à l’écran", "Zoom et focus", "Enregistrement", "Caméra", "Sous-titres", "Raccourcis", "Menu rapide" };
    private readonly string[] icons = { "settings", "ring", "click", "sound", "trail", "spotlight", "shake", "annotation", "keys", "zoom", "record", "camera", "captions", "shortcuts", "menu" };
    private AppSettings S => controller.Settings;

    public MainWindow(AppController controller)
    {
        this.controller = controller;
        Title = "Jerry — Réglages";
        var area = SystemParameters.WorkArea;
        var availableWidth = Math.Max(320, area.Width - 24);
        var availableHeight = Math.Max(320, area.Height - 24);
        MinWidth = Math.Min(780, availableWidth); MinHeight = Math.Min(560, availableHeight);
        Width = Math.Min(940, availableWidth); Height = Math.Min(740, availableHeight);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Theme.Background; Foreground = Theme.Foreground;
        WindowStyle = WindowStyle.None;
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 38, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(8) });
        Resources.MergedDictionaries.Add(Theme.CreateResources());
        var shell = new DockPanel();
        var titlebar = new Grid { Height = 38, Background = Theme.Brush("#282828") };
        titlebar.ColumnDefinitions.Add(new ColumnDefinition()); titlebar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var appIcon = new Image { Width = 18, Height = 18 };
        appIcon.SetBinding(Image.SourceProperty, new System.Windows.Data.Binding(nameof(Icon)) { Source = this });
        brand.Children.Add(appIcon);
        brand.Children.Add(new TextBlock { Text = "Jerry", FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(9, 0, 0, 0) });
        titlebar.Children.Add(brand);
        var captionButtons = new StackPanel { Orientation = Orientation.Horizontal };
        CaptionButton(captionButtons, "minimize", "Réduire", () => WindowState = WindowState.Minimized);
        CaptionButton(captionButtons, "maximize", "Agrandir ou restaurer", () => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized);
        CaptionButton(captionButtons, "close", "Fermer les réglages", Close);
        Grid.SetColumn(captionButtons, 1); titlebar.Children.Add(captionButtons);
        DockPanel.SetDock(titlebar, Dock.Top); shell.Children.Add(titlebar);
        var root = new Grid(); root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(208) }); root.ColumnDefinitions.Add(new ColumnDefinition());
        var sidebar = new DockPanel { Background = Theme.Brush("#222222") };
        var bottom = Action("Ouvrir les captures", controller.OpenOutputFolder);
        bottom.Margin = new Thickness(8, 4, 8, 12); bottom.Foreground = Theme.Muted;
        DockPanel.SetDock(bottom, Dock.Bottom); sidebar.Children.Add(bottom);
        navigation = new ListBox { BorderThickness = new Thickness(0), Background = Brushes.Transparent, Foreground = Theme.Foreground, Padding = new Thickness(0, 12, 0, 8) };
        AutomationProperties.SetName(navigation, "Rubriques des réglages");
        ScrollViewer.SetVerticalScrollBarVisibility(navigation, ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(navigation, ScrollBarVisibility.Disabled);
        for (var i = 0; i < titles.Length; i++)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(Icons.Create(icons[i], 18, Theme.Accent));
            row.Children.Add(new TextBlock { Text = titles[i], FontSize = 13, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            var item = new ListBoxItem { Content = row, Tag = titles[i] }; AutomationProperties.SetName(item, titles[i]); navigation.Items.Add(item);
        }
        navigation.SelectionChanged += (_, _) => ShowPage(navigation.SelectedIndex);
        sidebar.Children.Add(navigation);
        root.Children.Add(new Border { BorderBrush = Theme.Brush("#101010"), BorderThickness = new Thickness(0, 0, 1, 0), Child = sidebar });
        var main = new DockPanel { Margin = new Thickness(26, 12, 22, 12) }; Grid.SetColumn(main, 1);
        status = Theme.Text(controller.Status, 11, Theme.Muted); status.Margin = new Thickness(2, 10, 8, 0); status.MaxHeight = 32; status.TextTrimming = TextTrimming.CharacterEllipsis;
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite); DockPanel.SetDock(status, Dock.Bottom); main.Children.Add(status);
        scroll = new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 8, 0) };
        main.Children.Add(scroll); root.Children.Add(main); shell.Children.Add(root);
        Content = new Border { BorderBrush = Theme.Brush("#454545"), BorderThickness = new Thickness(1), Child = shell };
        controller.StatusChanged += OnStatus; controller.StateChanged += OnState;
        Closed += (_, _) => { controller.StatusChanged -= OnStatus; controller.StateChanged -= OnState; };
        Closing += OnClosing;
        Activated += (_, _) => ReloadPage();
        navigation.SelectedIndex = 0;
    }
    private void CaptionButton(Panel panel, string icon, string name, Action action)
    {
        var button = new Button { Content = Icons.Create(icon, 13), Width = 44, Height = 36, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), ToolTip = name };
        AutomationProperties.SetName(button, name); WindowChrome.SetIsHitTestVisibleInChrome(button, true);
        button.Click += (_, _) => action(); panel.Children.Add(button);
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (controller.IsShuttingDown) return;
        e.Cancel = true;
        if (S.MinimizeToTray) Hide();
        else Dispatcher.BeginInvoke(new Action(controller.Quit));
    }
    private void OnStatus(string value) => status.Text = value;
    private void OnState()
    {
        status.Text = controller.Status;
        if (!building) foreach (var refresh in refreshControls.ToArray()) refresh();
    }
    private void Save() => controller.SettingsChanged();
    private async Task Run(Func<Task> operation) { try { await operation(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Jerry", MessageBoxButton.OK, MessageBoxImage.Warning); } }
    private Button Action(string text, Action action)
    {
        var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(Theme.Text(text)); var arrow = Icons.Create("chevron", 12, Theme.Muted); arrow.Margin = new Thickness(12, 0, 0, 0); Grid.SetColumn(arrow, 1); row.Children.Add(arrow);
        var button = new Button { Content = row, Background = Brushes.Transparent, BorderThickness = new Thickness(0), Padding = new Thickness(12, 10, 12, 10), HorizontalContentAlignment = HorizontalAlignment.Stretch, MinHeight = 40 };
        AutomationProperties.SetName(button, text); button.Click += (_, _) => action(); return button;
    }
    private Button AsyncAction(string text, Func<Task> action)
    {
        Button? button = null;
        button = Action(text, async () => { button!.IsEnabled = false; try { await Run(action); } finally { button.IsEnabled = true; } });
        return button;
    }
    private Button LiveAction(Func<string> label, Action action) => WatchLabel(Action(label(), action), label);
    private Button LiveAsyncAction(Func<string> label, Func<Task> action) => WatchLabel(AsyncAction(label(), action), label);
    private Button WatchLabel(Button button, Func<string> label)
    {
        var text = (TextBlock)((Grid)button.Content).Children[0];
        refreshControls.Add(() => { text.Text = label(); AutomationProperties.SetName(button, text.Text); });
        return button;
    }
    private StackPanel Card(string title, string? explanation = null)
    {
        var section = new StackPanel { Margin = new Thickness(0, 4, 0, 18) };
        if (!string.IsNullOrEmpty(title)) { var heading = Theme.Text(title, 14); heading.FontWeight = FontWeights.SemiBold; heading.Margin = new Thickness(12, 9, 0, 9); section.Children.Add(heading); }
        var body = new StackPanel(); groups.Add(body);
        section.Children.Add(new Border { Background = Theme.Surface, BorderBrush = Theme.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Child = body });
        if (explanation != null) { var hint = Theme.Text(explanation, 12, Theme.Muted); hint.Margin = new Thickness(12, 8, 12, 0); section.Children.Add(hint); }
        page.Children.Add(section); return body;
    }
    private void Check(Panel p, string label, bool value, Action<bool> set, Func<bool>? read = null)
    {
        var box = new CheckBox { Content = Theme.Text(label), IsChecked = value }; AutomationProperties.SetName(box, label);
        box.Click += (_, _) => { set(box.IsChecked == true); Save(); };
        if (read != null) refreshControls.Add(() => box.IsChecked = read());
        p.Children.Add(box);
    }
    private void AsyncCheck(Panel p, string label, bool value, Func<bool, Task> set)
    {
        var box = new CheckBox { Content = Theme.Text(label), IsChecked = value }; AutomationProperties.SetName(box, label);
        Func<bool> read = label.Contains("caméra", StringComparison.OrdinalIgnoreCase) ? () => S.Camera.Enabled : () => S.Captions.LiveEnabled;
        refreshControls.Add(() => box.IsChecked = read());
        box.Click += async (_, _) => { box.IsEnabled = false; try { await Run(() => set(box.IsChecked == true)); box.IsChecked = read(); } finally { box.IsEnabled = true; } }; p.Children.Add(box);
    }
    private static Grid ControlRow(Panel p, string label, FrameworkElement control, double width = 200)
    {
        var row = new Grid { MinHeight = 44, Margin = new Thickness(12, 0, 12, 0) };
        row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(width) });
        var text = Theme.Text(label); text.Margin = new Thickness(0, 9, 16, 9); row.Children.Add(text);
        control.Margin = new Thickness(0, 7, 0, 7); control.VerticalAlignment = VerticalAlignment.Center; Grid.SetColumn(control, 1); row.Children.Add(control); p.Children.Add(row); return row;
    }
    private void Range(Panel p, string label, double value, double min, double max, double step, Action<double> set, string unit = "")
    {
        var controls = new Grid(); controls.ColumnDefinitions.Add(new ColumnDefinition()); controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(62) });
        var slider = new Slider { Minimum = min, Maximum = max, Value = value, TickFrequency = step, SmallChange = step, LargeChange = step * 5, IsSnapToTickEnabled = true, IsMoveToPointEnabled = true };
        AutomationProperties.SetName(slider, label);
        var number = Theme.Text(value.ToString("0.##", CultureInfo.CurrentCulture) + unit, 12, Theme.Muted); number.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetColumn(number, 1);
        controls.Children.Add(slider); controls.Children.Add(number); ControlRow(p, label, controls, 184);
        slider.ValueChanged += (_, _) => { number.Text = slider.Value.ToString("0.##", CultureInfo.CurrentCulture) + unit; set(slider.Value); Save(); };
    }
    private void Choice<T>(Panel p, string label, T value, Action<T> set) where T : struct, Enum
    {
        var values = Enum.GetValues<T>(); var box = new ComboBox { ItemsSource = values.Select(x => Label(x.ToString())).ToArray(), SelectedIndex = Array.IndexOf(values, value) };
        AutomationProperties.SetName(box, label); ControlRow(p, label, box);
        box.SelectionChanged += (_, _) => { if (box.SelectedIndex >= 0) { set(values[box.SelectedIndex]); Save(); } };
    }
    private void Options(Panel p, string label, string value, string[] options, Action<string> set)
    {
        var box = new ComboBox { ItemsSource = options.Select(x => label == "Fréquence d’animation" ? x + " i/s" : OptionLabel(x)).ToArray(), SelectedIndex = Array.IndexOf(options, value) };
        AutomationProperties.SetName(box, label); ControlRow(p, label, box);
        box.SelectionChanged += (_, _) => { if (box.SelectedIndex >= 0) { set(options[box.SelectedIndex]); Save(); } };
    }
    private void Input(Panel p, string label, string value, Action<string> set)
    {
        var box = new TextBox { Text = value }; AutomationProperties.SetName(box, label); ControlRow(p, label, box);
        box.LostKeyboardFocus += (_, _) => { if (box.Text.Trim() != value) { value = box.Text.Trim(); set(value); Save(); } };
    }
    private void Color(Panel p, string label, string value, Action<string> set)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(new Border { Background = Theme.Brush(value), Width = 16, Height = 16, CornerRadius = new CornerRadius(8), BorderBrush = Theme.Brush("#888888"), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 9, 0) });
        row.Children.Add(Theme.Text(value, 12, Theme.Muted));
        var button = new Button { Content = row, HorizontalAlignment = HorizontalAlignment.Right, ToolTip = "Choisir une couleur" }; AutomationProperties.SetName(button, label);
        button.Click += (_, _) =>
        {
            using var picker = new System.Windows.Forms.ColorDialog { FullOpen = true };
            try { picker.Color = System.Drawing.ColorTranslator.FromHtml(value); } catch { }
            if (picker.ShowDialog() == System.Windows.Forms.DialogResult.OK) { var hex = $"#{picker.Color.R:X2}{picker.Color.G:X2}{picker.Color.B:X2}"; set(hex); Save(); ReloadPage(); }
        };
        ControlRow(p, label, button, 140);
    }
    private void ReloadPage() => ShowPage(navigation.SelectedIndex, resetScroll: false);
    private void ShowPage(int index, bool resetScroll = true)
    {
        if (index < 0 || index >= titles.Length) return;
        building = true;
        try
        {
            page.Children.Clear(); groups.Clear(); refreshControls.Clear();
            switch (index) { case 0: Overview(); break; case 1: Ring(); break; case 2: Clicks(); break; case 3: Sounds(); break; case 4: Trail(); break; case 5: Spotlight(); break; case 6: Locate(); break; case 7: Annotations(); break; case 8: Keys(); break; case 9: Zoom(); break; case 10: Recording(); break; case 11: Camera(); break; case 12: Captions(); break; case 13: Hotkeys(); break; case 14: Preferences(); break; }
            foreach (var group in groups)
            {
                var children = group.Children.Cast<UIElement>().ToArray(); group.Children.Clear();
                for (var i = 0; i < children.Length; i++)
                {
                    if (children[i] is TextBlock hint) { hint.Margin = new Thickness(12, 8, 12, 10); hint.FontSize = 12; }
                    group.Children.Add(new Border { BorderBrush = Theme.Line, BorderThickness = new Thickness(0, 0, 0, i == children.Length - 1 ? 0 : 1), Child = children[i] });
                }
            }
            if (resetScroll) scroll.ScrollToTop();
        }
        finally { building = false; }
    }
}
