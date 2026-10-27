using System.Text.RegularExpressions;
namespace Jerry.Core.Captions;

public record CaptionAudioTrack(int Index, string Name, bool IsDefault);

public static class CaptionAudioSelection
{
    public static CaptionAudioTrack Preferred(IEnumerable<CaptionAudioTrack> tracks)
    {
        var ordered = tracks.OrderBy(track => track.Index).ToArray();
        return ordered.FirstOrDefault(track => Regex.IsMatch(track.Name, @"\b(microphone|micro|mic|voice|voix|narration)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            ?? ordered.FirstOrDefault(track => track.IsDefault)
            ?? ordered.FirstOrDefault()
            ?? throw new InvalidDataException("Le média ne contient aucune piste audio. Importez un fichier avec de la parole enregistrée.");
    }
}
