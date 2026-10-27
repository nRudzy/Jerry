using Jerry.Core.Settings;

namespace Jerry.Core.Tests;

public sealed class SettingsTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Jerry-tests-" + Guid.NewGuid());
    public SettingsTests() => Directory.CreateDirectory(directory);

    [Fact]
    public void SavesAndReloadsIndependentEffectsAndPrivateMediaPaths()
    {
        var store = new SettingsStore(Path.Combine(directory, "settings.json"));
        var value = new AppSettings();
        value.Spotlight.Enabled = true;
        value.Trail.Style = TrailStyle.Kawaii;
        value.Recording.OutputDirectory = @"D:\Mes vidéos\Prises";
        value.Hotkeys["record"] = "Ctrl+Alt+R";
        store.Save(value);
        var loaded = store.Load();
        Assert.True(loaded.Spotlight.Enabled);
        Assert.Equal(TrailStyle.Kawaii, loaded.Trail.Style);
        Assert.Equal(@"D:\Mes vidéos\Prises", loaded.Recording.OutputDirectory);
        Assert.Equal("Ctrl+Alt+R", loaded.Hotkeys["record"]);
    }

    [Fact]
    public void CorruptSettingsArePreservedAndRecoverToUsableDefaults()
    {
        var path = Path.Combine(directory, "settings.json");
        File.WriteAllText(path, "{broken");
        var store = new SettingsStore(path);
        var loaded = store.Load();
        Assert.True(loaded.Enabled);
        Assert.NotNull(store.RecoveryMessage);
        Assert.Contains(Directory.GetFiles(directory), p => File.ReadAllText(p) == "{broken");
    }

    [Fact]
    public void SettingsClampInvalidGeometryAndRetainUnassignedShortcuts()
    {
        var store = new SettingsStore(Path.Combine(directory, "settings.json"));
        var settings = new AppSettings();
        settings.Spotlight.Radius = -40;
        settings.Recording.FramesPerSecond = 0;
        store.Save(settings);
        var loaded = store.Load();
        Assert.InRange(loaded.Spotlight.Radius, 10, 1000);
        Assert.Equal(30, loaded.Recording.FramesPerSecond);
        Assert.All(loaded.Hotkeys.Values, value => Assert.Equal("", value));
    }
    public void Dispose() => Directory.Delete(directory, true);
}
