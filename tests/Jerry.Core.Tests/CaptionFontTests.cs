using Jerry.Core.Captions;
using Jerry.Core.Settings;
namespace Jerry.Core.Tests;

public class CaptionFontTests
{
    [Theory]
    [InlineData("Montserrat", "Montserrat ExtraBold")]
    [InlineData("Oswald", "Oswald Bold")]
    public void AssUsesTheBundledHeavyFaceName(string selected, string expected)
    {
        var ass = CaptionExporter.ToAss(new(), new CaptionOptions { FontFamily = selected }, 640, 360);
        Assert.Contains("Style: Default," + expected + ",44,", ass);
    }

    [Fact]
    public void AssFontDirectoryEscapesBothFilterParsingLevelsWithoutShellQuoting()
    {
        Assert.Equal("""ass=filename=captions.ass:fontsdir=C\\:/Jerry\\\'s Fonts/Assets/Fonts""", CaptionFfmpegFilter.WithFonts("captions.ass", @"C:\Jerry's Fonts\Assets\Fonts"));
    }
}
