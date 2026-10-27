using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using Jerry.App.Media;
using Jerry.Core.Captions;
using Jerry.Core.Settings;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Whisper.net;

namespace Jerry.App.Captions;

public sealed class CaptionService(AppSettings settings, ToolchainService tools) : IAsyncDisposable
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(30) };
    private static readonly SemaphoreSlim ModelGate = new(1, 1);
    private readonly SemaphoreSlim _engineGate = new(1, 1);
    private readonly SemaphoreSlim _liveGate = new(1, 1);
    private WhisperFactory? _factory;
    private string? _loadedModel;
    private CancellationTokenSource? _liveCancellation;
    private CancellationTokenRegistration _liveRegistration;
    private WasapiCapture? _capture;
    private MMDevice? _liveDevice;
    private Task? _liveTask;
    public bool IsLive => _capture != null;
    public event Action<string>? LiveText;
    public event Action<string>? StatusChanged;
    public event Action<Exception>? LiveError;
    public string ModelsDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Jerry", "models");
    private string WorkRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Jerry", "work", "captions");

    public async Task EnsureModelAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var (name, hash) = ModelInfo();
        await ModelGate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(ModelsDirectory);
            var destination = Path.Combine(ModelsDirectory, $"ggml-{name}.bin");
            if (File.Exists(destination))
            {
                StatusChanged?.Invoke($"Vérification du modèle local {name}…");
                if (await VerifyModelAsync(destination, hash, cancellationToken)) { progress?.Report(1); return; }
                throw new InvalidDataException($"Le modèle local est incomplet ou altéré. Supprimez « {destination} » puis relancez le téléchargement.");
            }
            StatusChanged?.Invoke($"Téléchargement local du modèle {name} ({(name == "tiny" ? "75" : "142")} Mio)…");
            var source = $"https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-{name}.bin";
            var partial = destination + "." + Guid.NewGuid().ToString("N") + ".partial";
            try
            {
                using (var response = await Http.GetAsync(source, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
                    await using var output = File.Create(partial);
                    var buffer = new byte[81920];
                    long total = 0;
                    int read;
                    while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                        total += read;
                        if (response.Content.Headers.ContentLength is > 0)
                            progress?.Report((double)total / response.Content.Headers.ContentLength.Value);
                    }
                }
                if (!await VerifyModelAsync(partial, hash, cancellationToken))
                    throw new InvalidDataException("Le modèle téléchargé ne correspond pas à l’empreinte publiée par whisper.cpp. Réessayez le téléchargement.");
                File.Move(partial, destination);
                await using var verified = File.OpenRead(destination);
                var sha256 = Convert.ToHexString(await SHA256.HashDataAsync(verified, cancellationToken));
                await File.WriteAllTextAsync(destination + ".provenance.json", JsonSerializer.Serialize(new
                {
                    source, model = name, sha1 = hash, sha256, downloadedUtc = DateTimeOffset.UtcNow,
                    distribution = "https://github.com/ggml-org/whisper.cpp/blob/master/models/README.md",
                    whisperNet = "1.9.0", runtime = "Whisper.net.Runtime 1.9.0 (CPU x64)"
                }, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
                progress?.Report(1);
                StatusChanged?.Invoke($"Modèle {name} disponible hors ligne.");
            }
            finally { if (File.Exists(partial)) File.Delete(partial); }
        }
        finally { ModelGate.Release(); }
    }

    public async Task<CaptionDocument> TranscribeAsync(string mediaPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        mediaPath = Path.GetFullPath(mediaPath);
        if (!File.Exists(mediaPath)) throw new FileNotFoundException("Choisissez un fichier vidéo ou audio existant.", mediaPath);
        await EnsureModelAsync(new Progress<double>(p => progress?.Report(p * .1)), cancellationToken);
        StatusChanged?.Invoke("Préparation de l’audio local à 16 kHz…");
        var ffmpeg = await tools.EnsureFfmpegAsync(cancellationToken: cancellationToken);
        var work = NewWorkDirectory();
        var wave = Path.Combine(work, "audio.wav");
        try
        {
            var audio = await SelectAudioTrackAsync(ffmpeg, mediaPath, work, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(work, "audio-selection.json"), JsonSerializer.Serialize(audio), cancellationToken);
            StatusChanged?.Invoke($"Piste transcrite : {audio.Name} (index {audio.Index}).");
            await RunAsync(ffmpeg, ["-hide_banner", "-loglevel", "error", "-nostdin", "-y", "-i", mediaPath, "-map", $"0:{audio.Index}", "-vn", "-ac", "1", "-ar", "16000", "-c:a", "pcm_s16le", wave], work, cancellationToken);
            progress?.Report(.2);
            await _engineGate.WaitAsync(cancellationToken);
            try
            {
                var factory = await GetFactoryAsync(cancellationToken);
                using var processor = BuildProcessor(factory, p => progress?.Report(.2 + p / 100d * .8));
                var document = new CaptionDocument { MediaPath = mediaPath };
                StatusChanged?.Invoke("Transcription Whisper sur cet ordinateur…");
                await using (var input = File.OpenRead(wave))
                {
                    await foreach (var segment in processor.ProcessAsync(input, cancellationToken))
                    {
                        var cue = CueFromSegment(segment);
                        if (cue != null) document.OriginalCues.Add(cue);
                    }
                }
                CaptionTiming.RegroupFromOriginal(document, Math.Clamp(settings.Captions.WordsPerCaption, 1, 30));
                if (document.Cues.Count == 0) throw new InvalidDataException("Whisper n’a reconnu aucune parole. Vérifiez la piste audio et la langue choisie.");
                await File.WriteAllTextAsync(Path.Combine(work, "transcript.jerrycaptions.json"), JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
                File.Delete(wave);
                progress?.Report(1);
                StatusChanged?.Invoke($"Transcription terminée : {document.Cues.Count} sous-titres. Copie récupérable : {work}");
                return document;
            }
            finally { _engineGate.Release(); }
        }
        catch (OperationCanceledException) { StatusChanged?.Invoke($"Transcription annulée. Fichiers intermédiaires : {work}"); throw; }
        catch (Exception error) { throw new InvalidOperationException($"Transcription impossible. {error.Message}\nFichiers et journal : {work}", error); }
    }

    public async Task StartLiveAsync(CancellationToken cancellationToken = default)
    {
        await _liveGate.WaitAsync(cancellationToken);
        try
        {
            if (_capture != null) return;
            await EnsureModelAsync(cancellationToken: cancellationToken);
            // Load before opening the microphone so model failures are reported by StartLiveAsync.
            await _engineGate.WaitAsync(cancellationToken);
            try { await GetFactoryAsync(cancellationToken); }
            finally { _engineGate.Release(); }
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var devices = new MMDeviceEnumerator();
            MMDevice device;
            try
            {
                device = string.IsNullOrWhiteSpace(settings.Recording.MicrophoneDeviceId)
                    ? devices.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia)
                    : devices.GetDevice(settings.Recording.MicrophoneDeviceId);
            }
            catch (Exception error) { cancellation.Dispose(); throw new InvalidOperationException("Aucun microphone sélectionné n’est disponible. Branchez un micro et choisissez-le dans les réglages audio Windows/Jerry.", error); }
            var capture = new WasapiCapture(device, true, 100);
            var queue = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(2) { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.DropOldest });
            var chunkBytes = capture.WaveFormat.AverageBytesPerSecond * 4;
            var chunk = new byte[chunkBytes];
            var filled = 0;
            var bufferLock = new object();
            capture.DataAvailable += (_, data) =>
            {
                lock (bufferLock)
                {
                    var offset = 0;
                    while (offset < data.BytesRecorded)
                    {
                        var count = Math.Min(chunk.Length - filled, data.BytesRecorded - offset);
                        Buffer.BlockCopy(data.Buffer, offset, chunk, filled, count);
                        filled += count;
                        offset += count;
                        if (filled == chunk.Length) { queue.Writer.TryWrite(chunk); chunk = new byte[chunkBytes]; filled = 0; }
                    }
                }
            };
            capture.RecordingStopped += (_, stopped) =>
            {
                queue.Writer.TryComplete(stopped.Exception);
                if (stopped.Exception != null) LiveError?.Invoke(new InvalidOperationException("Le microphone a cessé de fournir de l’audio. Vérifiez sa connexion et les permissions Windows.", stopped.Exception));
            };
            _liveDevice = device;
            _liveCancellation = cancellation;
            _capture = capture;
            var waveFormat = capture.WaveFormat;
            _liveTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var bytes in queue.Reader.ReadAllAsync(cancellation.Token))
                    {
                        var samples = Resample(bytes, waveFormat);
                        if (samples.Length == 0 || samples.Average(value => value * value) < .000004) continue;
                        await _engineGate.WaitAsync(cancellation.Token);
                        try
                        {
                            var factory = await GetFactoryAsync(cancellation.Token);
                            using var processor = BuildProcessor(factory);
                            await foreach (var segment in processor.ProcessAsync(samples, cancellation.Token))
                                if (!string.IsNullOrWhiteSpace(segment.Text)) LiveText?.Invoke(segment.Text.Trim());
                        }
                        finally { _engineGate.Release(); }
                    }
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                catch (Exception error) { LiveError?.Invoke(error); _ = Task.Run(StopLiveAsync); }
            }, CancellationToken.None);
            try { capture.StartRecording(); }
            catch
            {
                cancellation.Cancel(); queue.Writer.TryComplete();
                await _liveTask;
                capture.Dispose(); device.Dispose(); cancellation.Dispose();
                _capture = null; _liveDevice = null; _liveCancellation = null; _liveTask = null;
                throw;
            }
            _liveRegistration = cancellationToken.Register(() => { _ = StopLiveAsync(); });
            StatusChanged?.Invoke("Sous-titres micro actifs · blocs de 4 s · traitement local. Les blocs les plus anciens sont abandonnés si le processeur ne suit pas.");
        }
        finally { _liveGate.Release(); }
    }

    public async Task StopLiveAsync()
    {
        await _liveGate.WaitAsync();
        try
        {
            _liveRegistration.Unregister();
            _liveCancellation?.Cancel();
            try
            {
                _capture?.StopRecording();
                if (_liveTask != null) await _liveTask;
            }
            finally
            {
                _capture?.Dispose();
                _liveDevice?.Dispose();
                _liveCancellation?.Dispose();
                _capture = null; _liveTask = null; _liveDevice = null; _liveCancellation = null;
            }
            LiveText?.Invoke("");
            StatusChanged?.Invoke("Sous-titres micro arrêtés.");
        }
        finally { _liveGate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await StopLiveAsync();
        await _engineGate.WaitAsync();
        try { _factory?.Dispose(); _factory = null; _loadedModel = null; }
        finally { _engineGate.Release(); }
    }

    public async Task ExportVideoAsync(CaptionDocument document, string outputPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        var source = Path.GetFullPath(document.MediaPath);
        var destination = Path.GetFullPath(outputPath);
        if (source.Equals(destination, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Choisissez un nouveau fichier pour conserver la vidéo originale.");
        var ffmpeg = await tools.EnsureFfmpegAsync(cancellationToken: cancellationToken);
        var probe = Path.Combine(Path.GetDirectoryName(ffmpeg)!, "ffprobe.exe");
        var work = NewWorkDirectory();
        var json = await RunAsync(probe, ["-v", "error", "-select_streams", "v:0", "-show_entries", "stream=width,height:stream_tags=rotate:stream_side_data=rotation:format=duration", "-of", "json", source], work, cancellationToken);
        using var metadata = JsonDocument.Parse(json);
        var streams = metadata.RootElement.GetProperty("streams");
        if (streams.GetArrayLength() == 0) throw new InvalidDataException("Le fichier importé ne contient pas de piste vidéo à exporter.");
        var video = streams[0];
        var width = video.GetProperty("width").GetInt32();
        var height = video.GetProperty("height").GetInt32();
        double rotation = 0;
        if (video.TryGetProperty("side_data_list", out var sides))
            foreach (var side in sides.EnumerateArray())
                if (side.TryGetProperty("rotation", out var value)) rotation = value.GetDouble();
        if (video.TryGetProperty("tags", out var tags) && tags.TryGetProperty("rotate", out var rotate) && double.TryParse(rotate.GetString(), CultureInfo.InvariantCulture, out var tagRotation)) rotation = tagRotation;
        if (Math.Abs(rotation % 180) > 45 && Math.Abs(rotation % 180) < 135) (width, height) = (height, width);
        var duration = document.Cues.Count > 0 ? document.Cues.Max(c => c.End.TotalSeconds) : 1;
        if (metadata.RootElement.TryGetProperty("format", out var format) && format.TryGetProperty("duration", out var seconds) && double.TryParse(seconds.GetString(), CultureInfo.InvariantCulture, out var actualDuration)) duration = actualDuration;
        await File.WriteAllTextAsync(Path.Combine(work, "captions.ass"), CaptionExporter.ToAss(document, settings.Captions, width, height), new UTF8Encoding(false), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(work, "captions.srt"), CaptionExporter.ToSrt(document), new UTF8Encoding(true), cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var partial = Path.Combine(Path.GetDirectoryName(destination)!, Path.GetFileNameWithoutExtension(destination) + "." + Guid.NewGuid().ToString("N")[..8] + ".partial.mp4");
        CaptionFonts.EnsureAssets();
        var filter = CaptionFfmpegFilter.WithFonts("captions.ass", CaptionFonts.DirectoryPath);
        StatusChanged?.Invoke("Export vidéo local avec sous-titres animés…");
        try
        {
            await RunAsync(ffmpeg, ["-hide_banner", "-loglevel", "error", "-nostdin", "-i", source, "-map", "0:v:0", "-map", "0:a?", "-vf", filter, "-c:v", "libx264", "-crf", "18", "-preset", "medium", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", "-progress", "pipe:1", partial], work, cancellationToken, line =>
            {
                if (line.StartsWith("out_time_us=", StringComparison.Ordinal) && double.TryParse(line[12..], CultureInfo.InvariantCulture, out var time))
                    progress?.Report(Math.Clamp(time / 1_000_000 / Math.Max(duration, .01), 0, .999));
            });
            File.Move(partial, destination, true);
            progress?.Report(1);
            StatusChanged?.Invoke($"Vidéo exportée : {destination}");
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            throw new InvalidOperationException($"Export impossible. {error.Message}\nSous-titres et journal : {work}\nVidéo partielle : {partial}", error);
        }
    }

    private (string Name, string Sha1) ModelInfo() => settings.Captions.Model switch
    {
        "base" => ("base", "465707469ff3a37a2b9b8d8f89f2f99de7299dac"),
        "tiny" => ("tiny", "bd577a113a864445d4c299885e0cb97d4ba92b5f"),
        _ => throw new ArgumentException("Choisissez le modèle local « base » ou « tiny ».")
    };

    private static async Task<bool> VerifyModelAsync(string path, string expected, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA1.HashDataAsync(stream, cancellationToken)).Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<WhisperFactory> GetFactoryAsync(CancellationToken cancellationToken)
    {
        var path = Path.Combine(ModelsDirectory, $"ggml-{ModelInfo().Name}.bin");
        if (_factory != null && _loadedModel == path) return _factory;
        StatusChanged?.Invoke("Chargement du moteur Whisper local…");
        _factory?.Dispose();
        _factory = null;
        try { _factory = await Task.Run(() => WhisperFactory.FromPath(path), cancellationToken); }
        catch (Exception error) { throw new InvalidOperationException("Impossible de charger Whisper x64. Vérifiez le modèle et installez le runtime Microsoft Visual C++ 2015–2022 x64 si nécessaire. " + error.Message, error); }
        _loadedModel = path;
        return _factory;
    }

    private WhisperProcessor BuildProcessor(WhisperFactory factory, Action<int>? progress = null)
    {
        var builder = factory.CreateBuilder().WithTokenTimestamps().SplitOnWord().WithNoContext()
            .WithThreads(Math.Clamp(Environment.ProcessorCount - 1, 1, 8));
        builder = string.IsNullOrWhiteSpace(settings.Captions.Language) || settings.Captions.Language == "auto"
            ? builder.WithLanguageDetection() : builder.WithLanguage(settings.Captions.Language);
        if (progress != null) builder.WithProgressHandler(p => progress(p));
        return builder.Build();
    }

    private CaptionCue? CueFromSegment(SegmentData segment)
    {
        var text = segment.Text.Trim();
        if (text.Length == 0 || segment.End <= segment.Start) return null;
        var words = new List<CaptionWord>();
        foreach (var token in segment.Tokens)
        {
            var part = token.Text;
            if (string.IsNullOrEmpty(part) || part.StartsWith("[_", StringComparison.Ordinal) || token.Start < 0) continue;
            var start = TimeSpan.FromMilliseconds(token.Start * 10);
            var end = TimeSpan.FromMilliseconds(token.End * 10);
            start = start < segment.Start ? segment.Start : start > segment.End ? segment.End : start;
            end = end < start ? start : end > segment.End ? segment.End : end;
            if (char.IsWhiteSpace(part[0]) || words.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(part)) words.Add(new(part.Trim(), start, end));
            }
            else
            {
                var previous = words[^1];
                words[^1] = new(previous.Text + part, previous.Start, end > previous.End ? end : previous.End);
            }
        }
        var whole = new CaptionCue { Text = text, Start = segment.Start, End = segment.End, Words = words };
        if (!CaptionTiming.HasMatchingWords(whole) || words.Any(w => w.End <= w.Start))
        {
            words = CaptionTiming.EstimateWords(text, segment.Start, segment.End);
            whole.WordTimingsEstimated = true;
        }
        whole.Words = words;
        return whole;
    }

    private static async Task<CaptionAudioTrack> SelectAudioTrackAsync(string ffmpeg, string mediaPath, string work, CancellationToken cancellationToken)
    {
        var probe = Path.Combine(Path.GetDirectoryName(ffmpeg)!, "ffprobe.exe");
        var json = await RunAsync(probe, ["-v", "error", "-select_streams", "a", "-show_entries", "stream=index:stream_tags=handler_name,title:stream_disposition=default", "-of", "json", mediaPath], work, cancellationToken);
        using var metadata = JsonDocument.Parse(json);
        var tracks = new List<CaptionAudioTrack>();
        foreach (var stream in metadata.RootElement.GetProperty("streams").EnumerateArray())
        {
            var names = new List<string>();
            if (stream.TryGetProperty("tags", out var tags))
                foreach (var name in new[] { "handler_name", "title" })
                    if (tags.TryGetProperty(name, out var value) && !string.IsNullOrWhiteSpace(value.GetString())) names.Add(value.GetString()!);
            var isDefault = stream.TryGetProperty("disposition", out var disposition) && disposition.TryGetProperty("default", out var flag) && flag.GetInt32() == 1;
            var index = stream.GetProperty("index").GetInt32();
            tracks.Add(new(index, names.Count > 0 ? string.Join(" / ", names) : $"Audio {index}", isDefault));
        }
        return CaptionAudioSelection.Preferred(tracks);
    }

    private static float[] Resample(byte[] bytes, WaveFormat format)
    {
        using var input = new RawSourceWaveStream(new MemoryStream(bytes, false), format);
        ISampleProvider samples = input.ToSampleProvider();
        if (samples.WaveFormat.Channels == 2) samples = new StereoToMonoSampleProvider(samples) { LeftVolume = .5f, RightVolume = .5f };
        if (samples.WaveFormat.Channels != 1) throw new NotSupportedException("Choisissez un microphone mono ou stéréo pour les sous-titres live.");
        if (samples.WaveFormat.SampleRate != 16000) samples = new WdlResamplingSampleProvider(samples, 16000);
        var output = new List<float>(64000);
        var buffer = new float[4096];
        int read;
        while ((read = samples.Read(buffer, 0, buffer.Length)) > 0) output.AddRange(buffer.Take(read));
        return output.ToArray();
    }

    private string NewWorkDirectory()
    {
        var directory = Path.Combine(WorkRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static async Task<string> RunAsync(string executable, IEnumerable<string> arguments, string work, CancellationToken cancellationToken, Action<string>? onOutput = null)
    {
        var info = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = work, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new IOException("Impossible de démarrer FFmpeg.");
        var output = new StringBuilder();
        var errors = new StringBuilder();
        async Task ReadAsync(StreamReader reader, StringBuilder destination, Action<string>? handler)
        {
            while (await reader.ReadLineAsync() is { } line)
            {
                handler?.Invoke(line);
                if (destination.Length < 65536) destination.AppendLine(line);
            }
        }
        var reads = Task.WhenAll(ReadAsync(process.StandardOutput, output, onOutput), ReadAsync(process.StandardError, errors, null));
        try { await process.WaitForExitAsync(cancellationToken); }
        catch { if (!process.HasExited) process.Kill(true); await process.WaitForExitAsync(); throw; }
        finally
        {
            await reads;
            await File.WriteAllTextAsync(Path.Combine(work, "media-last.log"), errors.ToString() + output);
        }
        if (process.ExitCode != 0) throw new IOException($"FFmpeg a échoué (code {process.ExitCode}). {errors}");
        return output.ToString();
    }
}
