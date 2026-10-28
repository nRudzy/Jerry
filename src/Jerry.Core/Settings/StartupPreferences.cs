using System.Text.Json;
using System.Text.Json.Nodes;

namespace Jerry.Core.Settings;

/// <summary>Changes only the startup preference; unknown/newer settings survive maintenance.</summary>
public static class StartupPreferences
{
    public static void SetEnabled(string path, bool enabled)
    {
        var document = File.Exists(path)
            ? JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? throw new JsonException("Les réglages Jerry doivent être un objet JSON.")
            : new JsonObject();
        var names = document.Select(p => p.Key).Where(k => k.Equals("LaunchAtLogin", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (names.Length > 1) throw new JsonException("La préférence de démarrage est ambiguë.");
        document[names.SingleOrDefault() ?? "LaunchAtLogin"] = enabled;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".startup-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
