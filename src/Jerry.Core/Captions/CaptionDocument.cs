namespace Jerry.Core.Captions;

public record CaptionWord(string Text, TimeSpan Start, TimeSpan End);

public sealed class CaptionCue
{
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
    public string Text { get; set; } = "";
    public List<CaptionWord> Words { get; set; } = [];
    public bool WordTimingsEstimated { get; set; }
    public bool WordTimingsEdited { get; set; }
}

public sealed class CaptionDocument
{
    public string MediaPath { get; set; } = "";
    public List<CaptionCue> Cues { get; set; } = [];
    public List<CaptionCue> OriginalCues { get; set; } = [];
}
