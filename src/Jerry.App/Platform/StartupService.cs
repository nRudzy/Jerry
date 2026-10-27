using Microsoft.Win32;

namespace Jerry.App.Platform;

public static class StartupService
{
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled) key.SetValue("Jerry", "\"" + Path.Combine(AppContext.BaseDirectory, "Jerry.exe") + "\" --background");
        else key.DeleteValue("Jerry", false);
    }
}
