using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace Jerry.App.UI;

public static class Theme
{
    public static readonly SolidColorBrush Background = Brush("#1D1D1D");
    public static readonly SolidColorBrush Surface = Brush("#242424");
    public static readonly SolidColorBrush Foreground = Brush("#EAEAEA");
    public static readonly SolidColorBrush Muted = Brush("#999999");
    public static readonly SolidColorBrush Accent = Brush("#3B82F6");
    public static readonly SolidColorBrush Line = Brush("#333333");
    public static SolidColorBrush Brush(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); brush.Freeze(); return brush;
    }
    public static ResourceDictionary CreateResources()
    {
        var resources = (ResourceDictionary)XamlReader.Parse("""
        <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
          <Style TargetType="TextBlock">
            <Setter Property="Foreground" Value="#EAEAEA"/><Setter Property="FontFamily" Value="Segoe UI"/><Setter Property="FontSize" Value="13"/>
          </Style>
          <Style TargetType="Button">
            <Setter Property="Background" Value="#2D2D2D"/><Setter Property="Foreground" Value="#EAEAEA"/>
            <Setter Property="BorderBrush" Value="#424242"/><Setter Property="BorderThickness" Value="1"/>
            <Setter Property="Padding" Value="11,6"/><Setter Property="FontFamily" Value="Segoe UI"/><Setter Property="FontSize" Value="13"/>
            <Setter Property="HorizontalContentAlignment" Value="Center"/><Setter Property="VerticalContentAlignment" Value="Center"/>
            <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
            <Setter Property="Template"><Setter.Value>
              <ControlTemplate TargetType="Button">
                <Border x:Name="Chrome" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="5" Padding="{TemplateBinding Padding}">
                  <ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="{TemplateBinding VerticalContentAlignment}" RecognizesAccessKey="True"/>
                </Border>
                <ControlTemplate.Triggers>
                  <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Chrome" Property="Background" Value="#383838"/></Trigger>
                  <Trigger Property="IsPressed" Value="True"><Setter TargetName="Chrome" Property="Background" Value="#25436B"/></Trigger>
                  <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Chrome" Property="BorderBrush" Value="#6BA5FF"/></Trigger>
                  <Trigger Property="IsEnabled" Value="False"><Setter TargetName="Chrome" Property="Opacity" Value="0.4"/></Trigger>
                </ControlTemplate.Triggers>
              </ControlTemplate>
            </Setter.Value></Setter>
          </Style>
          <Style TargetType="CheckBox">
            <Setter Property="Foreground" Value="#EAEAEA"/><Setter Property="FontFamily" Value="Segoe UI"/><Setter Property="FontSize" Value="13"/>
            <Setter Property="MinHeight" Value="42"/><Setter Property="Padding" Value="12,8"/>
            <Setter Property="HorizontalContentAlignment" Value="Stretch"/><Setter Property="VerticalContentAlignment" Value="Center"/>
            <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
            <Setter Property="Template"><Setter.Value>
              <ControlTemplate TargetType="CheckBox">
                <Border x:Name="Hit" Background="Transparent" Padding="{TemplateBinding Padding}">
                  <Grid>
                    <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="Auto"/></Grid.ColumnDefinitions>
                    <ContentPresenter VerticalAlignment="Center" Margin="0,0,16,0" RecognizesAccessKey="True"/>
                    <Border x:Name="Track" Grid.Column="1" Width="34" Height="20" Background="#484848" CornerRadius="10" BorderThickness="1" BorderBrush="#555555" VerticalAlignment="Center">
                      <Ellipse x:Name="Thumb" Width="16" Height="16" Fill="#D8D8D8" HorizontalAlignment="Left" Margin="1"/>
                    </Border>
                  </Grid>
                </Border>
                <ControlTemplate.Triggers>
                  <Trigger Property="IsChecked" Value="True">
                    <Setter TargetName="Track" Property="Background" Value="#3B82F6"/><Setter TargetName="Track" Property="BorderBrush" Value="#4D8FF8"/>
                    <Setter TargetName="Thumb" Property="HorizontalAlignment" Value="Right"/><Setter TargetName="Thumb" Property="Fill" Value="#FFFFFF"/>
                  </Trigger>
                  <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Hit" Property="Background" Value="#0AFFFFFF"/></Trigger>
                  <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Track" Property="BorderBrush" Value="#FFFFFF"/></Trigger>
                  <Trigger Property="IsEnabled" Value="False"><Setter TargetName="Hit" Property="Opacity" Value="0.4"/></Trigger>
                </ControlTemplate.Triggers>
              </ControlTemplate>
            </Setter.Value></Setter>
          </Style>
          <Style TargetType="TextBox">
            <Setter Property="Background" Value="#2C2C2C"/><Setter Property="Foreground" Value="#EAEAEA"/><Setter Property="BorderBrush" Value="#454545"/>
            <Setter Property="BorderThickness" Value="1"/><Setter Property="Padding" Value="8,5"/><Setter Property="CaretBrush" Value="#6BA5FF"/>
            <Setter Property="SelectionBrush" Value="#3B82F6"/><Setter Property="FontFamily" Value="Segoe UI"/><Setter Property="FontSize" Value="13"/>
            <Setter Property="VerticalContentAlignment" Value="Center"/>
          </Style>
          <Style TargetType="ComboBoxItem">
            <Setter Property="Foreground" Value="#EAEAEA"/><Setter Property="Padding" Value="9,6"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBoxItem">
              <Border x:Name="Row" Background="Transparent" Padding="{TemplateBinding Padding}"><ContentPresenter/></Border>
              <ControlTemplate.Triggers>
                <Trigger Property="IsHighlighted" Value="True"><Setter TargetName="Row" Property="Background" Value="#354B68"/></Trigger>
                <Trigger Property="IsSelected" Value="True"><Setter TargetName="Row" Property="Background" Value="#264166"/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="ComboBox">
            <Setter Property="Foreground" Value="#EAEAEA"/><Setter Property="Background" Value="#2C2C2C"/>
            <Setter Property="FontFamily" Value="Segoe UI"/><Setter Property="FontSize" Value="13"/><Setter Property="MinHeight" Value="29"/>
            <Setter Property="MaxDropDownHeight" Value="280"/><Setter Property="ScrollViewer.HorizontalScrollBarVisibility" Value="Disabled"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBox">
              <Grid>
                <ToggleButton x:Name="Toggle" BorderBrush="#454545" Focusable="False" ClickMode="Press" IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}">
                  <ToggleButton.Template><ControlTemplate TargetType="ToggleButton">
                    <Border x:Name="Box" Background="#2C2C2C" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="1" CornerRadius="5">
                      <Path Data="M0 0 L4 4 8 0" Stroke="#AAAAAA" StrokeThickness="1.3" HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,10,0"/>
                    </Border>
                    <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Box" Property="Background" Value="#383838"/></Trigger></ControlTemplate.Triggers>
                  </ControlTemplate></ToggleButton.Template>
                </ToggleButton>
                <ContentPresenter Margin="9,4,30,4" VerticalAlignment="Center" Content="{TemplateBinding SelectionBoxItem}" ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}" IsHitTestVisible="False"/>
                <Popup x:Name="PART_Popup" Placement="Bottom" AllowsTransparency="True" Focusable="False" IsOpen="{TemplateBinding IsDropDownOpen}" PopupAnimation="Fade">
                  <Border Background="#2A2A2A" BorderBrush="#484848" BorderThickness="1" CornerRadius="5" MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}" MaxHeight="{TemplateBinding MaxDropDownHeight}" Padding="3">
                    <ScrollViewer CanContentScroll="True"><ItemsPresenter/></ScrollViewer>
                  </Border>
                </Popup>
              </Grid>
              <ControlTemplate.Triggers>
                <Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter TargetName="Toggle" Property="BorderBrush" Value="#6BA5FF"/></Trigger>
                <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.4"/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="Slider">
            <Setter Property="Height" Value="24"/><Setter Property="VerticalAlignment" Value="Center"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Slider">
              <Grid Background="Transparent">
                <Border x:Name="Focus" BorderBrush="Transparent" BorderThickness="1" CornerRadius="4" Margin="-3,0"/>
                <Border Height="3" Background="#505050" CornerRadius="1.5" VerticalAlignment="Center"/>
                <Track x:Name="PART_Track">
                  <Track.DecreaseRepeatButton><RepeatButton Command="Slider.DecreaseLarge" Focusable="False"><RepeatButton.Template><ControlTemplate TargetType="RepeatButton"><Border Height="3" Background="#3B82F6" CornerRadius="1.5" VerticalAlignment="Center"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>
                  <Track.Thumb><Thumb Width="12" Height="12" Focusable="False"><Thumb.Template><ControlTemplate TargetType="Thumb"><Ellipse Fill="#3B82F6"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
                  <Track.IncreaseRepeatButton><RepeatButton Command="Slider.IncreaseLarge" Focusable="False"><RepeatButton.Template><ControlTemplate TargetType="RepeatButton"><Border Background="Transparent"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>
                </Track>
              </Grid>
              <ControlTemplate.Triggers><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Focus" Property="BorderBrush" Value="#6BA5FF"/></Trigger></ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="ListBoxItem">
            <Setter Property="HorizontalContentAlignment" Value="Stretch"/><Setter Property="Padding" Value="10,7"/><Setter Property="Margin" Value="6,1"/>
            <Setter Property="FontSize" Value="13"/><Setter Property="FocusVisualStyle" Value="{x:Null}"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ListBoxItem">
              <Border x:Name="Row" Background="Transparent" CornerRadius="5" Padding="{TemplateBinding Padding}"><ContentPresenter/></Border>
              <ControlTemplate.Triggers>
                <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Row" Property="Background" Value="#303030"/></Trigger>
                <Trigger Property="IsSelected" Value="True"><Setter TargetName="Row" Property="Background" Value="#293B54"/></Trigger>
                <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Row" Property="BorderBrush" Value="#6BA5FF"/><Setter TargetName="Row" Property="BorderThickness" Value="1"/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="ScrollBar">
            <Setter Property="Width" Value="8"/><Setter Property="Background" Value="Transparent"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ScrollBar">
              <Track x:Name="PART_Track" IsDirectionReversed="True"><Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType="Thumb"><Border Background="#555555" CornerRadius="3" Margin="1,2"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb></Track>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="ToolTip"><Setter Property="Background" Value="#323232"/><Setter Property="Foreground" Value="#EAEAEA"/><Setter Property="BorderBrush" Value="#505050"/><Setter Property="Padding" Value="8,5"/></Style>
        </ResourceDictionary>
        """);
        resources[SystemParameters.VerticalScrollBarWidthKey] = 8d;
        return resources;
    }
    public static TextBlock Text(string value, double size = 13, Brush? color = null) => new()
    {
        Text = value, FontSize = size, Foreground = color ?? Foreground, FontFamily = new FontFamily("Segoe UI"),
        TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center
    };
}
