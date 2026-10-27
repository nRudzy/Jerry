using System.Diagnostics;
using System.Runtime.InteropServices;
using Jerry.Core.Recording;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;

namespace Jerry.App.Media;

public sealed record WebcamFrame(byte[] Pixels, int Width, int Height, int Stride);

public sealed class WebcamService
{
    private readonly SemaphoreSlim _transition = new(1, 1);
    private MediaCapture? _capture;
    private MediaFrameReader? _reader;
    private int _processing;
    private volatile bool _running;
    private Process? _compatibleProcess;
    private Task? _compatibleFrames;
    private Task<string>? _compatibleErrors;
    private string? _compatibleLog;
    public bool IsRunning => _running;
    public int? OwnedProcessId => _compatibleProcess is { HasExited: false } process ? process.Id : null;
    public event Action<WebcamFrame>? FrameReady;
    public event Action<bool>? RunningChanged;
    public event Action<string>? StatusChanged;

    public async Task StartAsync(string deviceId = "", CancellationToken cancellationToken = default)
    {
        await _transition.WaitAsync(cancellationToken);
        try
        {
            if (_reader != null || _compatibleProcess != null) return;
            cancellationToken.ThrowIfCancellationRequested();
            var groups = await MediaFrameSourceGroup.FindAllAsync();
            var group = groups.FirstOrDefault(g => (string.IsNullOrWhiteSpace(deviceId) || g.Id == deviceId) && g.SourceInfos.Any(i => i.SourceKind == MediaFrameSourceKind.Color))
                ?? throw new InvalidOperationException("Aucune caméra couleur disponible. Vérifiez le périphérique et les autorisations de caméra Windows.");
            try { await StartNativeAsync(group, cancellationToken); }
            catch (COMException ex) when (ex.HResult != unchecked((int)0x80070005))
            {
                await StopCoreAsync();
                cancellationToken.ThrowIfCancellationRequested();
                await StartCompatibleAsync(group, cancellationToken);
                StatusChanged?.Invoke("Caméra active en mode compatible Windows.");
            }
            SetRunning(true);
        }
        catch { await StopCoreAsync(); throw; }
        finally { _transition.Release(); }
    }

