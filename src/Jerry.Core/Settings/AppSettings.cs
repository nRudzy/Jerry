using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jerry.Core.Settings;

public enum SpotlightShape { Circle, HorizontalBar, VerticalBar }
public enum TrailStyle { Particles, Glow, SolidLine, Dots, DashedLine, Stars, Kawaii }
public enum ClickStyle { RippleWave, RingBurst, ColorFlash, ParticleBurst, GlowPulse, ScaleBounce, SingleRing }
public enum AnnotationTool { Freehand, Arrow, Rectangle, Circle, Line, Highlighter, Text, Number }
public enum KeyDisplayStyle { Classic, Glass, Keycaps, Neon, MouzzGradient, Minimal }
public enum CaptionStyle { CleanBar, BoldOutline, Minimal, WordFill, WordPop, SlideUp }
public enum OutputResolution { Native, P720, P1080, P2160 }
public enum VideoFillMode { Fit, Crop, Stretch }
public enum CameraShape { Circle, Card }
public enum CameraBorderStyle { None, White, Gradient }

public sealed class AppSettings
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalSettings { get; set; }
    public int Version { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public bool LaunchAtLogin { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public int UpdateRate { get; set; } = 60;
    public Dictionary<string, bool> TrayItems { get; set; } = new()
    {
        ["spotlight"] = true, ["ring"] = true, ["trail"] = true, ["click"] = true,
        ["annotation"] = true, ["zoom"] = true, ["loupe"] = true, ["focus"] = true,
        ["camera"] = true, ["captions"] = true, ["sound"] = true,
        ["locate"] = true, ["keystrokes"] = true
    };
    public SpotlightOptions Spotlight { get; set; } = new();
    public RingOptions Ring { get; set; } = new();
    public TrailOptions Trail { get; set; } = new();
    public ClickOptions Click { get; set; } = new();
    public SoundOptions Sound { get; set; } = new();
    public LocateOptions Locate { get; set; } = new();
    public AnnotationOptions Annotation { get; set; } = new();
    public KeystrokeOptions Keystrokes { get; set; } = new();
    public ZoomOptions Zoom { get; set; } = new();
    public FocusOptions Focus { get; set; } = new();
    public RecordingOptions Recording { get; set; } = new();
    public CameraOptions Camera { get; set; } = new();
    public CaptionOptions Captions { get; set; } = new();
    public Dictionary<string, string> Hotkeys { get; set; } = new()
    {
        ["master"] = "", ["spotlight"] = "", ["ring"] = "", ["trail"] = "",
        ["click"] = "", ["annotation"] = "", ["clear"] = "", ["zoom"] = "",
        ["loupe"] = "", ["focus"] = "", ["record"] = "", ["captions"] = "",
        ["flash"] = "", ["zoom-in"] = "", ["zoom-out"] = "", ["camera"] = "",
        ["keystrokes"] = "", ["sound"] = "", ["locate"] = ""
    };
}

public sealed class SpotlightOptions
{
    public bool Enabled { get; set; }
    public SpotlightShape Shape { get; set; }
    public double Radius { get; set; } = 145;
    public double BarThickness { get; set; } = 140;
    public double Opacity { get; set; } = .65;
    public string Color { get; set; } = "#0B1020";
    public bool HideCursor { get; set; }
    public bool TurnOffWhenMoving { get; set; }
    public string HoldKey { get; set; } = "None";
    public double MoveDistance { get; set; } = 20;
    public int HoldDelayMilliseconds { get; set; } = 200;
    public double AreaOpacity { get; set; }
    public string HighlightColor { get; set; } = "#FFFFFF";
}
public sealed class RingOptions
{
    public bool Enabled { get; set; } = true;
    public double Radius { get; set; } = 22;
    public double Thickness { get; set; } = 2.5;
    public double Opacity { get; set; } = .9;
    public string Color { get; set; } = "#7DE5C3";
    public bool Animate { get; set; }
    public double Speed { get; set; } = 1;
    public bool NeonGlow { get; set; }
    public double FollowSpeed { get; set; } = .35;
}
public sealed class TrailOptions
{
    public bool Enabled { get; set; }
    public TrailStyle Style { get; set; }
    public string Color { get; set; } = "#7DE5C3";
    public int Length { get; set; } = 28;
    public double Width { get; set; } = 6;
    public double Opacity { get; set; } = .8;
    public int FadeMilliseconds { get; set; } = 650;
    public int SpawnIntervalMilliseconds { get; set; } = 12;
}
public sealed class ClickOptions
{
    public bool Enabled { get; set; }
    public bool Automatic { get; set; } = true;
    public ClickStyle Style { get; set; } = ClickStyle.SingleRing;
    public string Color { get; set; } = "#7DE5C3";
    public double Size { get; set; } = 55;
    public double Thickness { get; set; } = 3;
    public double Speed { get; set; } = 1;
    public string TriggerKey { get; set; } = "None";
}
public sealed class SoundOptions
{
    public bool Enabled { get; set; }
    public string FilePath { get; set; } = "";
    public double Volume { get; set; } = .45;
}
public sealed class LocateOptions
{
    public bool Enabled { get; set; } = true;
    public double Sensitivity { get; set; } = .6;
}
public sealed class AnnotationOptions
{
    public bool Enabled { get; set; } = true;
    public AnnotationTool Tool { get; set; }
    public string HoldKey { get; set; } = "None";
    public string Color { get; set; } = "#FFC86D";
    public double Width { get; set; } = 4;
    public double FadeSeconds { get; set; }
    public bool ClearOnRelease { get; set; } = true;
    public string Text { get; set; } = "Note";
    public double FontSize { get; set; } = 28;
    public double HighlighterWidth { get; set; } = 22;
    public double CalloutSize { get; set; } = 32;
    public bool HighlighterSnap { get; set; } = true;
    public string HighlighterColor { get; set; } = "#FFE36D";
}
public sealed class KeystrokeOptions
{
    public bool Enabled { get; set; }
    public KeyDisplayStyle Style { get; set; }
    public double FontSize { get; set; } = 24;
    public int DurationMilliseconds { get; set; } = 1600;
    public bool ModifiersOnly { get; set; } = true;
    public bool ShowSpace { get; set; } = true;
    public bool ShowDelete { get; set; } = true;
    public bool ShowReturn { get; set; } = true;
    public string AccentColor { get; set; } = "#7DE5C3";
}
public sealed class ZoomOptions
{
    public bool Enabled { get; set; }
    public double Scale { get; set; } = 1.75;
    public bool AutoOnClick { get; set; }
    public int AutoDurationMilliseconds { get; set; } = 1200;
    public bool LoupeEnabled { get; set; }
    public double LoupeDiameter { get; set; } = 210;
    public double LoupeScale { get; set; } = 2;
    public int AnimationMilliseconds { get; set; } = 180;
    public double MovementSpeed { get; set; } = .3;
    public double AutoScale { get; set; } = 1.75;
}
public sealed class FocusOptions
{
    public bool Enabled { get; set; }
    public double Opacity { get; set; } = .65;
}
public sealed class RecordingOptions
{
    public string OutputDirectory { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Jerry");
    public OutputResolution Resolution { get; set; } = OutputResolution.P1080;
    public VideoFillMode FillMode { get; set; }
    public int FramesPerSecond { get; set; } = 30;
    public int CountdownSeconds { get; set; } = 3;
    public bool SystemAudio { get; set; } = true;
    public bool Microphone { get; set; }
    public string MicrophoneDeviceId { get; set; } = "";
}
public sealed class CameraOptions
{
    public bool Enabled { get; set; }
    public string DeviceId { get; set; } = "";
    public CameraShape Shape { get; set; }
    public double Size { get; set; } = 180;
    public bool Mirror { get; set; } = true;
    public double X { get; set; } = .86;
    public double Y { get; set; } = .78;
    public CameraBorderStyle Border { get; set; } = CameraBorderStyle.White;
    public bool SnapToCorners { get; set; } = true;
}
public sealed class CaptionOptions
{
    public bool LiveEnabled { get; set; }
    public string Language { get; set; } = "fr";
    public string Model { get; set; } = "base";
    public CaptionStyle Style { get; set; } = CaptionStyle.WordFill;
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 44;
    public string Color { get; set; } = "#FFFFFF";
    public string AccentColor { get; set; } = "#7DE5C3";
    public bool Background { get; set; } = true;
    public int WordsPerCaption { get; set; } = 6;
    public int Lines { get; set; } = 2;
    public double X { get; set; } = .5;
    public double Y { get; set; } = .85;
    public double LingerSeconds { get; set; } = 3;
    public int FadeMilliseconds { get; set; } = 300;
    public bool SmoothAnimation { get; set; } = true;
    public bool AutoOpenAfterRecording { get; set; }
}
