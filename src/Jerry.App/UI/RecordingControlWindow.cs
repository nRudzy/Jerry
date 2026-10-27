using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using Jerry.App.Platform;

namespace Jerry.App.UI;

public sealed class RecordingControlWindow : Window
{
    private readonly AppController controller;
    private readonly TextBlock elapsed;
    private readonly Button stop;
    private readonly DispatcherTimer timer;
    private DateTimeOffset? started;
    private int? countdown;
    public RecordingControlWindow(AppController controller)
    {
        this.controller = controller;
        Title = "Jerry — Commandes de capture"; Width = 290; Height = 78;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = System.Windows.Media.Brushes.Transparent;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        Resources.MergedDictionaries.Add(Theme.CreateResources());
        var row = new Grid { Margin = new Thickness(14, 8, 10, 8) };
        row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var readout = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        readout.Children.Add(Theme.Text("●  CAPTURE  ·  déplacer ici", 10, Theme.Accent)); elapsed = Theme.Text("Prêt", 20); readout.Children.Add(elapsed);
        readout.MouseLeftButtonDown += (_, e) => { if(e.LeftButton==MouseButtonState.Pressed) DragMove(); };row.Children.Add(readout);
        stop = new Button { Content = "■  Arrêter", VerticalAlignment = VerticalAlignment.Center };
        stop.Click += async (_, _) => { stop.IsEnabled=false;try{await controller.ToggleRecordingAsync();}catch(Exception ex){MessageBox.Show(ex.Message,"Jerry");}finally{stop.IsEnabled=true;} };Grid.SetColumn(stop,1);row.Children.Add(stop);
        Content = new Border { Background=Theme.Surface, BorderBrush=Theme.Accent, BorderThickness=new Thickness(1), CornerRadius=new CornerRadius(16), Child=row };
        Left = SystemParameters.WorkArea.Right - Width - 24; Top = SystemParameters.WorkArea.Top + 24;
        SourceInitialized += (_,_) => { WindowTools.ExcludeFromCapture(this); var source=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle); source?.AddHook((IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)=>{if(message==0x0021){handled=true;return new IntPtr(3);}return IntPtr.Zero;}); };
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };timer.Tick += (_,_)=>Update();
        Loaded += (_,_)=>{timer.Start();Update();};
        controller.StateChanged += Update;
        Closing += (_,e)=>{if(!controller.IsShuttingDown&&(controller.IsRecording||controller.IsRecordingTransition)){e.Cancel=true;return;}timer.Stop();controller.StateChanged-=Update;};
    }
    public void SetCountdown(int? seconds) { countdown=seconds; if(seconds.HasValue)started=null;Update(); }
    private void Update()
    {
        if(countdown is int remaining){elapsed.Text=remaining>0?$"Départ dans {remaining}…":"Départ…";stop.IsEnabled=false;return;}
        stop.IsEnabled=controller.IsRecording&&!controller.IsBusy;
        if(controller.IsRecording){started??=DateTimeOffset.Now;var duration=DateTimeOffset.Now-started.Value;elapsed.Text=duration.TotalHours>=1?duration.ToString(@"hh\:mm\:ss"):duration.ToString(@"mm\:ss");}
        else{started=null;elapsed.Text=controller.IsBusy?"Finalisation…":"Capture terminée";}
    }
}
