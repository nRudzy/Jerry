using System.Globalization;
using Jerry.Core.Settings;

namespace Jerry.Core.Recording;

public static class RecordingMedia
{
    public static string DirectShowCamera(string log, string deviceId, string name)
    {
        var devices = new List<(string Name, string Id)>();
        string? currentName = null;
        foreach (var line in log.Split('\n'))
        {
            if (line.Contains("(video)", StringComparison.Ordinal))
            {
                var begin = line.IndexOf('"');
                var end = line.LastIndexOf('"');
                currentName = begin >= 0 && end > begin ? line[(begin + 1)..end] : null;
            }
            else if (line.Contains("(audio)", StringComparison.Ordinal)) currentName = null;
            else if (currentName != null && line.Contains("Alternative name", StringComparison.Ordinal))
            {
                var begin = line.IndexOf('"');
                var end = line.LastIndexOf('"');
                if (begin >= 0 && end > begin) devices.Add((currentName, line[(begin + 1)..end]));
                currentName = null;
            }
        }
        static string PhysicalId(string id)
        {
            const string prefix = "@device_pnp_";
            if (id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) id = id[prefix.Length..];
            var category = id.IndexOf("#{", StringComparison.Ordinal);
            return category >= 0 ? id[..category] : id;
        }
        var physical = PhysicalId(deviceId);
        var exact = devices.Where(d => PhysicalId(d.Id).Equals(physical, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (exact.Length == 1) return exact[0].Id;
        var names = devices.Where(d => d.Name == name).ToArray();
        if (names.Length == 1) return names[0].Id;
        throw new InvalidOperationException("La caméra sélectionnée ne peut pas être identifiée sans ambiguïté.");
    }

    public static IReadOnlyList<string> CaptureArguments(PixelRect bounds, RecordingOptions options, string output)
    {
        if (bounds.Width < 2 || bounds.Height < 2) throw new ArgumentOutOfRangeException(nameof(bounds), "La zone de capture est vide.");
        if (options.FramesPerSecond is < 1 or > 120) throw new ArgumentOutOfRangeException(nameof(options.FramesPerSecond));
        return ["-hide_banner", "-n", "-f", "gdigrab", "-framerate", options.FramesPerSecond.ToString(CultureInfo.InvariantCulture),
            "-draw_mouse", "1", "-offset_x", bounds.X.ToString(CultureInfo.InvariantCulture), "-offset_y", bounds.Y.ToString(CultureInfo.InvariantCulture),
            "-video_size", $"{bounds.Width}x{bounds.Height}", "-i", "desktop", "-an", "-vf",
            RecordingGeometry.BuildFilter(bounds.Width, bounds.Height, options.Resolution, options.FillMode),
            "-c:v", "libx264", "-preset", "veryfast", "-tune", "zerolatency", "-crf", "20", "-pix_fmt", "yuv420p",
            "-progress", "pipe:1", "-stats_period", "0.1", "-f", "matroska", output];
    }

    public static IReadOnlyList<string> MuxArguments(string video, IReadOnlyList<(string Path, string Title)> audio, string output)
    {
        var args = new List<string> { "-hide_banner", "-nostdin", "-n", "-i", video };
        foreach (var track in audio) args.AddRange(["-i", track.Path]);
        args.AddRange(["-map", "0:v:0"]);
        for (var i = 0; i < audio.Count; i++) args.AddRange(["-map", $"{i + 1}:a:0", $"-metadata:s:a:{i}", $"title={audio[i].Title}", $"-metadata:s:a:{i}", $"handler_name={audio[i].Title}"]);
        var preferredAudio = 0;
        for (var i = 0; i < audio.Count; i++)
            if (audio[i].Title.Equals("Microphone", StringComparison.OrdinalIgnoreCase)) { preferredAudio = i; break; }
        for (var i = 0; i < audio.Count; i++) args.AddRange([$"-disposition:a:{i}", i == preferredAudio ? "default" : "0"]);
        args.AddRange(["-c:v", "copy"]);
        if (audio.Count > 0) args.AddRange(["-c:a", "aac", "-b:a", "192k"]);
        args.AddRange(["-movflags", "+faststart", output]);
        return args;
    }

    // WASAPI loopback emits no callbacks while the endpoint is silent. Position each
    // packet against the monotonic clock; tolerate one capture buffer of jitter.
    public static long SilenceFrames(long framesWritten, long elapsedFrames, int incomingFrames, int jitterFrames)
    {
        var gap = Math.Max(0, elapsedFrames - incomingFrames - framesWritten);
        return gap > jitterFrames ? gap : 0;
    }
}
