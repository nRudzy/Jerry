using System.Drawing;
using System.Windows;
using DrawingSize = System.Drawing.Size;

namespace Jerry.App.UI;

internal static class AppIcon
{
    public static Icon Create()
        => Load(new DrawingSize(32, 32));

    public static Icon CreateTray()
        => Load(System.Windows.Forms.SystemInformation.SmallIconSize);

    private static Icon Load(DrawingSize size)
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Jerry;component/Assets/Branding/jerry.ico"))
            ?? throw new InvalidOperationException("L’icône intégrée de Jerry est introuvable.");
        using var stream = resource.Stream;
        using var icon = new Icon(stream, size);
        return (Icon)icon.Clone();
    }
}
