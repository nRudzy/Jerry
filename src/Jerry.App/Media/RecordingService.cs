using System.Diagnostics;
using System.Text.Json;
using Jerry.Core.Recording;
using Jerry.Core.Settings;
using Microsoft.Win32;

namespace Jerry.App.Media;

public sealed class RecordingService
{
    private readonly AppSettings _settings;
    private readonly ToolchainService _tools;
    private readonly SemaphoreSlim _transition = new(1, 1);
    private readonly List<AudioTrackCapture> _audio = [];
    private readonly Stopwatch _clock = new();
    private Process? _process;
    private Task? _readProgress;
    private Task? _readErrors;
    private string? _sessionDirectory;
    private string? _finalPath;
    private string? _ffmpeg;
    private string? _lastOutput;
    public bool IsRecording { get; private set; }
    public string? RecoveryDirectory => _sessionDirectory;
    public string? LastOutputPath => _lastOutput;
    public int? OwnedProcessId => _process is { HasExited: false } p ? p.Id : null;
    public event Action<string>? StatusChanged;
    public event Action<bool>? RecordingChanged;

    public RecordingService(AppSettings settings, ToolchainService tools) { _settings = settings; _tools = tools; }

    public async Task StartAsync(PixelRect bounds, CancellationToken cancellationToken = default)
    {
        await _transition.WaitAsync(cancellationToken);
        try
        {
            if (_process != null) return;
            // Validate before creating files or opening devices.
            RecordingMedia.CaptureArguments(bounds, _settings.Recording, "validate.mkv");
            StatusChanged?.Invoke("Préparation de l’enregistrement…");
            _ffmpeg = await _tools.EnsureFfmpegAsync(cancellationToken: cancellationToken);
            var directory = Path.GetFullPath(_settings.Recording.OutputDirectory);
            Directory.CreateDirectory(directory);
            var id = "Jerry-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6];
            _sessionDirectory = Path.Combine(directory, ".Jerry-sessions", id);
            Directory.CreateDirectory(_sessionDirectory);
            _finalPath = Path.Combine(directory, id + ".mp4");
            _lastOutput = null;
            await File.WriteAllTextAsync(Path.Combine(_sessionDirectory, "session.json"), JsonSerializer.Serialize(new
            {
                startedUtc = DateTimeOffset.UtcNow, bounds, recording = _settings.Recording,
                tool = _ffmpeg, recovery = "screen.mkv + system.wav / microphone.wav; mux.log explains finalization failures"
            }, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
            _clock.Reset();
            await Task.Run(() =>
            {
                if (_settings.Recording.SystemAudio) _audio.Add(new AudioTrackCapture(Path.Combine(_sessionDirectory, "system.wav"), true, "", _clock));
                if (_settings.Recording.Microphone) _audio.Add(new AudioTrackCapture(Path.Combine(_sessionDirectory, "microphone.wav"), false, _settings.Recording.MicrophoneDeviceId, _clock));
                foreach (var track in _audio) track.Start();
            }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var process = MediaProcess.Start(_ffmpeg, RecordingMedia.CaptureArguments(bounds, _settings.Recording, Path.Combine(_sessionDirectory, "screen.mkv")), interactive: true);
            _process = process;
            await File.WriteAllTextAsync(Path.Combine(_sessionDirectory, "owned-process.json"), JsonSerializer.Serialize(new { pid = process.Id, executable = _ffmpeg, startedUtc = DateTimeOffset.UtcNow }), cancellationToken);
            var firstFrame = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _readProgress = ReadProgressAsync(process, firstFrame);
            _readErrors = SaveErrorsAsync(process, Path.Combine(_sessionDirectory, "capture.log"));
            await firstFrame.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            if (process.HasExited) throw new IOException("FFmpeg s’est arrêté pendant le démarrage. Consultez capture.log.");
            IsRecording = true;
            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
            RecordingChanged?.Invoke(true);
            StatusChanged?.Invoke("Enregistrement en cours");
            _ = ObserveExitAsync(process);
            _ = ObserveAudioAsync(process, _audio.ToArray());
        }
        catch
        {
            await AbortStartAsync();
            StatusChanged?.Invoke("Échec du démarrage. Fichiers conservés : " + _sessionDirectory);
            throw;
        }
        finally { _transition.Release(); }
    }

    private static async Task SaveErrorsAsync(Process process, string path)
    {
        await using var log = new StreamWriter(path) { AutoFlush = true };
        while (await process.StandardError.ReadLineAsync() is { } line) await log.WriteLineAsync(line);
    }
    private async Task ReadProgressAsync(Process process, TaskCompletionSource firstFrame)
    {
        while (await process.StandardOutput.ReadLineAsync() is { } line)
            if (line.StartsWith("frame=", StringComparison.Ordinal) && int.TryParse(line.AsSpan(6), out var frame) && frame > 0 && !firstFrame.Task.IsCompleted)
            {
                _clock.Start();
                firstFrame.TrySetResult();
            }
        firstFrame.TrySetException(new IOException("FFmpeg n’a produit aucune image. Consultez capture.log."));
    }

    private async Task ObserveExitAsync(Process process)
    {
        try
        {
            await process.WaitForExitAsync();
            if (_process != process || !IsRecording) return;
            StatusChanged?.Invoke("Capture interrompue ; récupération des fichiers…");
            await StopAsync();
        }
        catch (Exception ex) { StatusChanged?.Invoke("Capture interrompue : " + ex.Message + " Fichiers : " + _sessionDirectory); }
    }
    private async Task ObserveAudioAsync(Process process, AudioTrackCapture[] audioTracks)
    {
        try
        {
            while (_process == process && IsRecording)
            {
                await Task.Delay(500);
                var failure = audioTracks.Select(a => a.Failure).FirstOrDefault(e => e != null);
                if (failure == null) continue;
                StatusChanged?.Invoke("Entrée audio interrompue : " + failure.Message);
                await StopAsync();
                return;
            }
        }
        catch (Exception ex) { StatusChanged?.Invoke("Récupération audio : " + ex.Message); }
    }
    private void OnDisplayChanged(object? sender, EventArgs e) => _ = StopAfterDisplayChangeAsync();
    private async Task StopAfterDisplayChangeAsync()
    {
        try { StatusChanged?.Invoke("Configuration d’écran modifiée ; finalisation de la capture…"); await StopAsync(); }
        catch (Exception ex) { StatusChanged?.Invoke("Capture conservée après changement d’écran : " + ex.Message); }
    }

    public async Task<string?> StopAsync(CancellationToken cancellationToken = default)
    {
        await _transition.WaitAsync(cancellationToken);
        var endedSession = false;
        try
        {
            var process = _process;
            if (process == null) return _lastOutput;
            endedSession = true;
            IsRecording = false;
            SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            StatusChanged?.Invoke("Finalisation de l’enregistrement…");
            _clock.Stop();
            var finishVideo = EndProcessAsync(process);
            var failures = new List<string>();
            var audioTracks = _audio.ToArray();
            foreach (var track in audioTracks)
            {
                try { await track.StopAsync(_clock.Elapsed); }
                catch (Exception ex) { failures.Add(ex.Message); }
                if (track.Failure != null) failures.Add(track.Title + " : " + track.Failure.Message);
            }
            _audio.Clear();
            try
            {
                var graceful = await finishVideo;
                if (!graceful) failures.Add("FFmpeg a dû être interrompu après le délai de finalisation.");
                if (_readErrors != null) await _readErrors;
                if (_readProgress != null) await _readProgress;
                if (process.ExitCode != 0) failures.Add("FFmpeg capture : code " + process.ExitCode);
            }
            finally { _process = null; process.Dispose(); }
            var temporary = Path.Combine(_sessionDirectory!, "finalizing.mp4");
            var result = await MediaProcess.RunAsync(_ffmpeg!, RecordingMedia.MuxArguments(Path.Combine(_sessionDirectory!, "screen.mkv"),
                audioTracks.Select(a => (a.Path, a.Title)).ToArray(), temporary), Path.Combine(_sessionDirectory!, "mux.log"), cancellationToken);
            if (result != 0 || !File.Exists(temporary) || new FileInfo(temporary).Length == 0)
                throw new IOException("Impossible de finaliser le MP4. Capture récupérable : " + _sessionDirectory);
            File.Move(temporary, _finalPath!);
            _lastOutput = _finalPath;
            await File.WriteAllTextAsync(Path.Combine(_sessionDirectory!, "result.json"), JsonSerializer.Serialize(new { output = _lastOutput, durationSeconds = _clock.Elapsed.TotalSeconds, warnings = failures }));
            if (failures.Count > 0)
                StatusChanged?.Invoke("MP4 récupéré avec avertissement : " + string.Join(" ; ", failures) + " Originaux : " + _sessionDirectory);
            else StatusChanged?.Invoke("Enregistrement sauvegardé : " + _lastOutput);
            return _lastOutput;
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke("Finalisation incomplète : " + ex.Message + " Fichiers conservés : " + _sessionDirectory);
            throw;
        }
        finally
        {
            try { if (endedSession) RecordingChanged?.Invoke(false); }
            finally { _transition.Release(); }
        }
    }

    private async Task AbortStartAsync()
    {
        _clock.Stop();
        SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
        IsRecording = false;
        foreach (var track in _audio)
            try { await track.StopAsync(_clock.Elapsed); } catch (Exception) { /* Original files remain in the session directory. */ }
        _audio.Clear();
        if (_process is { } process)
        {
            try { await EndProcessAsync(process); if (_readErrors != null) await _readErrors; if (_readProgress != null) await _readProgress; }
            catch (Exception) { /* Preserve capture and log; startup exception is reported to the caller. */ }
            finally { process.Dispose(); _process = null; }
        }
    }

    private static async Task<bool> EndProcessAsync(Process process)
    {
        if (process.HasExited) return true;
        try { await process.StandardInput.WriteLineAsync("q"); await process.StandardInput.FlushAsync(); }
        catch (IOException) { }
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15)); return true; }
        catch (TimeoutException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            return false;
        }
    }
}
