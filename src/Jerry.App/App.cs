using System.Windows;
using System.Windows.Threading;

namespace Jerry.App;

public sealed class App : Application
{
    private AppController? controller;
    private Mutex? instance;
    private EventWaitHandle? show;
    private RegisteredWaitHandle? showRegistration;
    [STAThread]
    public static int Main(string[] args)
    {
        var app = new App { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.instance = new Mutex(true, @"Local\Jerry.CursorStudio", out var created);
        app.show = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\Jerry.CursorStudio.Show");
        if (!created) { app.show.Set(); app.instance.Dispose(); app.show.Dispose(); return 0; }
        app.DispatcherUnhandledException += app.OnUnhandled;
        app.Startup += (_, _) =>
        {
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Jerry");
            app.controller = new(directory, args.Contains("--background"));
            app.showRegistration = ThreadPool.RegisterWaitForSingleObject(app.show, (_, _) => app.Dispatcher.BeginInvoke(new Action(() => app.controller?.ShowSettings())), null, Timeout.Infinite, false);
        };
        app.SessionEnding += (_, _) => app.controller?.PrepareForSessionEnd();
        app.Exit += (_, _) =>
        {
            app.showRegistration?.Unregister(null); app.controller?.Dispose();
            app.show?.Dispose(); app.instance?.ReleaseMutex(); app.instance?.Dispose();
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
