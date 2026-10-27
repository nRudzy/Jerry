using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jerry.Core.Settings;

public sealed class SettingsStore(string path)
{
    public string FilePath => path;
    public string? RecoveryMessage { get; private set; }
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };
    public AppSettings Load()
    {
        RecoveryMessage = null;
        if (!File.Exists(path)) return new AppSettings();
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Json)
                ?? throw new JsonException("Le fichier de réglages est vide.");
            Validate(settings);
            return settings;
        }
        catch (JsonException)
        {
            var backup = path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
            File.Copy(path, backup, false);
            RecoveryMessage = "Réglages illisibles : une copie a été conservée dans " + backup;
            return new AppSettings();
        }
    }
    public void Save(AppSettings settings)
    {
        Validate(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temp = path + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, Json));
            if (File.Exists(path)) File.Copy(path, path + ".bak", true);
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static void Validate(AppSettings s)
    {
        s.Spotlight ??= new(); s.Ring ??= new(); s.Trail ??= new(); s.Click ??= new();
        s.Sound ??= new(); s.Locate ??= new(); s.Annotation ??= new(); s.Keystrokes ??= new();
        s.Zoom ??= new(); s.Focus ??= new(); s.Recording ??= new(); s.Camera ??= new(); s.Captions ??= new();
        s.Hotkeys ??= new();
        s.TrayItems ??= new AppSettings().TrayItems;
        foreach (var item in new AppSettings().TrayItems) s.TrayItems.TryAdd(item.Key, item.Value);
        s.UpdateRate = Math.Clamp(s.UpdateRate, 15, 120);
        foreach (var key in new AppSettings().Hotkeys.Keys) s.Hotkeys.TryAdd(key, "");
        s.Spotlight.Radius = Clamp(s.Spotlight.Radius, 10, 1000, 145);
        s.Spotlight.BarThickness = Clamp(s.Spotlight.BarThickness, 10, 2000, 140);
        s.Spotlight.Opacity = Clamp(s.Spotlight.Opacity, 0, 1, .65);
        s.Spotlight.Color = Color(s.Spotlight.Color, "#0B1020");
        s.Spotlight.HoldDelayMilliseconds = Math.Clamp(s.Spotlight.HoldDelayMilliseconds, 0, 2000);
        s.Spotlight.MoveDistance = Clamp(s.Spotlight.MoveDistance, 1, 1000, 20);
        s.Spotlight.AreaOpacity = Clamp(s.Spotlight.AreaOpacity, 0, 1, 0);
        s.Spotlight.HighlightColor = Color(s.Spotlight.HighlightColor, "#FFFFFF");
        if (s.Spotlight.HoldKey != "None") s.Spotlight.TurnOffWhenMoving = false;
        s.Ring.Radius = Clamp(s.Ring.Radius, 3, 300, 22);
        s.Ring.Thickness = Clamp(s.Ring.Thickness, .5, 30, 2.5);
        s.Ring.Opacity = Clamp(s.Ring.Opacity, 0, 1, .9);
        s.Ring.Speed = Clamp(s.Ring.Speed, .1, 5, 1);
        s.Ring.Color = Color(s.Ring.Color, "#7DE5C3");
        s.Ring.FollowSpeed = Clamp(s.Ring.FollowSpeed, .01, 1, .35);
        s.Trail.Length = Math.Clamp(s.Trail.Length, 2, 150);
        s.Trail.Width = Clamp(s.Trail.Width, 1, 50, 6);
        s.Trail.Opacity = Clamp(s.Trail.Opacity, 0, 1, .8);
        s.Trail.FadeMilliseconds = Math.Clamp(s.Trail.FadeMilliseconds, 100, 5000);
        s.Trail.Color = Color(s.Trail.Color, "#7DE5C3");
        s.Trail.SpawnIntervalMilliseconds = Math.Clamp(s.Trail.SpawnIntervalMilliseconds, 1, 300);
        s.Click.Size = Clamp(s.Click.Size, 5, 350, 55);
        s.Click.Thickness = Clamp(s.Click.Thickness, .5, 30, 3);
        s.Click.Speed = Clamp(s.Click.Speed, .1, 5, 1);
        s.Click.Color = Color(s.Click.Color, "#7DE5C3");
        s.Sound.Volume = Clamp(s.Sound.Volume, 0, 1, .45);
        s.Locate.Sensitivity = Clamp(s.Locate.Sensitivity, 0, 1, .6);
        s.Annotation.Width = Clamp(s.Annotation.Width, 1, 40, 4);
        s.Annotation.FontSize = Clamp(s.Annotation.FontSize, 10, 120, 28);
        s.Annotation.FadeSeconds = Clamp(s.Annotation.FadeSeconds, 0, 120, 0);
        s.Annotation.Color = Color(s.Annotation.Color, "#FFC86D");
        s.Annotation.Text ??= "Note";
        s.Annotation.HighlighterWidth = Clamp(s.Annotation.HighlighterWidth, 2, 200, 22);
        s.Annotation.CalloutSize = Clamp(s.Annotation.CalloutSize, 12, 160, 32);
        s.Annotation.HighlighterColor = Color(s.Annotation.HighlighterColor, "#FFE36D");
        s.Keystrokes.FontSize = Clamp(s.Keystrokes.FontSize, 16, 120, 24);
        s.Keystrokes.DurationMilliseconds = Math.Clamp(s.Keystrokes.DurationMilliseconds, 1000, 5000);
        s.Keystrokes.AccentColor = Color(s.Keystrokes.AccentColor, "#7DE5C3");
        s.Zoom.Scale = Clamp(s.Zoom.Scale, 1, 5, 1.75);
        s.Zoom.LoupeScale = Clamp(s.Zoom.LoupeScale, 1, 8, 2);
        s.Zoom.LoupeDiameter = Clamp(s.Zoom.LoupeDiameter, 80, 600, 210);
        s.Zoom.AutoDurationMilliseconds = Math.Clamp(s.Zoom.AutoDurationMilliseconds, 200, 10000);
        s.Zoom.AnimationMilliseconds = Math.Clamp(s.Zoom.AnimationMilliseconds, 0, 2000);
        s.Zoom.MovementSpeed = Clamp(s.Zoom.MovementSpeed, .01, 1, .3);
        s.Zoom.AutoScale = Clamp(s.Zoom.AutoScale, 1, 5, 1.75);
        s.Focus.Opacity = Clamp(s.Focus.Opacity, 0, 1, .65);
        s.Recording.FramesPerSecond = s.Recording.FramesPerSecond == 60 ? 60 : 30;
        s.Recording.CountdownSeconds = Math.Clamp(s.Recording.CountdownSeconds, 0, 10);
        if (string.IsNullOrWhiteSpace(s.Recording.OutputDirectory)) s.Recording.OutputDirectory = new RecordingOptions().OutputDirectory;
        s.Camera.Size = Clamp(s.Camera.Size, 80, 600, 180);
        s.Camera.X = Clamp(s.Camera.X, 0, 1, .86); s.Camera.Y = Clamp(s.Camera.Y, 0, 1, .78);
        s.Captions.FontSize = Clamp(s.Captions.FontSize, 12, 140, 44);
        s.Captions.WordsPerCaption = Math.Clamp(s.Captions.WordsPerCaption, 1, 30);
        s.Captions.Lines = Math.Clamp(s.Captions.Lines, 1, 3);
        s.Captions.X = Clamp(s.Captions.X, 0, 1, .5); s.Captions.Y = Clamp(s.Captions.Y, 0, 1, .85);
        s.Captions.LingerSeconds = Clamp(s.Captions.LingerSeconds, .5, 20, 3);
        s.Captions.Color = Color(s.Captions.Color, "#FFFFFF");
        s.Captions.AccentColor = Color(s.Captions.AccentColor, "#7DE5C3");
        s.Captions.FadeMilliseconds = Math.Clamp(s.Captions.FadeMilliseconds, 0, 3000);
    }
    private static double Clamp(double value, double min, double max, double fallback)
        => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
    private static string Color(string? color, string fallback)
        => color is { Length: 7 } && color[0] == '#' && uint.TryParse(color.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _) ? color : fallback;
}
