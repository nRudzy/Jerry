using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using Jerry.App;
using Jerry.App.Captions;
using Jerry.App.Media;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using ProgressBar = System.Windows.Controls.ProgressBar;

namespace Jerry.Windows.Tests;

internal static partial class Program
{
    private static async Task QuitEditorOperationAsync(AppController controller)
    {
        var data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Jerry");
        var model = File.Exists(Path.Combine(data, "models", "ggml-tiny.bin")) ? "tiny" : "base";
        Check(File.Exists(Path.Combine(data, "models", "ggml-" + model + ".bin")), "Existing local caption model is available without a new download");
        controller.Settings.Captions.Model = model; controller.Settings.Captions.Language = "en"; controller.SettingsChanged();
        var media = Path.Combine(data, "qa", "editor-quit-" + Guid.NewGuid().ToString("N") + ".wav");
        Directory.CreateDirectory(Path.GetDirectoryName(media)!);
        await RunTool(await new ToolchainService().EnsureFfmpegAsync(), ["-hide_banner", "-loglevel", "error", "-nostdin", "-f", "lavfi", "-i", "anullsrc=r=16000:cl=mono", "-t", "600", "-c:a", "pcm_s16le", media], "editor-fixture.log");
        var phase = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Observe(string status)
        {
            if (status.Contains("Transcription Whisper", StringComparison.Ordinal)) phase.TrySetResult(status);
        }
        controller.StatusChanged += Observe;
        controller.ShowCaptionEditor(media);
        var editor = Application.Current.Windows.OfType<CaptionEditorWindow>().Single();
        var editorClosed = false; editor.Closed += (_, _) => editorClosed = true;
        await PumpAsync(150);
        var transcribe = Descendants(editor).OfType<Button>().Single(b => b.Content is TextBlock label && label.Text == "Transcrire");
        transcribe.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var wait = Stopwatch.StartNew();
        while (!phase.Task.IsCompleted && wait.Elapsed < TimeSpan.FromSeconds(30))
        {
            CollectCurrentMediaChildren();
            await Task.Delay(30);
        }
        Check(phase.Task.IsCompleted, "Real shared caption service reached announced Whisper transcription phase");
        var progress = Descendants(editor).OfType<ProgressBar>().Single();
        var inferenceWait = Stopwatch.StartNew();
        while (transcribe.IsEnabled == false && progress.Value <= .2 && inferenceWait.Elapsed < TimeSpan.FromMilliseconds(700))
        { CollectCurrentMediaChildren(); await Task.Delay(25); }
        var progressAtQuit = progress.Value;
        var processingAtQuit = !transcribe.IsEnabled;
        Check(processingAtQuit, "Actual editor operation remains busy when Quit is requested");
        CollectCurrentMediaChildren();
        Save(Snapshot(editor), "editor-operation-before-quit.png");
        Console.WriteLine("EDITOR_PHASE_AT_QUIT=" + await phase.Task);
        Console.WriteLine("EDITOR_PROGRESS_AT_QUIT=" + progressAtQuit);
        Console.WriteLine("EDITOR_NATIVE_PROGRESS_ABOVE_AUDIO_PREPARATION=" + (progressAtQuit > .2));
        var timer = Stopwatch.StartNew();
        afterExit = () =>
        {
            CollectCurrentMediaChildren();
            Console.WriteLine("EDITOR_QUIT_MS=" + timer.ElapsedMilliseconds);
            Check(editorClosed && !editor.IsVisible, "Quit closed actual CaptionEditorWindow and its operation owner");
            Check(controller.IsShuttingDown && timer.Elapsed < TimeSpan.FromSeconds(10), "Quit during actual editor operation exited within ten seconds");
            results.Add(new { editorOperation = true, fixtureSeconds = 600, model, phase = phase.Task.Result, processingAtQuit, progressAtQuit, nativeProgressObserved = progressAtQuit > .2, editorClosed, quitMilliseconds = timer.ElapsedMilliseconds, speechContent = false, ownedMediaPids = childProcesses.Distinct().ToArray() });
        };
        controller.StatusChanged -= Observe;
        controller.Quit();
    }

    private static void CollectCurrentMediaChildren()
    {
        var snapshot = Native.CreateToolhelp32Snapshot(2, 0);
        if (snapshot == new IntPtr(-1)) throw new InvalidOperationException("Own child process snapshot unavailable.");
        try
        {
            var entry = new Native.ProcessEntry { Size = (uint)Marshal.SizeOf<Native.ProcessEntry>() };
            if (!Native.Process32First(snapshot, ref entry)) return;
            do
            {
                if (entry.ParentPid == Environment.ProcessId && (entry.Executable.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase) || entry.Executable.Equals("ffprobe.exe", StringComparison.OrdinalIgnoreCase)))
                    if (!childProcesses.Contains((int)entry.Pid)) childProcesses.Add((int)entry.Pid);
            } while (Native.Process32Next(snapshot, ref entry));
        }
        finally { Native.CloseHandle(snapshot); }
    }
    private static partial class Native
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct ProcessEntry
        {
            public uint Size, Usage, Pid;
            public UIntPtr Heap;
            public uint Module, Threads, ParentPid;
            public int Priority;
            public uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Executable;
        }
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
        [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern bool Process32First(IntPtr snapshot, ref ProcessEntry entry);
        [DllImport("kernel32.dll", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode, SetLastError = true)] internal static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry entry);
        [DllImport("kernel32.dll")] internal static extern bool CloseHandle(IntPtr handle);
    }
}
