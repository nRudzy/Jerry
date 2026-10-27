using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Jerry.App;
using Jerry.App.UI;
using Jerry.Core.Settings;
using Button = System.Windows.Controls.Button;
using ComboBox = System.Windows.Controls.ComboBox;
using ListBox = System.Windows.Controls.ListBox;

namespace Jerry.Windows.Tests;

internal static partial class Program
{
    private static async Task MinimalLiveActionsAsync(AppController controller, MainWindow main, ListBox navigation)
    {
        navigation.SelectedIndex = 0; await PumpAsync(90);
        var frequency = Descendants(main).OfType<ComboBox>().Single(c => OwnControlLabel(c) == "Fréquence d’animation");
        frequency.BringIntoView(); await PumpAsync(40);
        var originalFrequencyIndex = frequency.SelectedIndex;
        var originalFrequency = controller.Settings.UpdateRate;
        try
        {
            frequency.IsDropDownOpen = true; await PumpAsync(120);
            var popup = frequency.Template.FindName("PART_Popup", frequency) as Popup;
            Check(popup?.IsOpen == true && popup.Child is FrameworkElement child && child.IsVisible, "Own framerate ComboBox opens actual WPF popup");
            var body = (FrameworkElement)popup!.Child;
            Check(body is Border border && border.Background is SolidColorBrush background && background.Color.R < 70 && background.Color.G < 70 && background.Color.B < 70, "Actual dropdown popup uses dark background");
            Save(Snapshot(body), "minimal-framerate-dropdown.png");
            frequency.SelectedIndex = 0; await PumpAsync(30);
            Check(controller.Settings.UpdateRate == 15 && frequency.SelectedIndex == 0, "Own ComboBox selection updates actual animation rate model");
        }
        finally
        {
            frequency.IsDropDownOpen = false;
            frequency.SelectedIndex = originalFrequencyIndex;
        }
        Check(controller.Settings.UpdateRate == originalFrequency, "Own dropdown restores initial animation rate");
        var drawing = Descendants(main).OfType<Button>().Single(b => OwnControlLabel(b) == "Dessiner à l’écran");
        drawing.BringIntoView(); await PumpAsync(30); drawing.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await PumpAsync(50);
        Check(controller.DrawingMode && OwnControlLabel(drawing) == "Terminer le dessin" && navigation.SelectedIndex == 0 && Descendants(main).Contains(drawing), "General drawing action updates same visible control in place without navigation");
        controller.ToggleDrawing(); await PumpAsync(40);
        Check(!controller.DrawingMode && OwnControlLabel(drawing) == "Dessiner à l’écran" && Descendants(main).Contains(drawing), "External drawing toggle restores same General action label without navigation");
        controller.Settings.Recording.CountdownSeconds = 0; controller.Settings.Recording.SystemAudio = false; controller.Settings.Recording.Microphone = false;
        controller.Settings.Recording.Resolution = OutputResolution.P720; controller.Settings.Captions.AutoOpenAfterRecording = false; controller.SettingsChanged();
        var generalRecording = Descendants(main).OfType<Button>().Single(b => OwnControlLabel(b) == "Enregistrer l’écran");
        try
        {
            generalRecording.BringIntoView(); await PumpAsync(30);
            generalRecording.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await UntilAsync(() => controller.IsRecording && !controller.IsRecordingTransition, TimeSpan.FromSeconds(20));
            await PumpAsync(30);
            Check(OwnControlLabel(generalRecording) == "Arrêter l’enregistrement" && navigation.SelectedIndex == 0 && Descendants(main).Contains(generalRecording), "Actual capture updates same General recording action in place");
            CollectOwnedProcesses(controller.Settings.Recording.OutputDirectory);
            await Task.Delay(400);
            navigation.SelectedIndex = 10; await PumpAsync(60);
            var recording = Descendants(main).OfType<Button>().Single(b => OwnControlLabel(b) == "Arrêter et sauvegarder");
            Check(controller.IsRecording && recording.IsEnabled, "Recording page reflects actual ongoing capture");
            recording.BringIntoView(); await PumpAsync(30); recording.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await UntilAsync(() => !controller.IsRecording && !controller.IsRecordingTransition, TimeSpan.FromSeconds(20)); await PumpAsync(30);
            Check(OwnControlLabel(recording) == "Démarrer la capture" && navigation.SelectedIndex == 10 && Descendants(main).Contains(recording), "Actual capture stop restores same Recording page action in place");
            Check(controller.LastRecording != null && File.Exists(controller.LastRecording), "Live-label regression finalized a real MP4 rather than simulated recording state");
            results.Add(new { liveRecordingLabels = true, mp4 = controller.LastRecording });
        }
        finally { if (controller.IsRecording) await controller.ToggleRecordingAsync(); }
        navigation.SelectedIndex = 0; await PumpAsync(40);
        Check(Descendants(main).OfType<Button>().Any(b => OwnControlLabel(b) == "Enregistrer l’écran"), "General recording action returns to idle after actual stop");
    }

    private static async Task UntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var watch = Stopwatch.StartNew();
        while (!condition() && watch.Elapsed < timeout) await Task.Delay(25);
        Check(condition(), "Observed real asynchronous UI/service transition before timeout");
    }
}
