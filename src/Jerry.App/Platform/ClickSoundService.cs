using System.Windows.Media;
using Jerry.Core.Settings;

namespace Jerry.App.Platform;

public sealed class ClickSoundService : IDisposable
{
    private readonly MediaPlayer[] players = [new(), new(), new(), new()];
    private readonly string defaultPath;
    private string? current;
    private int index;
    public ClickSoundService(string dataDirectory)
    {
        defaultPath = Path.Combine(dataDirectory, "click.wav");
        if (File.Exists(defaultPath)) return;
        Directory.CreateDirectory(dataDirectory);
        const int samples = 650, rate = 44100;
        using var file = new BinaryWriter(File.Create(defaultPath));
        file.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); file.Write(36 + samples * 2);
        file.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); file.Write(16);
        file.Write((short)1); file.Write((short)1); file.Write(rate); file.Write(rate * 2); file.Write((short)2); file.Write((short)16);
        file.Write(System.Text.Encoding.ASCII.GetBytes("data")); file.Write(samples * 2);
        for (var i = 0; i < samples; i++)
        {
            var t = (double)i / samples;
            var signal = Math.Sin(i * 2 * Math.PI * 1500 / rate) * Math.Exp(-t * 10) * Math.Sin(Math.PI * t);
            file.Write((short)(signal * 18000));
        }
    }
    public void Play(SoundOptions settings)
    {
        var path = string.IsNullOrWhiteSpace(settings.FilePath) ? defaultPath : settings.FilePath;
        if (!File.Exists(path)) throw new FileNotFoundException("Le son de clic choisi est introuvable.", path);
        if (current != path)
        {
            current = path;
            foreach (var item in players) item.Open(new Uri(Path.GetFullPath(path)));
        }
        var player = players[index++ % players.Length];
        player.Volume = settings.Volume; player.Position = TimeSpan.Zero; player.Play();
    }
    public void Dispose() { foreach (var player in players) player.Close(); }
}
