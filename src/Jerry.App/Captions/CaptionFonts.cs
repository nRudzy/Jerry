using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Media;

namespace Jerry.App.Captions;

internal static class CaptionFonts
{
    private static readonly ConcurrentDictionary<string, FontFamily> Families = new(StringComparer.OrdinalIgnoreCase);
    public static string DirectoryPath => Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts");

    public static IEnumerable<string> Choices() => new[] { "Montserrat", "Oswald" }
        .Concat(Fonts.SystemFontFamilies.Where(family => family.GetTypefaces().Any(face => face.Weight.ToOpenTypeWeight() >= 700))
            .Select(family => family.Source).OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase))
        .Distinct(StringComparer.OrdinalIgnoreCase);

    public static FontWeight Weight(string family) => IsMontserrat(family) ? FontWeights.ExtraBold : FontWeights.Bold;

    public static FontFamily ResolveFamily(string family) => Families.GetOrAdd(family, name =>
    {
        var bundled = IsMontserrat(name) ? "Montserrat" : IsOswald(name) ? "Oswald" : null;
        if (bundled == null) return new FontFamily(name);
        EnsureAssets();
        // A file-based family from this directory retains its URI. It therefore
        // resolves these supplied faces without a machine-wide font installation.
        return Fonts.GetFontFamilies(DirectoryPath + Path.DirectorySeparatorChar).FirstOrDefault(font => font.FamilyNames.Values.Any(value =>
            value.Equals(bundled, StringComparison.OrdinalIgnoreCase) || value.Equals(bundled + " ExtraBold", StringComparison.OrdinalIgnoreCase)))
            ?? throw new InvalidDataException($"Police intégrée {bundled} illisible dans {DirectoryPath}.");
    });

    public static void EnsureAssets()
    {
        foreach (var name in new[] { "Montserrat-ExtraBold.ttf", "Oswald-Bold.ttf" })
            if (!File.Exists(Path.Combine(DirectoryPath, name)))
                throw new FileNotFoundException("Les polices intégrées sont absentes de l’application. Réinstallez Jerry avec son dossier Assets/Fonts.", Path.Combine(DirectoryPath, name));
    }

    private static bool IsMontserrat(string name) => name.Equals("Montserrat", StringComparison.OrdinalIgnoreCase) || name.Equals("Montserrat ExtraBold", StringComparison.OrdinalIgnoreCase);
    private static bool IsOswald(string name) => name.Equals("Oswald", StringComparison.OrdinalIgnoreCase) || name.Equals("Oswald Bold", StringComparison.OrdinalIgnoreCase);
}
