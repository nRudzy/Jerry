using Jerry.Core.Captions;
using Jerry.Core.Settings;

namespace Jerry.Core.Tests;

public class CaptionExporterTests
{
    private static CaptionDocument Sample() => new()
    {
        Cues = [new() { Start = TimeSpan.FromMilliseconds(3_599_999), End = TimeSpan.FromMilliseconds(3_601_234), Text = "Oui <b> & non\nEncore", Words = [] }]
    };

    [Fact]
    public void SrtKeepsTotalHoursMillisecondsAndEscapesMarkup()
    {
        Assert.Equal("1\r\n00:59:59,999 --> 01:00:01,234\r\nOui &lt;b&gt; &amp; non\r\nEncore\r\n\r\n", CaptionExporter.ToSrt(Sample()));
    }

    [Fact]
    public void SrtSortsCuesAndRejectsInvalidRanges()
    {
        var document = new CaptionDocument { Cues = [new() { Start = TimeSpan.FromSeconds(2), End = TimeSpan.FromSeconds(3), Text = "B" }, new() { Start = TimeSpan.Zero, End = TimeSpan.FromSeconds(1), Text = "A\n\nB" }] };
        Assert.StartsWith("1\r\n00:00:00,000 --> 00:00:01,000\r\nA\r\nB\r\n\r\n2", CaptionExporter.ToSrt(document));
        document.Cues[0].End = TimeSpan.FromSeconds(1);
        Assert.Throws<ArgumentException>(() => CaptionExporter.ToSrt(document));
    }

    [Theory]
    [InlineData(CaptionStyle.CleanBar, ",3,8,0,5,")]
    [InlineData(CaptionStyle.BoldOutline, ",1,3,1,5,")]
    [InlineData(CaptionStyle.Minimal, ",1,0,0,5,")]
    [InlineData(CaptionStyle.WordFill, "{\\kf100}")]
    [InlineData(CaptionStyle.WordPop, "\\fscx115\\fscy115")]
    [InlineData(CaptionStyle.SlideUp, "\\move(960,958,960,918,0,220)")]
    public void AssRendersEachStyleAndConfiguredLayout(CaptionStyle style, string expected)
    {
        var document = new CaptionDocument { Cues = [new() { Start = TimeSpan.Zero, End = TimeSpan.FromSeconds(2), Text = "Un deux", Words = [new("Un", TimeSpan.Zero, TimeSpan.FromSeconds(1)), new("deux", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2))] }] };
        var options = new CaptionOptions { Style = style, X = .5, Y = .85, Background = true, Color = "#FFFFFF", AccentColor = "#7DE5C3", Lines = 1 };
        var ass = CaptionExporter.ToAss(document, options, 1920, 1080);
        Assert.Contains("PlayResX: 1920", ass);
        Assert.Contains("PlayResY: 1080", ass);
        Assert.Contains("&H00C3E57D", ass);
        Assert.Contains(expected, ass);
        if (style == CaptionStyle.WordPop)
        {
            Assert.Contains("0:00:00.00,0:00:01.00", ass);
            Assert.Contains("0:00:01.00,0:00:02.00", ass);
        }
        else Assert.Contains("0:00:00.00,0:00:02.00", ass);
    }

    [Fact]
    public void AssEscapesOverridesAndLineBreaksInUserText()
    {
        var document = new CaptionDocument { Cues = [new() { Start = TimeSpan.Zero, End = TimeSpan.FromSeconds(1), Text = "{\\pos(0,0)}\nTexte" }] };
        var ass = CaptionExporter.ToAss(document, new() { Style = CaptionStyle.Minimal }, 640, 360);
        Assert.Contains("｛＼pos(0,0)｝\\NTexte", ass);
        Assert.DoesNotContain("{\\pos(0,0)}", ass);
    }

    [Fact]
    public void WordFillAccountsForSilenceBeforeAndBetweenWords()
    {
        var document = new CaptionDocument { Cues = [new() { Start = TimeSpan.FromSeconds(1), End = TimeSpan.FromSeconds(4), Text = "Un deux", Words = [new("Un", TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(2)), new("deux", TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(4))] }] };
        Assert.Contains("{\\k50}{\\kf50}Un {\\k100}{\\kf100}deux", CaptionExporter.ToAss(document, new() { Style = CaptionStyle.WordFill, Lines = 1 }, 640, 360));
    }
    [Fact]
    public void SlideUpStaggersEachWordAtItsSpeechTime()
    {
        var document = new CaptionDocument { Cues = [new() { Start = TimeSpan.Zero, End = TimeSpan.FromSeconds(2), Text = "Un deux", Words = [new("Un", TimeSpan.Zero, TimeSpan.FromSeconds(1)), new("deux", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2))] }] };
        var ass = CaptionExporter.ToAss(document, new() { Style = CaptionStyle.SlideUp, Lines = 1 }, 640, 360);
        var events = ass.Split("\r\n").Where(line => line.StartsWith("Dialogue:")).ToArray();
        Assert.Equal(2, events.Length);
        Assert.Contains("0:00:00.00,0:00:02.00", events[0]);
        Assert.Contains("0:00:01.00,0:00:02.00", events[1]);
        Assert.Contains("{\\alpha&H00&}Un{\\alpha&HFF&}", events[0]);
        Assert.Contains("{\\alpha&H00&}deux{\\alpha&HFF&}", events[1]);
        Assert.All(events, line => Assert.Contains("\\move(320,346,320,306,0,220)", line));
    }
}
