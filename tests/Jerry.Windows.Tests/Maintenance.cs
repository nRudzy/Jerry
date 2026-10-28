using System.Diagnostics;
using System.IO;
using Jerry.App;
using Jerry.App.Media;
using Jerry.App.UI;

namespace Jerry.Windows.Tests;

internal static partial class Program
{
    private static async Task MaintenanceAsync(AppController controller, MainWindow main)
    {
        // This uses only this harness's controller and data, never the user's app instance.
        controller.Settings.Recording.CountdownSeconds = 5;
        var start = controller.ToggleRecordingAsync();
        await PumpAsync(80);
        Check(controller.IsRecordingTransition && !start.IsCompleted, "Maintenance fixture starts a real pending recording transition");
        Check(!controller.TryQuitForMaintenance(), "Maintenance refuses a pending countdown or tool preparation");
        Check(!controller.IsShuttingDown && !start.IsCompleted, "Refusal does not cancel countdown or start shutdown");
        await controller.ToggleRecordingAsync();
        await start.WaitAsync(TimeSpan.FromSeconds(30));
        Check(!controller.IsRecording && !controller.IsRecordingTransition, "Only explicit fixture cancellation stops the pending start");

        controller.Settings.Recording.CountdownSeconds = 0;
        await controller.ToggleRecordingAsync().WaitAsync(TimeSpan.FromMinutes(8));
        Check(controller.IsRecording, "Maintenance fixture begins a real video capture");
        var recorder = (RecordingService)typeof(AppController).GetField("recording", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(controller)!;
        var pid = recorder.OwnedProcessId;
        Check(pid != null, "Actual capture owns a media process");
        Check(!controller.TryQuitForMaintenance(), "Maintenance refuses an ongoing video recording");
        await PumpAsync(350);
        using (var process = Process.GetProcessById(pid!.Value))
            Check(!process.HasExited && controller.IsRecording && !controller.IsShuttingDown, "Refused maintenance leaves the same capture process and recording running");
        await controller.ToggleRecordingAsync();
        Check(!controller.IsRecording && controller.LastRecording != null && File.Exists(controller.LastRecording), "An explicit stop finalizes the test recording into an MP4");

        controller.Settings.Camera.Enabled = true;
        Check(!controller.TryQuitForMaintenance(), "Maintenance preserves an enabled camera session");
        controller.Settings.Camera.Enabled = false;
        controller.Settings.Captions.LiveEnabled = true;
        Check(!controller.TryQuitForMaintenance(), "Maintenance preserves a live caption session");
        controller.Settings.Captions.LiveEnabled = false;
        controller.ShowCaptionEditor(controller.LastRecording!);
        Check(!controller.TryQuitForMaintenance(), "Maintenance preserves an open caption editor and its work");
        foreach (var editor in System.Windows.Application.Current.Windows.OfType<Jerry.App.Captions.CaptionEditorWindow>().ToArray()) editor.Close();

        afterExit = () =>
        {
            Check(controller.IsShuttingDown && !main.IsVisible, "Idle maintenance uses normal controller shutdown");
            CollectOwnedProcesses(controller.Settings.Recording.OutputDirectory);
        };
        Check(controller.TryQuitForMaintenance(), "Idle maintenance accepts a graceful exit");
    }
}
