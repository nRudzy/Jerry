using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Path = System.Windows.Shapes.Path;

namespace Jerry.App.UI;

public static class Icons
{
    public static FrameworkElement Create(string name, double size = 18, Brush? brush = null)
    {
        var data = name switch
        {
            "settings" => "M9 3 L15 3 16 6 19 7 21 10 19 12 20 15 18 18 15 18 13 21 10 21 8 18 5 18 3 15 4 12 3 9 6 7 8 7 Z M15.5 12 A3.5 3.5 0 1 1 8.5 12 A3.5 3.5 0 1 1 15.5 12",
            "ring" or "circle" => "M21 12 A9 9 0 1 1 3 12 A9 9 0 1 1 21 12",
            "click" => "M5 14 A8 8 0 1 1 19 9 M10 7 L10 20 13 16 16 21 18 20 15 15 20 15 Z",
            "sound" => "M3 9 L7 9 12 5 12 19 7 15 3 15 Z M16 8 Q20 12 16 16 M19 4 Q26 12 19 20",
            "trail" => "M3 16 C7 4 11 3 10 8 S2 19 8 18 17 4 18 8 12 20 21 16",
            "spotlight" => "M8 3 L16 3 16 6 14 10 14 20 10 20 10 10 8 6 Z M8 6 L16 6 M11 14 L13 14",
            "locate" or "shake" => "M3 10 L3 14 M6 6 L6 18 M9 9 L9 15 M12 3 L12 21 M15 7 L15 17 M18 5 L18 19 M21 10 L21 14",
            "annotation" => "M21 12 A9 9 0 1 1 3 12 A9 9 0 1 1 21 12 M8 16 L12 7 16 16 M9.5 13 L14.5 13",
            "keys" or "shortcuts" => "M3 5 L21 5 21 19 3 19 Z M6 9 L7 9 M10 9 L11 9 M14 9 L15 9 M18 9 L18.5 9 M6 12 L7 12 M10 12 L11 12 M14 12 L15 12 M18 12 L18.5 12 M8 16 L16 16",
            "zoom" or "loupe" => "M17 10 A7 7 0 1 1 3 10 A7 7 0 1 1 17 10 M15 15 L21 21",
            "focus" => "M3 9 L3 4 8 4 M16 4 L21 4 21 9 M21 15 L21 20 16 20 M8 20 L3 20 3 15 M8 8 L16 8 16 16 8 16 Z",
            "record" => "M3 5 L16 5 16 19 3 19 Z M16 10 L22 6 22 18 16 14",
            "camera" => "M3 7 L7 7 9 4 15 4 17 7 21 7 21 20 3 20 Z M16 13 A4 4 0 1 1 8 13 A4 4 0 1 1 16 13",
            "captions" => "M3 4 L21 4 21 20 3 20 Z M6 9 L9 9 M12 9 L18 9 M6 13 L13 13 M16 13 L18 13 M6 17 L10 17",
            "menu" => "M3 6 L21 6 M3 12 L21 12 M3 18 L21 18",
            "pen" => "M4 16 L3 21 8 20 20 8 Q22 6 20 4 Q18 2 16 4 Z M14 6 L18 10 M4 16 L8 20",
            "arrow" => "M4 20 L20 4 M9 4 L20 4 20 15",
            "rectangle" => "M3 5 L21 5 21 19 3 19 Z",
            "line" => "M4 20 L20 4",
            "highlighter" => "M4 16 L8 20 20 8 16 4 Z M12 8 L16 12 M5 19 L3 21 9 21",
            "text" => "M3 18 L8 5 13 18 M5 13 L11 13 M16 11 C22 8 23 20 17 18 C13 17 17 13 21 14 M21 10 L21 19",
            "number" => "M21 12 A9 9 0 1 1 3 12 A9 9 0 1 1 21 12 M9 10 L12 7 12 17 M9 17 L15 17",
            "undo" => "M8 4 L3 9 8 14 M3 9 L15 9 C23 9 23 20 15 20 L11 20",
            "trash" => "M4 6 L20 6 M9 6 L9 3 15 3 15 6 M6 6 L7 21 17 21 18 6 M10 10 L10 17 M14 10 L14 17",
            "close" => "M6 6 L18 18 M18 6 L6 18",
            "stop" => "M6 6 L18 6 18 18 6 18 Z",
            "grip" => "M8 5 L8 6 M15 5 L15 6 M8 11 L8 12 M15 11 L15 12 M8 17 L8 18 M15 17 L15 18",
            "minimize" => "M5 12 L19 12",
            "maximize" => "M5 5 L19 5 19 19 5 19 Z",
            "folder" => "M3 7 L3 20 21 20 21 7 12 7 9 4 3 4 Z",
            "power" => "M12 3 L12 12 M6 6 A8 8 0 1 0 18 6",
            "chevron" => "M8 5 L15 12 8 19",
            _ => "M5 3 L19 12 12 13 9 20 Z"
        };
        var geometry = Geometry.Parse(data); geometry.Freeze();
        var path = new Path { Data = geometry, Stroke = brush ?? Theme.Foreground, StrokeThickness = 1.65,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round };
        var canvas = new Canvas { Width = 24, Height = 24, IsHitTestVisible = false };
        canvas.Children.Add(path);
        return new Viewbox { Width = size, Height = size, Child = canvas, IsHitTestVisible = false };
    }
}
