using System.Windows;
using System.Windows.Threading;
using Jerry.Windows;

namespace Jerry.App;

public sealed class App : Application
{
    private AppController? controller;
    private MutexLease? instance;
    private EventWaitHandle? show;
    private RegisteredWaitHandle? showRegistration;
    private InstanceControlServer? control;
    [STAThread]
    public static int Main(string[] args)
    {
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        using (var maintenance = InstanceControl.TryClaimMaintenance())
        {
            if (maintenance == null) return 10;
            app.instance = InstanceControl.TryClaimInstance();
        }
        app.show = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceControl.ShowEventName);
        if (app.instance == null) { app.show.Set(); app.show.Dispose(); return 0; }
        app.DispatcherUnhandledException += app.OnUnhandled;
        app.Startup += (_, _) =>
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Jerry");
            app.controller = new(directory, args.Contains("--background"));
            app.control = new InstanceControlServer(async () => await app.Dispatcher.InvokeAsync(() => app.controller.TryQuitForMaintenance()));
            app.showRegistration = ThreadPool.RegisterWaitForSingleObject(app.show, (_, _) => app.Dispatcher.BeginInvoke(new Action(() => app.controller?.ShowSettings())), null, Timeout.Infinite, false);
        };
        app.SessionEnding += (_, _) => app.controller?.PrepareForSessionEnd();
        app.Exit += (_, _) =>
        {
            app.control?.Dispose(); app.showRegistration?.Unregister(null); app.controller?.Dispose();
            app.show?.Dispose(); app.instance?.Dispose();
        };
        return app.Run();
    }
    private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Jerry", "logs");
        Directory.CreateDirectory(directory);
        File.AppendAllText(Path.Combine(directory, "errors.log"), DateTimeOffset.Now + " " + e.Exception + Environment.NewLine);
        MessageBox.Show(e.Exception.Message, "Jerry — erreur", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
