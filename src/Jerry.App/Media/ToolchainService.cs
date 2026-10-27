using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace Jerry.App.Media;

public sealed class ToolchainService
{
    private const string ArchiveUrl = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
    private static readonly SemaphoreSlim DownloadLock = new(1, 1);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(15) };
    public string ToolsDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Jerry", "tools");
    public string? FindFfmpeg() => Find("ffmpeg.exe");
    private string? Find(string name)
    {
        var path = Path.Combine(ToolsDirectory, "ffmpeg", name);
        return File.Exists(path) ? path : null;
    }

    public async Task<string> EnsureFfmpegAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        await DownloadLock.WaitAsync(cancellationToken);
        try
        {
            if (FindFfmpeg() is { } installed && Find("ffprobe.exe") != null) return installed;
            Directory.CreateDirectory(ToolsDirectory);
            var staging = Path.Combine(ToolsDirectory, "ffmpeg-download-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            try
            {
                var zipPath = Path.Combine(staging, "distribution.zip");
                var checksumText = await Http.GetStringAsync(ArchiveUrl + ".sha256", cancellationToken);
                var expectedHash = checksumText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0];
                if (expectedHash.Length != 64 || !expectedHash.All(Uri.IsHexDigit)) throw new InvalidDataException("Empreinte FFmpeg invalide.");
                using (var response = await Http.GetAsync(ArchiveUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
                    await using var output = File.Create(zipPath);
                    var buffer = new byte[81920];
                    long readTotal = 0;
                    int read;
                    while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                        readTotal += read;
                        if (response.Content.Headers.ContentLength is > 0) progress?.Report((double)readTotal / response.Content.Headers.ContentLength.Value);
                    }
                }
                await using (var stream = File.OpenRead(zipPath))
                {
                    var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
                    if (!actual.Equals(expectedHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Le téléchargement FFmpeg ne correspond pas à son empreinte SHA-256.");
                }
                var extracted = Path.Combine(staging, "bin");
                Directory.CreateDirectory(extracted);
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (var name in new[] { "ffmpeg.exe", "ffprobe.exe" })
                    {
                        var entry = archive.Entries.Single(e => e.FullName.EndsWith("/bin/" + name, StringComparison.OrdinalIgnoreCase));
                        entry.ExtractToFile(Path.Combine(extracted, name));
                    }
                    foreach (var entry in archive.Entries.Where(e => e.Name.Equals("LICENSE", StringComparison.OrdinalIgnoreCase) || e.Name.StartsWith("LICENSE.", StringComparison.OrdinalIgnoreCase)))
                        entry.ExtractToFile(Path.Combine(extracted, entry.Name), true);
                }
                await File.WriteAllTextAsync(Path.Combine(extracted, "provenance.json"), JsonSerializer.Serialize(new { source = ArchiveUrl, archiveSha256 = expectedHash, installedUtc = DateTimeOffset.UtcNow }), cancellationToken);
                var destination = Path.Combine(ToolsDirectory, "ffmpeg");
                if (Directory.Exists(destination)) Directory.Delete(destination, true);
                Directory.Move(extracted, destination);
                progress?.Report(1);
                return FindFfmpeg()!;
            }
            finally { try { Directory.Delete(staging, true); } catch (IOException) { } }
        }
        finally { DownloadLock.Release(); }
    }

    public async Task<string> EnsureFfprobeAsync(CancellationToken cancellationToken = default)
    {
        await EnsureFfmpegAsync(cancellationToken: cancellationToken);
        return Find("ffprobe.exe")!;
    }
}

internal static class MediaProcess
{
    public static Process Start(string executable, IEnumerable<string> arguments, bool interactive = false)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true, RedirectStandardInput = interactive };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        return Process.Start(info) ?? throw new IOException("Impossible de démarrer l’outil média.");
    }

    public static async Task<int> RunAsync(string executable, IEnumerable<string> arguments, string logPath, CancellationToken cancellationToken)
    {
        using var process = Start(executable, arguments);
        var errors = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEndAsync();
        try { await process.WaitForExitAsync(cancellationToken); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); throw; }
        finally { await File.WriteAllTextAsync(logPath, await errors + await output); }
        return process.ExitCode;
    }
}
