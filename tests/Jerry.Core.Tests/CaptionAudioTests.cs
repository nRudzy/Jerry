using Jerry.Core.Captions;
namespace Jerry.Core.Tests;
public class CaptionAudioTests
{
    [Fact]
    public void TaggedMicrophoneWinsOverDefaultSystemAudio()
    {
        var selected = CaptionAudioSelection.Preferred([new(1, "Son système", true), new(2, "Microphone", false)]);
        Assert.Equal(2, selected.Index);
    }
    [Fact]
    public void ForeignImportUsesDeclaredDefaultThenFirstAudioTrack()
    {
        Assert.Equal(3, CaptionAudioSelection.Preferred([new(1, "French", false), new(3, "English", true)]).Index);
        Assert.Equal(1, CaptionAudioSelection.Preferred([new(3, "English", false), new(1, "French", false)]).Index);
        Assert.Throws<InvalidDataException>(() => CaptionAudioSelection.Preferred([]));
    }
}
