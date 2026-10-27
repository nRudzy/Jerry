using Jerry.Core.Recording;
using Jerry.Core.Settings;

namespace Jerry.Core.Tests;

public sealed class RecordingMediaTests
{
    [Fact]
    public void CapturePreservesNegativeDesktopCoordinatesAndPassesPathsAsOneArgument()
    {
        var args = RecordingMedia.CaptureArguments(new(-1920, -200, 1920, 1080), new() { FramesPerSecond = 60 }, @"C:\My Videos\video.mkv").ToList();
        Assert.Equal("-1920", args[args.IndexOf("-offset_x") + 1]);
        Assert.Equal("-200", args[args.IndexOf("-offset_y") + 1]);
        Assert.Equal("60", args[args.IndexOf("-framerate") + 1]);
        Assert.Equal(@"C:\My Videos\video.mkv", args[^1]);
        Assert.Equal("gdigrab", args[args.IndexOf("-f") + 1]);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(-20, 100)]
    public void InvalidCaptureAreaFailsBeforeLaunchingAnyChild(int width, int height)
        => Assert.Throws<ArgumentOutOfRangeException>(() => RecordingMedia.CaptureArguments(new(0, 0, width, height), new(), "out.mkv"));

    [Fact]
    public void MuxKeepsSystemAndMicrophoneAsTwoIndependentlyNamedTracks()
    {
        var args = RecordingMedia.MuxArguments("screen.mkv", [("system.wav", "Son système"), ("mic.wav", "Microphone")], "out.mp4").ToList();
        Assert.Contains("0:v:0", args);
        Assert.Contains("1:a:0", args);
        Assert.Contains("2:a:0", args);
        Assert.Contains("title=Son système", args);
        Assert.Contains("title=Microphone", args);
        Assert.Contains("handler_name=Son système", args);
        Assert.Contains("handler_name=Microphone", args);
        Assert.Contains("copy", args);
        Assert.DoesNotContain("-shortest", args);
    }

    [Fact]
    public void SilentLoopbackPeriodsRetainTheirPlaceOnTheVideoTimeline()
    {
        Assert.Equal(432000, RecordingMedia.SilenceFrames(48000, 484800, 4800, 4800));
        Assert.Equal(0, RecordingMedia.SilenceFrames(48000, 52900, 4800, 4800));
        Assert.Equal(480000, RecordingMedia.SilenceFrames(0, 480000, 0, 0));
    }
    [Fact]
    public void CameraMappingUsesPhysicalDeviceIdentityWhenFriendlyNamesAreDuplicated()
    {
        var log = "[dshow] \"Camera\" (video)\n[dshow] Alternative name \"@device_pnp_usb#one#{video}\"\n[dshow] \"Camera\" (video)\n[dshow] Alternative name \"@device_pnp_usb#two#{video}\"";
        Assert.Equal("@device_pnp_usb#two#{video}", RecordingMedia.DirectShowCamera(log, "usb#TWO#{winrt}", "Camera"));
        Assert.Throws<InvalidOperationException>(() => RecordingMedia.DirectShowCamera(log, "missing", "Camera"));
    }
    [Fact]
    public void MicrophoneIsPreferredForPlayersThatOnlyPlayTheDefaultAudioTrack()
    {
        var args = RecordingMedia.MuxArguments("screen.mkv", [("system.wav", "Son système"), ("microphone.wav", "Microphone")], "out.mp4").ToList();
        Assert.Contains("-disposition:a:0", args);
        Assert.Equal("0", args[args.IndexOf("-disposition:a:0") + 1]);
        Assert.Contains("-disposition:a:1", args);
        Assert.Equal("default", args[args.IndexOf("-disposition:a:1") + 1]);
        var systemOnly = RecordingMedia.MuxArguments("screen.mkv", [("system.wav", "Son système")], "out.mp4").ToList();
        Assert.Equal("default", systemOnly[systemOnly.IndexOf("-disposition:a:0") + 1]);
    }
}
