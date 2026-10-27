using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using Jerry.Core.Settings;
using Jerry.App.Platform;

namespace Jerry.App.UI;

public sealed class AnnotationToolbar : Window
{
    private readonly AppController controller;
    private readonly Dictionary<AnnotationTool, Button> tools = new();
    private readonly Dictionary<string, Ellipse> swatches = new(StringComparer.OrdinalIgnoreCase);
    private readonly Slider width;
    private readonly Button customColor;
    private readonly WrapPanel row;
    private bool syncing;
    private static readonly Brush Outline = Theme.Brush("#4A4A4A");
    private static readonly Brush Selected = Theme.Brush("#264A74");
    private readonly Style compactStyle;

    public AnnotationToolbar(AppController controller)
    {
        this.controller = controller;
        Title = "Jerry — Outils de dessin";
        Width = Math.Min(548, Math.Max(240, SystemParameters.WorkArea.Width - 20));
        Height = Width >= 548 ? 44 : 76;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        Resources.MergedDictionaries.Add(Theme.CreateResources());
        compactStyle = CreateCompactStyle();
        row = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6, 5, 6, 5), VerticalAlignment = VerticalAlignment.Center };
        var grip = new Border { Width = 16, Height = 30, Child = Icons.Create("grip", 13, Theme.Muted), Background = Brushes.Transparent, Cursor = Cursors.SizeAll, ToolTip = "Déplacer la barre" };
        grip.MouseLeftButtonDown += (_, e) => { if (e.LeftButton == MouseButtonState.Pressed) { DragMove(); ClampPlacement(); } };
        AutomationProperties.SetName(grip, "Déplacer la barre de dessin");
        row.Children.Add(grip);
        var names = new[] { (AnnotationTool.Freehand, "pen", "Crayon · F"), (AnnotationTool.Arrow, "arrow", "Flèche · A"), (AnnotationTool.Rectangle, "rectangle", "Rectangle · R"), (AnnotationTool.Circle, "circle", "Cercle · C"), (AnnotationTool.Line, "line", "Ligne · L"), (AnnotationTool.Highlighter, "highlighter", "Surligneur · H"), (AnnotationTool.Text, "text", "Texte · T"), (AnnotationTool.Number, "number", "Repère numéroté · N") };
        foreach (var (tool, icon, label) in names)
        {
            var button = IconButton(icon, label, () => { controller.Settings.Annotation.Tool = tool; controller.SettingsChanged(); Sync(); });
            tools.Add(tool, button); row.Children.Add(button);
        }
        Separator();
        foreach (var color in new[] { "#EF4444", "#FACC15", "#22C55E", "#3B82F6", "#FFFFFF", "#151515" })
        {
            var dot = new Ellipse { Width = 15, Height = 15, Fill = Theme.Brush(color), Stroke = Outline, StrokeThickness = 1 };
            var button = new Button { Style = compactStyle, Content = dot, Width = 20, Height = 30, ToolTip = ColorName(color) };
            AutomationProperties.SetName(button, "Couleur : " + ColorName(color));
            button.Click += (_, _) => SetColor(color);
            swatches.Add(color, dot); row.Children.Add(button);
        }
        customColor = IconButton("circle", "Choisir une couleur…", PickColor);
        row.Children.Add(customColor); Separator();
        width = new Slider { Minimum = 1, Maximum = 30, Value = 4, Width = 58, Height = 24, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2, 3, 2, 3), SmallChange = 1, TickFrequency = 1, IsSnapToTickEnabled = true, ToolTip = "Épaisseur du trait" };
        AutomationProperties.SetName(width, "Épaisseur du trait");
        width.ValueChanged += (_, _) =>
        {
            if (syncing) return;
            var annotation = controller.Settings.Annotation;
            if (annotation.Tool == AnnotationTool.Highlighter) annotation.HighlighterWidth = width.Value;
            else if (annotation.Tool == AnnotationTool.Number) annotation.CalloutSize = width.Value;
            else annotation.Width = width.Value;
            controller.SettingsChanged();
        };
        row.Children.Add(width); Separator();
        row.Children.Add(IconButton("undo", "Annuler le dernier trait · Ctrl+Z", controller.UndoDrawing));
        row.Children.Add(IconButton("trash", "Effacer les annotations", controller.ClearDrawing));
        row.Children.Add(IconButton("close", "Terminer le dessin · Échap", controller.ToggleDrawing));
        Content = new Border { Background = Theme.Brush("#292929"), BorderBrush = Outline, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Child = row };
        Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - Width) / 2;
        Top = SystemParameters.WorkArea.Top + 16;
        SourceInitialized += (_, _) =>
        {
            WindowTools.ExcludeFromCapture(this);
            HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook((IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled) =>
            { if (message == 0x0021) { handled = true; return new IntPtr(3); } return IntPtr.Zero; });
        };
        Loaded += (_, _) => { row.Measure(new Size(Width - 2, double.PositiveInfinity)); Height = Math.Max(44, row.DesiredSize.Height + 2); ClampPlacement(); Sync(); };
        controller.StateChanged += Sync;
        Closed += (_, _) => controller.StateChanged -= Sync;
        Sync();
    }
    private void Separator() => row.Children.Add(new Border { Width = 1, Height = 18, Background = Outline, Margin = new Thickness(3, 6, 3, 6) });
    private Button IconButton(string icon, string label, Action action)
    {
        var button = new Button { Content = Icons.Create(icon, 16, Theme.Foreground), Style = compactStyle, Width = 26, Height = 30, ToolTip = label };
        AutomationProperties.SetName(button, label); button.Click += (_, _) => action(); return button;
    }
    private void Sync()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.InvokeAsync(Sync); return; }
        syncing = true;
        try
        {
            var a = controller.Settings.Annotation;
            foreach (var pair in tools)
            {
                pair.Value.Background = pair.Key == a.Tool ? Selected : Brushes.Transparent;
                AutomationProperties.SetItemStatus(pair.Value, pair.Key == a.Tool ? "Sélectionné" : "");
            }
            var current = a.Tool == AnnotationTool.Highlighter ? a.HighlighterColor : a.Color;
            foreach (var pair in swatches) { pair.Value.Stroke = string.Equals(pair.Key, current, StringComparison.OrdinalIgnoreCase) ? Brushes.White : Outline; pair.Value.StrokeThickness = string.Equals(pair.Key, current, StringComparison.OrdinalIgnoreCase) ? 2.5 : 1; }
            customColor.Content = new Ellipse { Width = 15, Height = 15, Fill = Theme.Brush(current), Stroke = swatches.ContainsKey(current) ? Outline : Theme.Accent, StrokeThickness = 2 };
            customColor.ToolTip = "Choisir une couleur… " + current;
            width.Maximum = a.Tool is AnnotationTool.Highlighter or AnnotationTool.Number ? 100 : 30;
            width.Minimum = a.Tool == AnnotationTool.Number ? 12 : 1;
            width.Value = a.Tool == AnnotationTool.Highlighter ? a.HighlighterWidth : a.Tool == AnnotationTool.Number ? a.CalloutSize : a.Width;
            var label = a.Tool == AnnotationTool.Highlighter ? "Largeur du surligneur" : a.Tool == AnnotationTool.Number ? "Taille du repère" : "Épaisseur du trait";
            width.ToolTip = label + " : " + width.Value.ToString("0") + " px";
            AutomationProperties.SetName(width, label);
        }
        finally { syncing = false; }
    }
    private void SetColor(string value)
    {
        if (controller.Settings.Annotation.Tool == AnnotationTool.Highlighter) controller.Settings.Annotation.HighlighterColor = value;
        else controller.Settings.Annotation.Color = value;
        controller.SettingsChanged(); Sync();
    }
    private void PickColor()
    {
        using var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true };
        try { dialog.Color = System.Drawing.ColorTranslator.FromHtml(controller.Settings.Annotation.Tool == AnnotationTool.Highlighter ? controller.Settings.Annotation.HighlighterColor : controller.Settings.Annotation.Color); } catch { }
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) SetColor($"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}");
    }
    private void ClampPlacement() => WindowTools.ClampToCurrentMonitor(this);
    private static string ColorName(string color) => color switch { "#EF4444" => "Rouge", "#FACC15" => "Jaune", "#22C55E" => "Vert", "#3B82F6" => "Bleu", "#FFFFFF" => "Blanc", _ => "Noir" };
    internal static Style CreateCompactStyle()
    {
        var style = new Style(typeof(Button));
        style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        style.Setters.Add(new Setter(Control.ForegroundProperty, Theme.Foreground));
        style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(0)));
        style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0)));
        style.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
        style.Setters.Add(new Setter(Control.TemplateProperty, (ControlTemplate)XamlReader.Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Button">
              <Border x:Name="Chrome" Background="{TemplateBinding Background}" BorderBrush="Transparent" BorderThickness="1" CornerRadius="5">
                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
              </Border>
              <ControlTemplate.Triggers>
                <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Chrome" Property="BorderBrush" Value="#666666"/></Trigger>
                <Trigger Property="IsPressed" Value="True"><Setter TargetName="Chrome" Property="Opacity" Value="0.65"/></Trigger>
                <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Chrome" Property="BorderBrush" Value="#3B82F6"/></Trigger>
                <Trigger Property="IsEnabled" Value="False"><Setter TargetName="Chrome" Property="Opacity" Value="0.4"/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate>
            """)));
        return style;
    }
}
