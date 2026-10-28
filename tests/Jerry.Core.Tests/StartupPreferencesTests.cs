using System.Text.Json;
using Jerry.Core.Settings;

namespace Jerry.Core.Tests;

public sealed class StartupPreferencesTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Jerry-Startup-" + Guid.NewGuid().ToString("N"));
    private string SettingsPath => Path.Combine(directory, "settings.json");

    [Fact]
    public void StartupChangeRetainsUnknownFieldsAndOtherPreferences()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(SettingsPath, """{"launchAtLogin":false,"updateRate":42,"future":{"token":"à conserver"},"recording":{"outputDirectory":"D:\\Prises é"}}""");
        StartupPreferences.SetEnabled(SettingsPath, true);
        using var document = JsonDocument.Parse(File.ReadAllText(SettingsPath));
        Assert.True(document.RootElement.GetProperty("launchAtLogin").GetBoolean());
        Assert.Equal(42, document.RootElement.GetProperty("updateRate").GetInt32());
        Assert.Equal("à conserver", document.RootElement.GetProperty("future").GetProperty("token").GetString());
        Assert.Equal(@"D:\Prises é", document.RootElement.GetProperty("recording").GetProperty("outputDirectory").GetString());
    }

    [Fact]
    public void ExistingPropertyCasingIsUpdatedWithoutCreatingADuplicate()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(SettingsPath, "{\"LaunchAtLogin\":true,\"Enabled\":false}");
        StartupPreferences.SetEnabled(SettingsPath, false);
        using var document = JsonDocument.Parse(File.ReadAllText(SettingsPath));
        Assert.Equal(2, document.RootElement.EnumerateObject().Count());
        Assert.False(document.RootElement.GetProperty("LaunchAtLogin").GetBoolean());
        Assert.False(new SettingsStore(SettingsPath).Load().Enabled);
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"LaunchAtLogin\":true,\"launchAtLogin\":false}")]
    public void InvalidPreferencesAreNotReplacedWithDefaults(string content)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(SettingsPath, content);
        Assert.ThrowsAny<Exception>(() => StartupPreferences.SetEnabled(SettingsPath, false));
        Assert.Equal(content, File.ReadAllText(SettingsPath));
        Assert.Single(Directory.GetFiles(directory));
    }

    [Fact]
    public void FreshStartupPreferenceLoadsWithAllApplicationDefaults()
    {
        StartupPreferences.SetEnabled(SettingsPath, true);
        var settings = new SettingsStore(SettingsPath).Load();
        Assert.True(settings.LaunchAtLogin);
        Assert.True(settings.Ring.Enabled);
        Assert.Equal(60, settings.UpdateRate);
    }

    [Fact]
    public void NormalApplicationSaveAfterMaintenanceRetainsFuturePreferences()
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(SettingsPath, "{\"LaunchAtLogin\":true,\"futurePreference\":{\"value\":42}}");
        var store = new SettingsStore(SettingsPath);
        var settings = store.Load();
        settings.Enabled = false;
        store.Save(settings);
        using var document = JsonDocument.Parse(File.ReadAllText(SettingsPath));
        Assert.Equal(42, document.RootElement.GetProperty("futurePreference").GetProperty("value").GetInt32());
        Assert.True(document.RootElement.GetProperty("LaunchAtLogin").GetBoolean());
        Assert.False(document.RootElement.GetProperty("Enabled").GetBoolean());
    }

    public void Dispose() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
}
