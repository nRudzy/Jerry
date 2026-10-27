using Jerry.Core.Settings;

namespace Jerry.Core.Recording;

public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
}
public static class RecordingGeometry
{
    public static (int Width, int Height) OutputSize(int width, int height, OutputResolution resolution)
    {
        if (width < 2 || height < 2) throw new ArgumentOutOfRangeException(nameof(width), "La capture doit mesurer au moins deux pixels par axe.");
        return resolution switch
        {
            OutputResolution.P720 => (1280, 720), OutputResolution.P1080 => (1920, 1080),
            OutputResolution.P2160 => (3840, 2160), _ => (width / 2 * 2, height / 2 * 2)
        };
    }
    public static string BuildFilter(int width, int height, OutputResolution resolution, VideoFillMode fill)
    {
        var (w, h) = OutputSize(width, height, resolution);
        return fill switch
        {
            VideoFillMode.Crop => $"scale={w}:{h}:force_original_aspect_ratio=increase,crop={w}:{h},setsar=1",
            VideoFillMode.Stretch => $"scale={w}:{h},setsar=1",
            _ => $"scale={w}:{h}:force_original_aspect_ratio=decrease,pad={w}:{h}:(ow-iw)/2:(oh-ih)/2,setsar=1"
        };
    }
}
