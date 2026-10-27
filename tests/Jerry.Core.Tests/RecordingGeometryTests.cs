using Jerry.Core.Recording;
using Jerry.Core.Settings;

namespace Jerry.Core.Tests;

public sealed class RecordingGeometryTests
{
    [Theory]
    [InlineData(3840, 2160, OutputResolution.P1080, 1920, 1080)]
    [InlineData(3440, 1440, OutputResolution.P720, 1280, 720)]
    [InlineData(1919, 1079, OutputResolution.Native, 1918, 1078)]
    public void OutputDimensionsAreExplicitAndH264Compatible(int w, int h, OutputResolution preset, int ew, int eh)
        => Assert.Equal((ew, eh), RecordingGeometry.OutputSize(w, h, preset));

    [Fact]
    public void FitPreservesAspectWhileCropFillsAndStretchUsesExactSize()
    {
        Assert.Equal("scale=1920:1080:force_original_aspect_ratio=decrease,pad=1920:1080:(ow-iw)/2:(oh-ih)/2,setsar=1", RecordingGeometry.BuildFilter(3440, 1440, OutputResolution.P1080, VideoFillMode.Fit));
        Assert.Contains("force_original_aspect_ratio=increase,crop=1920:1080", RecordingGeometry.BuildFilter(3440, 1440, OutputResolution.P1080, VideoFillMode.Crop));
        Assert.Equal("scale=1920:1080,setsar=1", RecordingGeometry.BuildFilter(3440, 1440, OutputResolution.P1080, VideoFillMode.Stretch));
    }

    [Fact]
    public void NegativeMonitorOriginIsRetained()
        => Assert.Equal(-1920, new PixelRect(-1920, 120, 1920, 1080).X);
}
