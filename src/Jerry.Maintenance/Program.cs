using Jerry.Windows;

namespace Jerry.Maintenance;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args is ["--prepare-update"]) return InstanceControl.PrepareUpdateAsync().GetAwaiter().GetResult();
            if (args is ["--startup-status"]) return StartupService.IsEnabled() ? 20 : 0;
            if (args is ["--configure-startup=enable"] or ["--configure-startup=disable"])
            {
                using var instance = InstanceControl.TryClaimInstance();
                if (instance == null) return 10;
                var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Jerry", "settings.json");
                StartupService.Configure(args[0].EndsWith("=enable", StringComparison.Ordinal), settingsPath);
                return 0;
            }
            return 2;
        }
        catch (Exception error)
        {
            // Installer logs the exit code; no UI, models, captures or settings recovery here.
            Console.Error.WriteLine(error.Message);
            return 12;
        }
    }
}
