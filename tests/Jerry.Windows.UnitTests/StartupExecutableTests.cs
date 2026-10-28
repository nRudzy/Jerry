using Jerry.Windows;

namespace Jerry.Windows.UnitTests;

public sealed class StartupExecutableTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "Jerry chemins é " + Guid.NewGuid().ToString("N"));
    private string Portable => Path.Combine(directory, "Portable", "Jerry.exe");
    private string Installed => Path.Combine(directory, "Installé", "Jerry.exe");

    public StartupExecutableTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Portable)!);
        Directory.CreateDirectory(Path.GetDirectoryName(Installed)!);
        File.WriteAllText(Portable, "portable fixture"); File.WriteAllText(Installed, "installed fixture");
    }

    [Fact]
    public void RegisteredInstalledCopyIsPreferredToPortable()
        => Assert.Equal(Installed, StartupService.ChooseExecutable(Portable, Installed));

    [Fact]
    public void InstalledCopyIsPreferredEvenWhenCurrentCopyIsOnWsl()
        => Assert.Equal(Installed, StartupService.ChooseExecutable(@"\\wsl.localhost\Ubuntu\home\jerry\Jerry.exe", Installed));

    [Theory]
    [InlineData(null)]
    [InlineData(@"C:\missing\Jerry.exe")]
    [InlineData(@"\\server\share\Jerry.exe")]
    [InlineData(@"\\?\UNC\server\share\Jerry.exe")]
    [InlineData(@"C:relative\Jerry.exe")]
    public void MissingOrNonlocalRegistrationFallsBackToLocalPortable(string? registered)
        => Assert.Equal(Portable, StartupService.ChooseExecutable(Portable, registered));

    [Theory]
    [InlineData(@"\\wsl.localhost\Ubuntu\home\user\Jerry.exe")]
    [InlineData(@"\\server\share\Jerry.exe")]
    [InlineData(@"\\?\UNC\server\share\Jerry.exe")]
    [InlineData(@"C:relative\Jerry.exe")]
    [InlineData(@"Jerry.exe")]
    public void StartupRejectsNetworkAndRelativeCopiesWithoutInstallation(string current)
        => Assert.Throws<InvalidOperationException>(() => StartupService.ChooseExecutable(current, null));

    [Fact]
    public void CommandQuotesSpacesAndUnicodeAndStartsInBackground()
        => Assert.Equal("\"C:\\Users\\Prénom Nom\\Programs\\Jerry\\Jerry.exe\" --background",
            StartupService.CreateCommand(@"C:\Users\Prénom Nom\Programs\Jerry\Jerry.exe"));

    [Fact]
    public void StalePortableExecutableIsRejected()
    {
        File.Delete(Portable);
        Assert.Throws<InvalidOperationException>(() => StartupService.ChooseExecutable(Portable, null));
    }

    public void Dispose() => Directory.Delete(directory, true);
}
