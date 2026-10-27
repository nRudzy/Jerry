using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Jerry.Core.Settings;
using Jerry.App.Platform;

namespace Jerry.App.UI;

public sealed class AnnotationToolbar : Window
{
    public AnnotationToolbar(AppController controller)
    {
        Title="Jerry — Outils de dessin";Width=760;Height=132;
        WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=System.Windows.Media.Brushes.Transparent;
        ShowInTaskbar=false;ShowActivated=false;Topmost=true;
        Resources.MergedDictionaries.Add(Theme.CreateResources());
        var body=new StackPanel{Margin=new Thickness(12,7,12,8)};
        var title=Theme.Text("DESSIN  ·  déplacer la barre ici  ·  Échap pour terminer",11,Theme.Accent);title.MouseLeftButtonDown+=(_,e)=>{if(e.LeftButton==MouseButtonState.Pressed)DragMove();};body.Children.Add(title);
        var tools=new WrapPanel();var labels=new[]{"Crayon F","Flèche A","Rectangle R","Cercle C","Ligne L","Surligneur H","Texte T","Numéro N"};
        var values=Enum.GetValues<AnnotationTool>();
        for(var i=0;i<values.Length;i++){var tool=values[i];var b=new Button{Content=labels[i],Padding=new Thickness(9,6,9,6),FontSize=12,ToolTip="Choisir cet outil"};b.Click+=(_,_)=>{controller.Settings.Annotation.Tool=tool;controller.SettingsChanged();};tools.Children.Add(b);}body.Children.Add(tools);
        var row=new WrapPanel();
        void Button(string name,Action action){var b=new Button{Content=name,Padding=new Thickness(9,5,9,5),FontSize=12};b.Click+=(_,_)=>action();row.Children.Add(b);}
        Button("Couleur…",()=>{using var d=new System.Windows.Forms.ColorDialog{FullOpen=true};try{d.Color=System.Drawing.ColorTranslator.FromHtml(controller.Settings.Annotation.Tool == AnnotationTool.Highlighter ? controller.Settings.Annotation.HighlighterColor : controller.Settings.Annotation.Color);}catch{}if(d.ShowDialog()==System.Windows.Forms.DialogResult.OK){var chosen=$"#{d.Color.R:X2}{d.Color.G:X2}{d.Color.B:X2}";if(controller.Settings.Annotation.Tool==AnnotationTool.Highlighter)controller.Settings.Annotation.HighlighterColor=chosen;else controller.Settings.Annotation.Color=chosen;controller.SettingsChanged();}});
        row.Children.Add(Theme.Text("Trait",12));var width=new Slider{Minimum=1,Maximum=30,Value=controller.Settings.Annotation.Width,Width=96,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(8,0,12,0),SmallChange=1};System.Windows.Automation.AutomationProperties.SetName(width,"Épaisseur du trait");width.ValueChanged+=(_,_)=>{controller.Settings.Annotation.Width=width.Value;controller.SettingsChanged();};row.Children.Add(width);
        Button("Annuler",controller.UndoDrawing);Button("Effacer",controller.ClearDrawing);Button("Terminer",controller.ToggleDrawing);body.Children.Add(row);
        Content=new Border{Background=Theme.Surface,BorderBrush=Theme.Accent,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Child=body};
        Left=Math.Max(SystemParameters.WorkArea.Left+10,SystemParameters.WorkArea.Left+(SystemParameters.WorkArea.Width-Width)/2);Top=SystemParameters.WorkArea.Top+20;
        SourceInitialized+=(_,_)=>{ WindowTools.ExcludeFromCapture(this); var source=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle); source?.AddHook((IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)=>{if(message==0x0021){handled=true;return new IntPtr(3);}return IntPtr.Zero;}); };
    }
}
