using System.Text;

namespace Jerry.Core.Captions;

public static class CaptionFfmpegFilter
{
    public static string WithFonts(string subtitleFile, string fontsDirectory) =>
        "ass=filename=" + Escape(subtitleFile) + ":fontsdir=" + Escape(fontsDirectory);

    private static string Escape(string path)
    {
        // ArgumentList already handles the process command line. Escape the option
        // value, then the filter graph; there is no third shell quoting layer.
        var option = new StringBuilder();
        foreach (var character in path.Replace('\\', '/'))
        {
            if (character is '\\' or '\'' or ':') option.Append('\\');
            option.Append(character);
        }
        var graph = new StringBuilder();
        foreach (var character in option.ToString())
        {
            if (character is '\\' or '\'' or '[' or ']' or ',' or ';') graph.Append('\\');
            graph.Append(character);
        }
        return graph.ToString();
    }
}
