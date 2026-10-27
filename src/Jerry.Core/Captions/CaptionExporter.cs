using System.Globalization;
using System.Net;
using System.Text;
using Jerry.Core.Settings;

namespace Jerry.Core.Captions;

public static class CaptionExporter
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string ToSrt(CaptionDocument document)
    {
        var output = new StringBuilder();
        var index = 1;
        foreach (var cue in ValidCues(document))
        {
            output.Append(index++).Append("\r\n").Append(SrtTime(cue.Start)).Append(" --> ").Append(SrtTime(cue.End)).Append("\r\n");
            var lines = cue.Text.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
            output.Append(string.Join("\r\n", lines.Select(WebUtility.HtmlEncode))).Append("\r\n\r\n");
        }
        return output.ToString();
    }

    public static string ToAss(CaptionDocument document, CaptionOptions options, int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentException("Dimensions vidéo invalides.");
        var primary = Color(options.Color);
        var accent = Color(options.AccentColor);
        var requestedFont = options.FontFamily.Equals("Montserrat", StringComparison.OrdinalIgnoreCase) ? "Montserrat ExtraBold"
            : options.FontFamily.Equals("Oswald", StringComparison.OrdinalIgnoreCase) ? "Oswald Bold" : options.FontFamily;
        var font = requestedFont.Replace(",", " ").Replace("\n", " ").Replace("\r", " ");
        var fontSize = Math.Clamp(options.FontSize, 10, 200).ToString("0.##", Invariant);
        var border = options.Style == CaptionStyle.CleanBar && options.Background ? 3 : 1;
        var outline = options.Style == CaptionStyle.BoldOutline ? 3 : options.Style == CaptionStyle.CleanBar ? options.Background ? 8 : 0 : options.Background && options.Style != CaptionStyle.Minimal ? 2 : 0;
        var shadow = options.Style == CaptionStyle.BoldOutline ? 1 : 0;
        // Primary is the filled karaoke color; secondary is the color before the sweep.
        var fillPrimary = options.Style == CaptionStyle.WordFill ? accent : primary;
        var secondary = options.Style == CaptionStyle.WordFill ? primary : accent;
        var output = new StringBuilder($"[Script Info]\r\nScriptType: v4.00+\r\nPlayResX: {width}\r\nPlayResY: {height}\r\nWrapStyle: 2\r\nScaledBorderAndShadow: yes\r\n\r\n[V4+ Styles]\r\nFormat: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding\r\n");
        output.Append($"Style: Default,{font},{fontSize},{fillPrimary},{secondary},&H80000000,&H80000000,-1,0,0,0,100,100,0,0,{border},{outline},{shadow},5,20,20,20,1\r\n");
        output.Append("\r\n[Events]\r\nFormat: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\r\n");
        var x = (int)Math.Round(width * Math.Clamp(options.X, .03, .97));
        var y = (int)Math.Round(height * Math.Clamp(options.Y, .03, .97));
        foreach (var cue in ValidCues(document))
        {
            var position = $"{{\\an5\\pos({x},{y})}}";
            var words = CaptionTiming.GetWords(cue);
            if (options.Style == CaptionStyle.SlideUp)
            {
                // Each event lays out the complete phrase, with only its own word visible.
                // Invisible neighbours reserve identical glyph widths, while each visible
                // word has its own speech-timed movement and fade.
                for (var active = 0; active < words.Count; active++)
                {
                    var start = words[active].Start < cue.Start ? cue.Start : words[active].Start;
                    if (start >= cue.End) continue;
                    var parts = words.Select((word, i) => i == active
                        ? "{\\alpha&H00&}" + Escape(word.Text) + "{\\alpha&HFF&}"
                        : Escape(word.Text)).ToArray();
                    var animation = $"{{\\an5\\move({x},{y + 40},{x},{y},0,220)\\fad(180,120)\\alpha&HFF&}}";
                    Event(output, start, cue.End, animation + JoinLines(parts, options.Lines));
                }
                continue;
            }
            if (options.Style == CaptionStyle.WordPop && words.Count > 0)
            {
                // One event per active word keeps the surrounding text stable and only animates that word.
                for (var active = 0; active < words.Count; active++)
                {
                    var start = active == 0 ? cue.Start : words[active].Start;
                    var end = active + 1 < words.Count ? words[active + 1].Start : cue.End;
                    if (end <= start) continue;
                    var parts = words.Select((word, i) => i == active
                        ? $"{{\\1c{accent}\\fscx115\\fscy115\\t(0,140,\\fscx100\\fscy100)}}{Escape(word.Text)}{{\\rDefault}}"
                        : Escape(word.Text)).ToArray();
                    Event(output, start, end, position + JoinLines(parts, options.Lines));
                }
                continue;
            }
            string text;
            if (options.Style == CaptionStyle.WordFill)
            {
                var parts = new List<string>();
                var cursor = cue.Start;
                foreach (var word in words)
                {
                    var gap = Math.Max(0, Centiseconds(word.Start) - Centiseconds(cursor));
                    var duration = Math.Max(1, Centiseconds(word.End) - Centiseconds(word.Start));
                    parts.Add((gap > 0 ? $"{{\\k{gap}}}" : "") + $"{{\\kf{duration}}}" + Escape(word.Text));
                    cursor = word.End;
                }
                text = JoinLines(parts.ToArray(), options.Lines);
            }
            else if (cue.Text.Contains('\n')) text = Escape(cue.Text);
            else text = JoinLines(CaptionTiming.SplitWords(cue.Text).Select(Escape).ToArray(), options.Lines);
            Event(output, cue.Start, cue.End, position + text);
        }
        return output.ToString();
    }

    private static IEnumerable<CaptionCue> ValidCues(CaptionDocument document)
    {
        foreach (var cue in document.Cues.OrderBy(c => c.Start))
        {
            CaptionTiming.ValidateRange(cue.Start, cue.End);
            if (!string.IsNullOrWhiteSpace(cue.Text)) yield return cue;
        }
    }

    private static void Event(StringBuilder output, TimeSpan start, TimeSpan end, string text) =>
        output.Append($"Dialogue: 0,{AssTime(start)},{AssTime(end)},Default,,0,0,0,,{text}\r\n");

    private static string JoinLines(string[] words, int lines)
    {
        if (words.Length == 0) return "";
        var perLine = (int)Math.Ceiling(words.Length / (double)Math.Clamp(lines, 1, 3));
        return string.Join("", words.Select((word, i) => (i == 0 ? "" : i % perLine == 0 ? "\\N" : " ") + word));
    }

    // ASS cannot quote arbitrary override braces reliably across renderers. Fullwidth forms
    // retain readable user text while preventing it from becoming an override or a \N command.
    private static string Escape(string text) => text.Replace("\\", "＼").Replace("{", "｛").Replace("}", "｝").Replace("\r", "").Replace("\n", "\\N");
    private static string Color(string color)
    {
        var hex = color.TrimStart('#');
        if (hex.Length == 8) hex = hex[2..];
        if (hex.Length != 6 || !uint.TryParse(hex, NumberStyles.HexNumber, Invariant, out _)) hex = "FFFFFF";
        return $"&H00{hex[4..6]}{hex[2..4]}{hex[0..2]}".ToUpperInvariant();
    }
    private static long Centiseconds(TimeSpan time) => time.Ticks / (TimeSpan.TicksPerMillisecond * 10);
    private static string SrtTime(TimeSpan time) => $"{(long)time.TotalHours:00}:{time.Minutes:00}:{time.Seconds:00},{time.Milliseconds:000}";
    private static string AssTime(TimeSpan time) => $"{(long)time.TotalHours}:{time.Minutes:00}:{time.Seconds:00}.{time.Milliseconds / 10:00}";
}