    private async Task StartNativeAsync(MediaFrameSourceGroup group, CancellationToken cancellationToken)
    {
            var capture = new MediaCapture();
            _capture = capture;
            await capture.InitializeAsync(new MediaCaptureInitializationSettings
            {
                SourceGroup = group,
                SharingMode = MediaCaptureSharingMode.SharedReadOnly,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu,
                StreamingCaptureMode = StreamingCaptureMode.Video
            });
            cancellationToken.ThrowIfCancellationRequested();
            capture.Failed += OnFailed;
            var source = capture.FrameSources.Values.First(s => s.Info.SourceKind == MediaFrameSourceKind.Color);
            var reader = await capture.CreateFrameReaderAsync(source, MediaEncodingSubtypes.Bgra8);
            _reader = reader;
            reader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;
            reader.FrameArrived += OnFrame;
            var result = await reader.StartAsync();
            if (result != MediaFrameReaderStartStatus.Success) throw new InvalidOperationException("La caméra ne peut pas démarrer : " + result);
            cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task StartCompatibleAsync(MediaFrameSourceGroup group, CancellationToken cancellationToken)
    {
        var tools = new ToolchainService();
        var ffmpeg = await tools.EnsureFfmpegAsync(cancellationToken: cancellationToken);
        var logs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Jerry", "logs");
        Directory.CreateDirectory(logs);
        _compatibleLog = Path.Combine(logs, "webcam-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".log");
        // DirectShow and WinRT expose different interface category GUIDs for the
        // same PnP device. Match the physical path before selecting a source.
        await MediaProcess.RunAsync(ffmpeg, ["-hide_banner", "-list_devices", "true", "-f", "dshow", "-i", "dummy"], _compatibleLog, cancellationToken);
        var listing = await File.ReadAllTextAsync(_compatibleLog, cancellationToken);
        var device = group.SourceInfos.FirstOrDefault(i => i.SourceKind == MediaFrameSourceKind.Color)?.DeviceInformation?.Id ?? group.Id;
        var directShowId = RecordingMedia.DirectShowCamera(listing, device, group.DisplayName);
        var process = MediaProcess.Start(ffmpeg, ["-hide_banner", "-f", "dshow", "-rtbufsize", "128M", "-i", "video=" + directShowId,
            "-an", "-vf", "scale=640:360:force_original_aspect_ratio=increase,crop=640:360", "-pix_fmt", "bgra", "-f", "rawvideo", "pipe:1"], interactive: true);
        _compatibleProcess = process;
        _compatibleErrors = process.StandardError.ReadToEndAsync();
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _compatibleFrames = ReadCompatibleFramesAsync(process, first);
        await first.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
    }

    private async Task ReadCompatibleFramesAsync(Process process, TaskCompletionSource first)
    {
        try
        {
            while (true)
            {
                var pixels = new byte[640 * 360 * 4];
                await process.StandardOutput.BaseStream.ReadExactlyAsync(pixels);
                if (_compatibleProcess != process) continue;
                FrameReady?.Invoke(new(pixels, 640, 360, 640 * 4));
                first.TrySetResult();
            }
        }
        catch (Exception ex)
        {
            first.TrySetException(new IOException("La caméra ne produit aucune image. Journal : " + _compatibleLog, ex));
            if (_compatibleProcess == process && first.Task.IsCompletedSuccessfully)
            {
                StatusChanged?.Invoke("Caméra interrompue. Journal : " + _compatibleLog);
                _ = StopAfterFailureAsync();
            }
        }
    }

    private void OnFrame(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        if (Interlocked.Exchange(ref _processing, 1) != 0) return;
        try
        {
            using var frame = sender.TryAcquireLatestFrame();
            using var bitmap = frame?.VideoMediaFrame?.SoftwareBitmap;
            if (bitmap == null) return;
            using var converted = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
            var stride = checked(converted.PixelWidth * 4);
            var pixels = new byte[checked(stride * converted.PixelHeight)];
            var buffer = new Windows.Storage.Streams.Buffer((uint)pixels.Length);
            converted.CopyToBuffer(buffer);
            using var data = DataReader.FromBuffer(buffer);
            data.ReadBytes(pixels);
            FrameReady?.Invoke(new(pixels, converted.PixelWidth, converted.PixelHeight, stride));
        }
        catch (Exception ex) { StatusChanged?.Invoke("Image caméra indisponible : " + ex.Message); }
        finally { Interlocked.Exchange(ref _processing, 0); }
    }

    private void OnFailed(MediaCapture sender, MediaCaptureFailedEventArgs args)
    {
        StatusChanged?.Invoke("Caméra interrompue : " + args.Message);
        _ = StopAfterFailureAsync();
    }
    private async Task StopAfterFailureAsync()
    {
        try { await StopAsync(); } catch (Exception ex) { StatusChanged?.Invoke("Arrêt caméra : " + ex.Message); }
    }

    public async Task StopAsync()
    {
        await _transition.WaitAsync();
        try { await StopCoreAsync(); }
        finally { _transition.Release(); }
    }
    private void SetRunning(bool running)
    {
        if (_running == running) return;
        _running = running;
        RunningChanged?.Invoke(running);
    }
    private async Task StopCoreAsync()
    {
        SetRunning(false);
        var compatible = _compatibleProcess;
        _compatibleProcess = null;
        if (compatible != null)
        {
            try
            {
                if (!compatible.HasExited)
                {
                    try { await compatible.StandardInput.WriteLineAsync("q"); await compatible.StandardInput.FlushAsync(); } catch (IOException) { }
                    try { await compatible.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3)); }
                    catch (TimeoutException) { if (!compatible.HasExited) compatible.Kill(entireProcessTree: true); await compatible.WaitForExitAsync(); }
                }
                if (_compatibleFrames != null) await _compatibleFrames;
                if (_compatibleErrors != null && _compatibleLog != null) await File.AppendAllTextAsync(_compatibleLog, await _compatibleErrors);
            }
            finally { compatible.Dispose(); _compatibleFrames = null; _compatibleErrors = null; }
        }
        var reader = _reader;
        _reader = null;
        try
        {
            if (reader != null)
            {
                reader.FrameArrived -= OnFrame;
                try { await reader.StopAsync(); } finally { reader.Dispose(); }
            }
        }
        finally
        {
            if (_capture != null) { _capture.Failed -= OnFailed; _capture.Dispose(); _capture = null; }
        }
    }
}
