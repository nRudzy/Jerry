using Jerry.Core.Captions;

namespace Jerry.Core.Tests;

public class CaptionTimingTests
{
    [Fact]
    public void TimingEditScalesExistingWordsInsteadOfDiscardingAlignment()
    {
        var cue = new CaptionCue { Start = TimeSpan.FromSeconds(1), End = TimeSpan.FromSeconds(3), Text = "Un deux", Words = [new("Un", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1.5)), new("deux", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3))] };
        CaptionTiming.UpdateCue(cue, "Un deux", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(14));
        Assert.Equal(new CaptionWord("Un", TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(11)), cue.Words[0]);
        Assert.Equal(new CaptionWord("deux", TimeSpan.FromSeconds(12), TimeSpan.FromSeconds(14)), cue.Words[1]);
    }

    [Fact]
    public void TextEditRedistributesWordsAndKeepsFrenchPunctuation()
    {
        var cue = new CaptionCue { Start = TimeSpan.Zero, End = TimeSpan.FromSeconds(6), Text = "Original", Words = [new("Original", TimeSpan.Zero, TimeSpan.FromSeconds(6))] };
        CaptionTiming.UpdateCue(cue, "L’été, c’est joli !", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(8));
        Assert.Equal(["L’été,", "c’est", "joli", "!"], cue.Words.Select(x => x.Text));
        Assert.Equal(TimeSpan.FromSeconds(2), cue.Words[0].Start);
        Assert.Equal(TimeSpan.FromSeconds(8), cue.Words[^1].End);
        Assert.All(cue.Words, word => Assert.True(word.End > word.Start));
        Assert.True(cue.WordTimingsEstimated);
    }

    [Fact]
    public void InvalidTimingEditLeavesCueUnchanged()
    {
        var cue = new CaptionCue { Start = TimeSpan.Zero, End = TimeSpan.FromSeconds(2), Text = "Avant" };
        Assert.Throws<ArgumentException>(() => CaptionTiming.UpdateCue(cue, "Après", TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1)));
        Assert.Equal("Avant", cue.Text);
    }
    [Fact]
    public void WordEditsUpdateTranscriptAndKeepManualAlignment()
    {
        var cue = new CaptionCue { Start = TimeSpan.FromSeconds(1), End = TimeSpan.FromSeconds(4), Text = "Un mot" };
        CaptionTiming.UpdateWords(cue, [new("Un", TimeSpan.FromSeconds(1.2), TimeSpan.FromSeconds(2)), new("autre!", TimeSpan.FromSeconds(2.5), TimeSpan.FromSeconds(3.8))]);
        Assert.Equal("Un autre!", cue.Text);
        Assert.Equal(TimeSpan.FromSeconds(2.5), cue.Words[1].Start);
        Assert.True(cue.WordTimingsEdited);
        Assert.False(cue.WordTimingsEstimated);
    }

    [Fact]
    public void OverlappingOrOutOfCueWordEditsAreRejectedAtomically()
    {
        var cue = new CaptionCue { Start = TimeSpan.FromSeconds(1), End = TimeSpan.FromSeconds(4), Text = "Avant" };
        Assert.Throws<ArgumentException>(() => CaptionTiming.UpdateWords(cue, [new("A", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3)), new("B", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4))]));
        Assert.Equal("Avant", cue.Text);
        Assert.Empty(cue.Words);
        Assert.Throws<ArgumentException>(() => CaptionTiming.UpdateWords(cue, [new("A", TimeSpan.Zero, TimeSpan.FromSeconds(2))]));
    }
    [Fact]
    public void RegroupRestoresOriginalTextAndWordTimesAfterCorrections()
    {
        var original = new CaptionCue { Start = TimeSpan.Zero, End = TimeSpan.FromSeconds(4), Text = "Un deux trois quatre", Words = [new("Un", TimeSpan.Zero, TimeSpan.FromSeconds(1)), new("deux", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)), new("trois", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3)), new("quatre", TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(4))] };
        var document = new CaptionDocument { OriginalCues = [original] };
        CaptionTiming.RegroupFromOriginal(document, 2);
        Assert.Equal(["Un deux", "trois quatre"], document.Cues.Select(c => c.Text));
        CaptionTiming.UpdateCue(document.Cues[0], "Correction", TimeSpan.Zero, TimeSpan.FromSeconds(2));
        CaptionTiming.RegroupFromOriginal(document, 3);
        Assert.Equal(["Un deux trois", "quatre"], document.Cues.Select(c => c.Text));
        Assert.Equal(TimeSpan.FromSeconds(2), document.Cues[0].Words[2].Start);
        Assert.Equal("Un deux trois quatre", original.Text);
        Assert.False(document.Cues[0].WordTimingsEstimated);
        Assert.False(document.Cues[0].WordTimingsEdited);
    }
}
