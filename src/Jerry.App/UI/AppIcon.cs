using System.Drawing;
using System.Windows;

namespace Jerry.App.UI;

internal static class AppIcon
{
    public static Icon Create()
    {
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/Jerry;component/Assets/Branding/jerry.ico"))
            ?? throw new InvalidOperationException("L’icône intégrée de Jerry est introuvable.");
        using var stream = resource.Stream;
        using var icon = new Icon(stream, 32, 32);
        return (Icon)icon.Clone();
    }
}
