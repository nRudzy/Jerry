using System.Diagnostics;
using Jerry.Core.Recording;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Jerry.App.Media;

internal sealed class AudioTrackCapture
{
    private readonly WasapiCapture _capture;
    private readonly MMDevice _device;
    private readonly WaveFileWriter _writer;
    private readonly Stopwatch _clock;
    private readonly object _writeLock = new();
    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly byte[] _silence;
    private bool _closed;
    private long _framesWritten;
    public string Path { get; }
    public string Title { get; }
    public Exception? Failure { get; private set; }

    public AudioTrackCapture(string path, bool loopback, string deviceId, Stopwatch clock)
    {
        Path = path;
        Title = loopback ? "Son système" : "Microphone";
        _clock = clock;
        using var enumerator = new MMDeviceEnumerator();
        _device = loopback ? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
            : string.IsNullOrWhiteSpace(deviceId) ? enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications) : enumerator.GetDevice(deviceId);
        try
        {
            _capture = loopback ? new WasapiLoopbackCapture(_device) : new WasapiCapture(_device);
            _writer = new WaveFileWriter(path, _capture.WaveFormat);
            _silence = new byte[Math.Max(_capture.WaveFormat.BlockAlign, _capture.WaveFormat.AverageBytesPerSecond / 10)];
            _capture.DataAvailable += OnData;
            _capture.RecordingStopped += (_, e) => { Failure ??= e.Exception; _stopped.TrySetResult(); };
        }
        catch { _device.Dispose(); throw; }
    }

    public void Start() => _capture.StartRecording();

    private void OnData(object? sender, WaveInEventArgs e)
    {
        lock (_writeLock)
        {
            if (_closed || (!_clock.IsRunning && _clock.ElapsedTicks == 0)) return;
            try
            {
                var format = _capture.WaveFormat;
                var elapsedFrames = (long)(_clock.Elapsed.TotalSeconds * format.SampleRate);
                WriteSilence(RecordingMedia.SilenceFrames(_framesWritten, elapsedFrames, e.BytesRecorded / format.BlockAlign, format.SampleRate / 10));
                var frames = e.BytesRecorded / format.BlockAlign;
                // The first packet can straddle the first video image. Remove its
                // pre-video portion instead of giving audio a startup head start.
                var skip = _framesWritten == 0 ? (int)Math.Max(0, frames - elapsedFrames) : 0;
                var writableFrames = frames - skip;
                if (!_clock.IsRunning) writableFrames = (int)Math.Min(writableFrames, Math.Max(0, elapsedFrames - _framesWritten));
                var count = writableFrames * format.BlockAlign;
                _writer.Write(e.Buffer, skip * format.BlockAlign, count);
                _framesWritten += writableFrames;
                _writer.Flush(); // Keep the WAV header usable if Jerry is interrupted.
            }
            catch (Exception ex) { Failure ??= ex; _capture.StopRecording(); }
        }
    }

    private void WriteSilence(long frames)
    {
        var remaining = checked(frames * _capture.WaveFormat.BlockAlign);
        while (remaining > 0)
        {
            var count = (int)Math.Min(_silence.Length, remaining);
            _writer.Write(_silence, 0, count);
            remaining -= count;
        }
        _framesWritten += frames;
    }

    public async Task StopAsync(TimeSpan duration)
    {
        try
        {
            _capture.StopRecording();
            await _stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex) { Failure ??= ex; }
        lock (_writeLock)
        {
            if (_closed) return;
            _closed = true;
            try
            {
                WriteSilence(RecordingMedia.SilenceFrames(_framesWritten, (long)(duration.TotalSeconds * _capture.WaveFormat.SampleRate), 0, 0));
            }
            catch (Exception ex) { Failure ??= ex; }
            finally { try { _writer.Dispose(); } catch (Exception ex) { Failure ??= ex; } }
        }
        try { _capture.Dispose(); }
        finally { _device.Dispose(); }
    }
}
