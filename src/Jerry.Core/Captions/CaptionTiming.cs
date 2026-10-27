using System.Text.RegularExpressions;

namespace Jerry.Core.Captions;

public static class CaptionTiming
{
    public static void RegroupFromOriginal(CaptionDocument document, int wordsPerCaption)
    {
        if (wordsPerCaption < 1 || wordsPerCaption > 30) throw new ArgumentException("Choisissez entre 1 et 30 mots par sous-titre.");
        if (document.OriginalCues.Count == 0)
            document.OriginalCues = document.Cues.Select(Clone).ToList();
        var result = new List<CaptionCue>();
        foreach (var original in document.OriginalCues.OrderBy(cue => cue.Start))
        {
            var words = GetWords(original);
            for (var i = 0; i < words.Count; i += wordsPerCaption)
            {
                var group = words.Skip(i).Take(wordsPerCaption).ToList();
                result.Add(new CaptionCue
                {
                    Start = i == 0 ? original.Start : group[0].Start,
                    End = i + wordsPerCaption >= words.Count ? original.End : group[^1].End,
                    Text = string.Join(" ", group.Select(word => word.Text)),
                    Words = group, WordTimingsEstimated = original.WordTimingsEstimated,
                    WordTimingsEdited = original.WordTimingsEdited
                });
            }
        }
        document.Cues = result;
    }

    private static CaptionCue Clone(CaptionCue cue) => new()
    {
        Start = cue.Start, End = cue.End, Text = cue.Text, Words = cue.Words.ToList(),
        WordTimingsEstimated = cue.WordTimingsEstimated, WordTimingsEdited = cue.WordTimingsEdited
    };

    public static void UpdateWords(CaptionCue cue, IEnumerable<CaptionWord> words)
    {
        var replacement = words.ToList();
        if (replacement.Count == 0) throw new ArgumentException("Ajoutez au moins un mot au sous-titre.");
        var previousEnd = cue.Start;
        foreach (var word in replacement)
        {
            if (string.IsNullOrWhiteSpace(word.Text) || word.Text.Any(char.IsWhiteSpace))
                throw new ArgumentException("Chaque cellule doit contenir un seul mot ; modifiez le texte du sous-titre pour ajouter ou supprimer des mots.");
            ValidateRange(word.Start, word.End);
            if (word.Start < cue.Start || word.End > cue.End || word.Start < previousEnd)
                throw new ArgumentException("Les mots doivent se suivre sans chevauchement et rester dans l’intervalle du sous-titre.");
            previousEnd = word.End;
        }
        cue.Words = replacement;
        cue.Text = string.Join(" ", replacement.Select(word => word.Text));
        cue.WordTimingsEstimated = false;
        cue.WordTimingsEdited = true;
    }

    public static void ValidateRange(TimeSpan start, TimeSpan end)
    {
        if (start < TimeSpan.Zero || end <= start)
            throw new ArgumentException("Le début doit être positif et la fin doit suivre le début.");
    }

    public static void UpdateCue(CaptionCue cue, string text, TimeSpan start, TimeSpan end)
    {
        ValidateRange(start, end);
        text = text.Trim();
        if (text != cue.Text || start != cue.Start || end != cue.End) cue.WordTimingsEdited = true;
        if (text == cue.Text.Trim() && HasMatchingWords(cue) && cue.End > cue.Start)
        {
            var ratio = (end - start).Ticks / (double)(cue.End - cue.Start).Ticks;
            var origin = cue.Start;
            cue.Words = cue.Words.Select(w => new CaptionWord(w.Text,
                start + TimeSpan.FromTicks((long)((w.Start - origin).Ticks * ratio)),
                start + TimeSpan.FromTicks((long)((w.End - origin).Ticks * ratio)))).ToList();
        }
        else
        {
            cue.Words = EstimateWords(text, start, end);
            cue.WordTimingsEstimated = true;
        }
        cue.Start = start;
        cue.End = end;
        cue.Text = text;
    }

    public static bool HasMatchingWords(CaptionCue cue) => cue.Words.Count > 0 &&
        string.Join(" ", SplitWords(cue.Text)) == string.Join(" ", cue.Words.Select(w => w.Text));

    public static string[] SplitWords(string text) => Regex.Matches(text, @"\S+").Select(x => x.Value).ToArray();

    public static List<CaptionWord> GetWords(CaptionCue cue) => HasMatchingWords(cue)
        ? cue.Words : EstimateWords(cue.Text, cue.Start, cue.End);

    public static List<CaptionWord> EstimateWords(string text, TimeSpan start, TimeSpan end)
    {
        ValidateRange(start, end);
        var words = SplitWords(text);
        var total = words.Sum(w => w.Length);
        var offset = 0;
        var result = new List<CaptionWord>();
        foreach (var word in words)
        {
            var wordStart = start + TimeSpan.FromTicks((long)((end - start).Ticks * (offset / (double)total)));
            offset += word.Length;
            var wordEnd = offset == total ? end : start + TimeSpan.FromTicks((long)((end - start).Ticks * (offset / (double)total)));
            result.Add(new(word, wordStart, wordEnd));
        }
        return result;
    }
}
