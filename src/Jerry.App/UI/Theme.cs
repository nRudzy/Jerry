using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Markup;

namespace Jerry.App.UI;

public static class Theme
{
    public static readonly SolidColorBrush Background = Brush("#0B1220");
    public static readonly SolidColorBrush Surface = Brush("#141F30");
    public static readonly SolidColorBrush Foreground = Brush("#F5F1E8");
    public static readonly SolidColorBrush Muted = Brush("#AAB8CB");
    public static readonly SolidColorBrush Accent = Brush("#7DE5C3");
    public static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    public static ResourceDictionary CreateResources()
    {
        var r = new ResourceDictionary();
        var text = new Style(typeof(TextBlock));
        text.Setters.Add(new Setter(TextBlock.ForegroundProperty, Foreground));
        text.Setters.Add(new Setter(TextBlock.FontFamilyProperty, new FontFamily("Segoe UI")));
        r.Add(typeof(TextBlock), text);
        var button = new Style(typeof(Button));
        button.Setters.Add(new Setter(Control.BackgroundProperty, Brush("#20354A")));
        button.Setters.Add(new Setter(Control.ForegroundProperty, Foreground));
        button.Setters.Add(new Setter(Control.BorderBrushProperty, Brush("#3B5068")));
        button.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(14, 9, 14, 9)));
        button.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 4, 8, 4)));
        button.Setters.Add(new Setter(Control.FontSizeProperty, 13d));
        button.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
        button.Setters.Add(new Setter(Control.TemplateProperty, (ControlTemplate)XamlReader.Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="Button">
              <Border x:Name="Chrome" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                      Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}"
                      BorderThickness="2" CornerRadius="7" Padding="{TemplateBinding Padding}">
                <ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}"
                                  VerticalAlignment="{TemplateBinding VerticalContentAlignment}"
                                  RecognizesAccessKey="True"/>
              </Border>
              <ControlTemplate.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                  <Setter TargetName="Chrome" Property="Background" Value="#2C465C"/>
                  <Setter TargetName="Chrome" Property="BorderBrush" Value="#66859E"/>
                </Trigger>
                <Trigger Property="IsPressed" Value="True">
                  <Setter TargetName="Chrome" Property="Background" Value="#163C3B"/>
                  <Setter TargetName="Chrome" Property="BorderBrush" Value="#7DE5C3"/>
                </Trigger>
                <Trigger Property="IsKeyboardFocused" Value="True">
                  <Setter TargetName="Chrome" Property="BorderBrush" Value="#7DE5C3"/>
                </Trigger>
                <Trigger Property="IsEnabled" Value="False">
                  <Setter TargetName="Chrome" Property="Background" Value="#172231"/>
                  <Setter TargetName="Chrome" Property="BorderBrush" Value="#2B3B4D"/>
                  <Setter TargetName="Chrome" Property="Opacity" Value="0.5"/>
                </Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate>
            """)));
        r.Add(typeof(Button), button);
        var check = new Style(typeof(CheckBox));
        check.Setters.Add(new Setter(Control.ForegroundProperty, Foreground));
        check.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 10, 0, 10)));
        check.Setters.Add(new Setter(Control.FontSizeProperty, 14d));
        r.Add(typeof(CheckBox), check);
        var box = new Style(typeof(TextBox));
        box.Setters.Add(new Setter(Control.BackgroundProperty, Background));
        box.Setters.Add(new Setter(Control.ForegroundProperty, Foreground));
        box.Setters.Add(new Setter(Control.BorderBrushProperty, Brush("#40536A")));
        box.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(9, 7, 9, 7)));
        box.Setters.Add(new Setter(Control.FontSizeProperty, 14d));
        box.Setters.Add(new Setter(TextBox.CaretBrushProperty, Accent));
        r.Add(typeof(TextBox), box);
        var combo = new Style(typeof(ComboBox));
        // Native dropdown popup keeps black text against its light system background.
        combo.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Black));
        combo.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8)));
        combo.Setters.Add(new Setter(Control.FontSizeProperty, 14d));
        r.Add(typeof(ComboBox), combo);
        return r;
    }
    public static TextBlock Text(string value, double size = 14, Brush? color = null) => new()
    {
        Text = value, FontSize = size, Foreground = color ?? Foreground,
        TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3, 0, 5)
    };
}
