using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Jerry.Core.Settings;
using Jerry.App.Media;
using Microsoft.Win32;

namespace Jerry.App.UI;

public sealed class MainWindow : Window
{
    private readonly AppController controller;
    private readonly StackPanel page = new();
    private readonly TextBlock status;
    private readonly TextBlock heading;
    private readonly TextBlock subtitle;
    private readonly ListBox navigation;
    private readonly string[] titles = { "Vue d’ensemble", "Curseur et clics", "Annotations", "Touches à l’écran", "Zoom et focus", "Enregistrement", "Caméra", "Sous-titres", "Raccourcis", "Préférences" };
    private AppSettings S => controller.Settings;

    public MainWindow(AppController controller)
    {
        this.controller = controller;
        Title = "Jerry — Votre présentation, au premier plan";
        var workArea = SystemParameters.WorkArea;
        var availableWidth = Math.Max(320, workArea.Width - 32);
        var availableHeight = Math.Max(320, workArea.Height - 32);
        MinWidth = Math.Min(800, availableWidth); MinHeight = Math.Min(560, availableHeight);
        Width = Math.Min(1080, availableWidth); Height = Math.Min(800, availableHeight);
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Theme.Background; Foreground = Theme.Foreground;
        Resources.MergedDictionaries.Add(Theme.CreateResources());
        var root = new Grid(); root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(224) }); root.ColumnDefinitions.Add(new ColumnDefinition());
        var sidebar = new DockPanel { Background = Theme.Surface, LastChildFill = true, Margin = new Thickness(0) };
        var brand = new StackPanel { Margin = new Thickness(24, 26, 20, 24) };
        brand.Children.Add(Theme.Text("jerry", 32, Theme.Accent));
        brand.Children.Add(Theme.Text("L’attention, simplement.", 12, Theme.Muted));
        DockPanel.SetDock(brand, Dock.Top); sidebar.Children.Add(brand);
        var lower = new StackPanel { Margin = new Thickness(18) };
        lower.Children.Add(Action("Ouvrir les captures", controller.OpenOutputFolder));
        lower.Children.Add(Action("Quitter Jerry", controller.Quit));
        DockPanel.SetDock(lower, Dock.Bottom); sidebar.Children.Add(lower);
        navigation = new ListBox { BorderThickness = new Thickness(0), Background = Brushes.Transparent, Foreground = Theme.Foreground, Margin = new Thickness(10, 0, 10, 0), FontSize = 14 };
        ScrollViewer.SetVerticalScrollBarVisibility(navigation, ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(navigation, ScrollBarVisibility.Disabled);
        foreach (var title in titles) navigation.Items.Add(new ListBoxItem { Content = title, Padding = new Thickness(12, 12, 8, 12), Margin = new Thickness(0, 2, 0, 2) });
        navigation.SelectionChanged += (_, _) => ShowPage(navigation.SelectedIndex);
        sidebar.Children.Add(navigation); root.Children.Add(sidebar);
        var main = new DockPanel { Margin = new Thickness(32, 25, 28, 18) }; Grid.SetColumn(main, 1);
        var header = new StackPanel(); heading = Theme.Text("", 27); heading.FontWeight = FontWeights.SemiBold;
        subtitle = Theme.Text("", 13, Theme.Muted); subtitle.Margin = new Thickness(0, 4, 0, 20);
        header.Children.Add(heading); header.Children.Add(subtitle); DockPanel.SetDock(header, Dock.Top); main.Children.Add(header);
        status = Theme.Text(controller.Status, 12, Theme.Accent); status.Margin = new Thickness(0, 14, 0, 0); AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
        DockPanel.SetDock(status, Dock.Bottom); main.Children.Add(status);
        main.Children.Add(new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(0, 0, 12, 0) });
        root.Children.Add(main); Content = root;
        controller.StatusChanged += OnStatus; controller.StateChanged += OnState;
        Closed += (_, _) => { controller.StatusChanged -= OnStatus; controller.StateChanged -= OnState; };
        Closing += OnClosing;
        Activated += (_, _) => { if(navigation.SelectedIndex >= 0) ShowPage(navigation.SelectedIndex); };
        navigation.SelectedIndex = 0;
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (controller.IsShuttingDown) return;
        e.Cancel = true;
        if (S.MinimizeToTray) Hide();
        else Dispatcher.BeginInvoke(new Action(controller.Quit));
    }
    private void OnStatus(string value) => status.Text = value;
    private void OnState() { status.Text = controller.Status; if (navigation.SelectedIndex == 0) ShowPage(0); }
    private void Save() => controller.SettingsChanged();
    private async Task Run(Func<Task> operation) { try { await operation(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Jerry", MessageBoxButton.OK, MessageBoxImage.Warning); } }
    private Button Action(string text, Action action) { var b = new Button { Content = text }; b.Click += (_, _) => action(); return b; }
    private Button AsyncAction(string text, Func<Task> action) { var b = new Button { Content = text }; b.Click += async (_, _) => await Run(action); return b; }
    private StackPanel Card(string title, string? explanation = null)
    {
        var body = new StackPanel { Margin = new Thickness(20, 15, 20, 17) };
        body.Children.Add(Theme.Text(title, 18));
        if (explanation != null) body.Children.Add(Theme.Text(explanation, 12, Theme.Muted));
        page.Children.Add(new Border { Background = Theme.Surface, CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 0, 16), Child = body });
        return body;
    }
    private void Check(Panel p, string label, bool value, Action<bool> set)
    {
        var c = new CheckBox { Content = label, IsChecked = value }; AutomationProperties.SetName(c, label);
        c.Click += (_, _) => { set(c.IsChecked == true); Save(); }; p.Children.Add(c);
    }
    private void AsyncCheck(Panel p, string label, bool value, Func<bool, Task> set)
    {
        var c = new CheckBox { Content = label, IsChecked = value }; AutomationProperties.SetName(c, label);
        c.Click += async (_, _) => { c.IsEnabled = false; await Run(() => set(c.IsChecked == true)); c.IsChecked = label.Contains("caméra", StringComparison.OrdinalIgnoreCase) ? S.Camera.Enabled : S.Captions.LiveEnabled; c.IsEnabled = true; }; p.Children.Add(c);
    }
    private void Range(Panel p, string label, double value, double min, double max, double step, Action<double> set, string unit = "")
    {
        var row = new Grid { Margin = new Thickness(0, 9, 0, 2) }; row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(Theme.Text(label, 13)); var number = Theme.Text(value.ToString("0.##", CultureInfo.CurrentCulture) + unit, 13, Theme.Accent); Grid.SetColumn(number, 1); row.Children.Add(number); p.Children.Add(row);
        var slider = new Slider { Minimum = min, Maximum = max, Value = value, TickFrequency = step, SmallChange = step, LargeChange = step * 5, IsSnapToTickEnabled = true, Margin = new Thickness(0, 0, 0, 9) };
        AutomationProperties.SetName(slider, label); slider.ValueChanged += (_, _) => { number.Text = slider.Value.ToString("0.##", CultureInfo.CurrentCulture) + unit; set(slider.Value); Save(); }; p.Children.Add(slider);
    }
    private void Choice<T>(Panel p, string label, T value, Action<T> set) where T : struct, Enum
    {
        var options = Enum.GetValues<T>(); var c = new ComboBox { Margin = new Thickness(0, 1, 0, 10), ItemsSource = options.Select(x => Label(x.ToString())).ToArray(), SelectedIndex = Array.IndexOf(options, value) };
        p.Children.Add(Theme.Text(label, 13)); AutomationProperties.SetName(c, label); c.SelectionChanged += (_, _) => { if (c.SelectedIndex >= 0) { set(options[c.SelectedIndex]); Save(); } }; p.Children.Add(c);
    }
    private void Options(Panel p, string label, string value, string[] options, Action<string> set)
    {
        var c = new ComboBox { ItemsSource = options.Select(OptionLabel).ToArray(), SelectedIndex = Array.IndexOf(options, value), Margin = new Thickness(0, 1, 0, 10) }; p.Children.Add(Theme.Text(label, 13)); AutomationProperties.SetName(c, label);
        c.SelectionChanged += (_, _) => { if (c.SelectedIndex >= 0) { set(options[c.SelectedIndex]); Save(); } }; p.Children.Add(c);
    }
    private void Input(Panel p, string label, string value, Action<string> set)
    {
        p.Children.Add(Theme.Text(label, 13)); var box = new TextBox { Text = value, Margin = new Thickness(0, 1, 0, 10) }; AutomationProperties.SetName(box, label);
        box.LostKeyboardFocus += (_, _) => { set(box.Text.Trim()); Save(); }; p.Children.Add(box);
    }
    private void Color(Panel p, string label, string value, Action<string> set)
    {
        var button = Action(label + "  " + value, () => { using var picker = new System.Windows.Forms.ColorDialog { FullOpen = true }; try { picker.Color = System.Drawing.ColorTranslator.FromHtml(value); } catch { }
            if (picker.ShowDialog() == System.Windows.Forms.DialogResult.OK) { var hex = $"#{picker.Color.R:X2}{picker.Color.G:X2}{picker.Color.B:X2}"; set(hex); Save(); ShowPage(navigation.SelectedIndex); } }); p.Children.Add(button);
    }
    private void ShowPage(int index)
    {
        if (index < 0) return; page.Children.Clear(); heading.Text = titles[index];
        subtitle.Text = index switch { 0 => "Vos outils de présentation Windows, réunis au même endroit.", 5 => "Une prise, vos effets et deux pistes audio indépendantes.", 7 => "Transcription locale : votre voix reste sur cet ordinateur.", 8 => "Aucune combinaison n’est réservée avant votre choix.", _ => "Les réglages sont conservés automatiquement." };
        switch (index) { case 0: Overview(); break; case 1: CursorPage(); break; case 2: Annotations(); break; case 3: Keys(); break; case 4: Zoom(); break; case 5: Recording(); break; case 6: Camera(); break; case 7: Captions(); break; case 8: Hotkeys(); break; case 9: Preferences(); break; }
    }
    private void Overview()
    {
        var p = Card("Prêt pour votre prochaine démonstration", "Activez vos effets, puis lancez une capture ou dessinez à l’écran.");
        Check(p, "Activer les effets Jerry", S.Enabled, v => S.Enabled = v);
        var actions = new WrapPanel(); actions.Children.Add(AsyncAction(controller.IsRecording ? "Arrêter l’enregistrement" : "Enregistrer l’écran", controller.ToggleRecordingAsync)); actions.Children.Add(Action(controller.DrawingMode ? "Terminer le dessin" : "Dessiner à l’écran", controller.ToggleDrawing)); actions.Children.Add(Action("Sous-titrer une vidéo", () => controller.ShowCaptionEditor())); p.Children.Add(actions);
        var tools = Card("Votre boîte à outils"); foreach (var item in new[] { ("Lumière et mouvement", "Spotlight, anneau, sept traînées et effets de clic.", 1), ("Présenter avec précision", "Annotations, touches, zoom et focus de fenêtre.", 2), ("Enregistrer et raconter", "Capture MP4, caméra et sous-titres locaux.", 5) }) { tools.Children.Add(Theme.Text(item.Item2, 13, Theme.Muted)); tools.Children.Add(Action(item.Item1 + "  →", () => navigation.SelectedIndex = item.Item3)); }
        var info = Card("Capturer le bon écran", "Placez le pointeur sur l’écran à enregistrer avant la prise. Les contrôles flottants sont exclus de la vidéo. Couper les effets n’interrompt pas l’enregistrement."); info.Children.Add(Theme.Text("Configurez vos raccourcis pour activer un effet sans quitter votre présentation. Retrouvez vos captures avec le bouton Ouvrir les captures.", 12, Theme.Muted));
    }
    private void CursorPage()
    {
        var p = Card("Spotlight", "Éclairez une zone autour du pointeur et atténuez le reste."); Check(p,"Activer le spotlight",S.Spotlight.Enabled,v=>S.Spotlight.Enabled=v); Choice(p,"Forme",S.Spotlight.Shape,v=>S.Spotlight.Shape=v); Range(p,"Rayon",S.Spotlight.Radius,10,600,5,v=>S.Spotlight.Radius=v," px"); Range(p,"Épaisseur de la barre",S.Spotlight.BarThickness,10,800,5,v=>S.Spotlight.BarThickness=v," px"); Range(p,"Assombrissement",S.Spotlight.Opacity,0,1,.05,v=>S.Spotlight.Opacity=v); Color(p,"Teinte",S.Spotlight.Color,v=>S.Spotlight.Color=v); Check(p,"Masquer le pointeur dans le spotlight",S.Spotlight.HideCursor,v=>S.Spotlight.HideCursor=v); Check(p,"Éteindre au déplacement",S.Spotlight.TurnOffWhenMoving,v=>{S.Spotlight.TurnOffWhenMoving=v;if(v)S.Spotlight.HoldKey="None";ShowPage(1);}); Options(p,"Afficher en maintenant",S.Spotlight.HoldKey,HoldKeys,v=>{S.Spotlight.HoldKey=v;if(v!="None")S.Spotlight.TurnOffWhenMoving=false;ShowPage(1);});
        Range(p,"Distance avant extinction",S.Spotlight.MoveDistance,1,300,1,v=>S.Spotlight.MoveDistance=v," px");Range(p,"Délai du maintien",S.Spotlight.HoldDelayMilliseconds,0,1500,50,v=>S.Spotlight.HoldDelayMilliseconds=(int)v," ms");Range(p,"Opacité de la zone éclairée",S.Spotlight.AreaOpacity,0,1,.05,v=>S.Spotlight.AreaOpacity=v);Color(p,"Couleur de la zone éclairée",S.Spotlight.HighlightColor,v=>S.Spotlight.HighlightColor=v);p=Card("Anneau");Check(p,"Activer l’anneau",S.Ring.Enabled,v=>S.Ring.Enabled=v);Range(p,"Rayon de l’anneau",S.Ring.Radius,5,100,1,v=>S.Ring.Radius=v," px");Range(p,"Épaisseur",S.Ring.Thickness,1,15,.5,v=>S.Ring.Thickness=v," px");Range(p,"Opacité",S.Ring.Opacity,0,1,.05,v=>S.Ring.Opacity=v);Color(p,"Couleur de l’anneau",S.Ring.Color,v=>S.Ring.Color=v);Check(p,"Animer l’anneau",S.Ring.Animate,v=>S.Ring.Animate=v);Range(p,"Vitesse d’animation",S.Ring.Speed,.2,4,.1,v=>S.Ring.Speed=v);
        Check(p,"Halo néon",S.Ring.NeonGlow,v=>S.Ring.NeonGlow=v);Range(p,"Vitesse de suivi",S.Ring.FollowSpeed,.05,1,.05,v=>S.Ring.FollowSpeed=v);p=Card("Traînée du curseur");Check(p,"Activer la traînée",S.Trail.Enabled,v=>S.Trail.Enabled=v);Choice(p,"Style de traînée",S.Trail.Style,v=>S.Trail.Style=v);Color(p,"Couleur de la traînée",S.Trail.Color,v=>S.Trail.Color=v);Range(p,"Longueur",S.Trail.Length,4,150,1,v=>S.Trail.Length=(int)v);Range(p,"Largeur",S.Trail.Width,1,40,1,v=>S.Trail.Width=v," px");Range(p,"Opacité de traînée",S.Trail.Opacity,0,1,.05,v=>S.Trail.Opacity=v);Range(p,"Effacement progressif",S.Trail.FadeMilliseconds,100,3000,50,v=>S.Trail.FadeMilliseconds=(int)v," ms");
        Range(p,"Intervalle d’apparition (plus bas = plus dense)",S.Trail.SpawnIntervalMilliseconds,1,100,1,v=>S.Trail.SpawnIntervalMilliseconds=(int)v," ms");p=Card("Clics visibles");Check(p,"Activer les effets de clic",S.Click.Enabled,v=>S.Click.Enabled=v);Check(p,"Afficher à chaque clic",S.Click.Automatic,v=>S.Click.Automatic=v);Choice(p,"Effet de clic",S.Click.Style,v=>S.Click.Style=v);Color(p,"Couleur de l’effet",S.Click.Color,v=>S.Click.Color=v);Range(p,"Taille du clic",S.Click.Size,10,200,5,v=>S.Click.Size=v," px");Range(p,"Épaisseur des cercles",S.Click.Thickness,1,15,.5,v=>S.Click.Thickness=v," px");Range(p,"Vitesse du clic",S.Click.Speed,.2,4,.1,v=>S.Click.Speed=v);Options(p,"Déclencher par une touche isolée",S.Click.TriggerKey,HoldKeys,v=>S.Click.TriggerKey=v);
        p=Card("Son des clics");Check(p,"Jouer un son au clic",S.Sound.Enabled,v=>S.Sound.Enabled=v);Range(p,"Volume",S.Sound.Volume,0,1,.05,v=>S.Sound.Volume=v);p.Children.Add(Action("Choisir un son…",()=>{var d=new OpenFileDialog{Filter="Fichiers audio|*.wav;*.mp3;*.aiff;*.wma|Tous les fichiers|*.*"};if(d.ShowDialog(this)==true){S.Sound.FilePath=d.FileName;Save();ShowPage(1);}}));p.Children.Add(Theme.Text(string.IsNullOrWhiteSpace(S.Sound.FilePath)?"Son intégré":S.Sound.FilePath,12,Theme.Muted));p.Children.Add(Action("Utiliser le son intégré",()=>{S.Sound.FilePath="";Save();ShowPage(1);}));
        p=Card("Retrouver le pointeur");Check(p,"Localiser en secouant la souris",S.Locate.Enabled,v=>S.Locate.Enabled=v);Range(p,"Sensibilité de détection",S.Locate.Sensitivity,.1,1,.05,v=>S.Locate.Sensitivity=v);
    }
    internal static readonly string[] HoldKeys={"None","LeftControl","RightControl","LeftAlt","RightAlt","LeftShift","RightShift","LeftWindows","RightWindows"};
    private void Annotations()
    {
        var p=Card("Dessiner sur l’écran", "F : crayon · A : flèche · R : rectangle · C : cercle · L : ligne · H : surligneur · T : texte · N : numéro. Échap pour terminer.");Check(p,"Activer les annotations",S.Annotation.Enabled,v=>S.Annotation.Enabled=v);Choice(p,"Outil",S.Annotation.Tool,v=>S.Annotation.Tool=v);Options(p,"Dessiner en maintenant",S.Annotation.HoldKey,HoldKeys,v=>S.Annotation.HoldKey=v);Color(p,"Couleur du dessin",S.Annotation.Color,v=>S.Annotation.Color=v);Range(p,"Épaisseur du trait",S.Annotation.Width,1,30,1,v=>S.Annotation.Width=v," px");Color(p,"Couleur du surligneur",S.Annotation.HighlighterColor,v=>S.Annotation.HighlighterColor=v);Range(p,"Largeur du surligneur",S.Annotation.HighlighterWidth,5,100,1,v=>S.Annotation.HighlighterWidth=v," px");Range(p,"Taille des repères numérotés",S.Annotation.CalloutSize,12,100,1,v=>S.Annotation.CalloutSize=v," px");Check(p,"Aligner les surlignages presque horizontaux",S.Annotation.HighlighterSnap,v=>S.Annotation.HighlighterSnap=v);Input(p,"Texte à placer",S.Annotation.Text,v=>S.Annotation.Text=v);Range(p,"Taille du texte",S.Annotation.FontSize,12,120,2,v=>S.Annotation.FontSize=v," pt");Range(p,"Effacer après (0 = conserver)",S.Annotation.FadeSeconds,0,30,.5,v=>S.Annotation.FadeSeconds=v," s");Check(p,"Effacer en quittant le mode dessin",S.Annotation.ClearOnRelease,v=>S.Annotation.ClearOnRelease=v);var a=new WrapPanel();a.Children.Add(Action("Dessiner / terminer",controller.ToggleDrawing));a.Children.Add(Action("Annuler le dernier trait",controller.UndoDrawing));a.Children.Add(Action("Tout effacer",controller.ClearDrawing));p.Children.Add(a);
    }
    private void Keys(){var p=Card("Touches à l’écran", "Les frappes peuvent révéler du texte privé : activez ce mode pour les démonstrations prévues.");Check(p,"Afficher les touches",S.Keystrokes.Enabled,v=>S.Keystrokes.Enabled=v);Choice(p,"Apparence",S.Keystrokes.Style,v=>S.Keystrokes.Style=v);Range(p,"Taille des touches",S.Keystrokes.FontSize,16,120,1,v=>S.Keystrokes.FontSize=v," pt");Range(p,"Durée d’affichage",S.Keystrokes.DurationMilliseconds,1000,5000,100,v=>S.Keystrokes.DurationMilliseconds=(int)v," ms");Check(p,"Afficher Espace",S.Keystrokes.ShowSpace,v=>S.Keystrokes.ShowSpace=v);Check(p,"Afficher Supprimer",S.Keystrokes.ShowDelete,v=>S.Keystrokes.ShowDelete=v);Check(p,"Afficher Entrée",S.Keystrokes.ShowReturn,v=>S.Keystrokes.ShowReturn=v);Color(p,"Accent des touches",S.Keystrokes.AccentColor,v=>S.Keystrokes.AccentColor=v);Check(p,"Afficher uniquement les raccourcis avec modificateur",S.Keystrokes.ModifiersOnly,v=>S.Keystrokes.ModifiersOnly=v);}
    private void Zoom(){var p=Card("Zoom en direct", "Activez le zoom pour armer son raccourci. Le bouton ci-dessous lance ou quitte le grossissement. Échap quitte le zoom.");Check(p,"Armer le zoom en direct",S.Zoom.Enabled,v=>S.Zoom.Enabled=v);Range(p,"Grossissement",S.Zoom.Scale,1.1,5,.1,v=>S.Zoom.Scale=v," ×");Range(p,"Durée de transition",S.Zoom.AnimationMilliseconds,0,1500,25,v=>S.Zoom.AnimationMilliseconds=(int)v," ms");Range(p,"Vitesse du déplacement zoomé",S.Zoom.MovementSpeed,.05,1,.05,v=>S.Zoom.MovementSpeed=v);Range(p,"Grossissement automatique",S.Zoom.AutoScale,1.1,5,.1,v=>S.Zoom.AutoScale=v," ×");p.Children.Add(Action("Zoomer / revenir",()=>controller.Toggle("zoom")));Check(p,"Zoom automatique au clic",S.Zoom.AutoOnClick,v=>S.Zoom.AutoOnClick=v);Range(p,"Maintien après le dernier clic",S.Zoom.AutoDurationMilliseconds,200,5000,100,v=>S.Zoom.AutoDurationMilliseconds=(int)v," ms");p=Card("Loupe", "Grossissement local qui suit le curseur sans bloquer les clics.");Check(p,"Activer la loupe",S.Zoom.LoupeEnabled,v=>S.Zoom.LoupeEnabled=v);Range(p,"Diamètre de la loupe",S.Zoom.LoupeDiameter,80,600,10,v=>S.Zoom.LoupeDiameter=v," px");Range(p,"Grossissement de la loupe",S.Zoom.LoupeScale,1.1,5,.1,v=>S.Zoom.LoupeScale=v," ×");p=Card("Focus de fenêtre", "Assombrit les zones extérieures à la fenêtre active.");Check(p,"Activer le focus",S.Focus.Enabled,v=>S.Focus.Enabled=v);Range(p,"Assombrissement extérieur",S.Focus.Opacity,0,.95,.05,v=>S.Focus.Opacity=v);}
    private void Recording()
    {
        var p=Card("Capture vidéo", "L’écran sous le pointeur est capturé avec vos effets. Les commandes flottantes et le décompte sont exclus de la vidéo.");p.Children.Add(AsyncAction(controller.IsRecording?"Arrêter et sauvegarder":"Démarrer la capture",controller.ToggleRecordingAsync));Choice(p,"Résolution MP4",S.Recording.Resolution,v=>S.Recording.Resolution=v);Choice(p,"Cadrage",S.Recording.FillMode,v=>S.Recording.FillMode=v);Options(p,"Images par seconde",S.Recording.FramesPerSecond.ToString(),new[]{"30","60"},v=>S.Recording.FramesPerSecond=int.Parse(v));Range(p,"Décompte",S.Recording.CountdownSeconds,0,10,1,v=>S.Recording.CountdownSeconds=(int)v," s");
        p=Card("Audio", "Le son du système et le micro sont conservés sur des pistes séparées. Un casque évite que le micro reprenne vos haut-parleurs.");Check(p,"Enregistrer le son du système",S.Recording.SystemAudio,v=>S.Recording.SystemAudio=v);Check(p,"Enregistrer le microphone",S.Recording.Microphone,v=>S.Recording.Microphone=v);var mic=new ComboBox{Margin=new Thickness(0,5,0,10),DisplayMemberPath="Name",SelectedValuePath="Id"};try{var devices=new List<MediaDevice>{new("","Microphone par défaut")};devices.AddRange(DeviceEnumeration.GetMicrophones());mic.ItemsSource=devices;mic.SelectedValue=S.Recording.MicrophoneDeviceId;}catch(Exception ex){p.Children.Add(Theme.Text(ex.Message,12,Theme.Muted));}AutomationProperties.SetName(mic,"Microphone d’enregistrement");mic.SelectionChanged+=(_,_)=>{if(mic.SelectedValue is string id){S.Recording.MicrophoneDeviceId=id;Save();}};p.Children.Add(mic);
        p=Card("Fichiers et préparation");p.Children.Add(Theme.Text(S.Recording.OutputDirectory,13,Theme.Muted));p.Children.Add(Action("Choisir le dossier…",()=>{var d=new OpenFolderDialog{Title="Dossier des enregistrements",InitialDirectory=Directory.Exists(S.Recording.OutputDirectory)?S.Recording.OutputDirectory:Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)};if(d.ShowDialog(this)==true){S.Recording.OutputDirectory=d.FolderName;Save();ShowPage(5);}}));p.Children.Add(Action("Ouvrir le dossier",controller.OpenOutputFolder));p.Children.Add(AsyncAction("Préparer les outils vidéo locaux",controller.EnsureToolsAsync));p.Children.Add(Theme.Text("À l’arrêt, Jerry finalise le fichier capturé. Les erreurs et interruptions sont indiquées dans l’état de l’application.",12,Theme.Muted));
    }
    private void Camera()
    {
        var p=Card("Votre caméra à l’écran", "La bulle apparaît dans les captures. Elle reste désactivée au lancement jusqu’à votre choix.");AsyncCheck(p,"Activer la caméra",S.Camera.Enabled,controller.SetCameraEnabledAsync);
        var devices=new ComboBox{DisplayMemberPath="Name",SelectedValuePath="Id",Margin=new Thickness(0,5,0,10)};AutomationProperties.SetName(devices,"Caméra");p.Children.Add(devices);
        var refresh=AsyncAction("Actualiser les caméras",async()=>{devices.ItemsSource=await DeviceEnumeration.GetCamerasAsync();devices.SelectedValue=S.Camera.DeviceId;});p.Children.Add(refresh);
        devices.SelectionChanged+=async(_,_)=>{if(devices.SelectedValue is string id){if(id==S.Camera.DeviceId)return;var was=S.Camera.Enabled;if(was)await Run(()=>controller.SetCameraEnabledAsync(false));S.Camera.DeviceId=id;Save();if(was)await Run(()=>controller.SetCameraEnabledAsync(true));}};
        devices.Loaded+=async(_,_)=>await Run(async()=>{devices.ItemsSource=await DeviceEnumeration.GetCamerasAsync();devices.SelectedValue=S.Camera.DeviceId;});
        Choice(p,"Bordure de la caméra",S.Camera.Border,v=>S.Camera.Border=v);Check(p,"Aimantation aux coins",S.Camera.SnapToCorners,v=>S.Camera.SnapToCorners=v);Choice(p,"Forme de la bulle",S.Camera.Shape,v=>S.Camera.Shape=v);Range(p,"Taille de la caméra",S.Camera.Size,80,500,10,v=>S.Camera.Size=v," px");Check(p,"Image miroir",S.Camera.Mirror,v=>S.Camera.Mirror=v);Range(p,"Position horizontale",S.Camera.X,0,1,.01,v=>S.Camera.X=v);Range(p,"Position verticale",S.Camera.Y,0,1,.01,v=>S.Camera.Y=v);
    }
    private void Captions()
    {
        var p=Card("Transcrire sur cet ordinateur", "Le premier usage nécessite le téléchargement d’un moteur et d’un modèle. Les vidéos et la voix sont traitées localement.");Options(p,"Langue",S.Captions.Language,new[]{"fr","en","de","es","it","pt","nl","auto"},v=>S.Captions.Language=v);Options(p,"Modèle de reconnaissance",S.Captions.Model,new[]{"tiny","base"},v=>S.Captions.Model=v);p.Children.Add(AsyncAction("Préparer le modèle vocal",controller.EnsureCaptionModelAsync));AsyncCheck(p,"Activer les sous-titres en direct",S.Captions.LiveEnabled,controller.SetLiveCaptionsAsync);Check(p,"Animer l’apparition et la disparition des sous-titres",S.Captions.SmoothAnimation,v=>S.Captions.SmoothAnimation=v);Check(p,"Ouvrir les sous-titres après chaque enregistrement",S.Captions.AutoOpenAfterRecording,v=>S.Captions.AutoOpenAfterRecording=v);
        p=Card("Style et disposition");Choice(p,"Style des sous-titres",S.Captions.Style,v=>S.Captions.Style=v);Options(p,"Police",S.Captions.FontFamily,Jerry.App.Captions.CaptionFonts.Choices().ToArray(),v=>S.Captions.FontFamily=v);Range(p,"Taille des sous-titres",S.Captions.FontSize,16,120,2,v=>S.Captions.FontSize=v," pt");Color(p,"Couleur du texte",S.Captions.Color,v=>S.Captions.Color=v);Color(p,"Couleur animée",S.Captions.AccentColor,v=>S.Captions.AccentColor=v);Check(p,"Afficher un fond",S.Captions.Background,v=>S.Captions.Background=v);Range(p,"Mots par sous-titre",S.Captions.WordsPerCaption,1,15,1,v=>S.Captions.WordsPerCaption=(int)v);Range(p,"Nombre de lignes",S.Captions.Lines,1,3,1,v=>S.Captions.Lines=(int)v);Range(p,"Placement horizontal",S.Captions.X,0,1,.01,v=>S.Captions.X=v);Range(p,"Placement vertical",S.Captions.Y,0,1,.01,v=>S.Captions.Y=v);Range(p,"Maintien après le silence",S.Captions.LingerSeconds,.5,15,.5,v=>S.Captions.LingerSeconds=v," s");
        Range(p,"Durée du fondu de sortie",S.Captions.FadeMilliseconds,0,3000,50,v=>S.Captions.FadeMilliseconds=(int)v," ms");p=Card("Sous-titrer une vidéo", "Importez un fichier, corrigez le texte et le minutage, puis exportez un MP4 incrusté ou un fichier SRT.");p.Children.Add(Action("Ouvrir l’éditeur de sous-titres…",()=>controller.ShowCaptionEditor()));if(controller.LastRecording is string file)p.Children.Add(Action("Sous-titrer la dernière prise",()=>controller.ShowCaptionEditor(file)));
    }
    private void Hotkeys(){var p=Card("Raccourcis globaux", "Saisissez une combinaison telle que Ctrl+Alt+R, puis quittez le champ pour l’appliquer. Un champ vide désactive le raccourci. Les conflits d’enregistrement apparaissent dans la barre d’état.");foreach(var key in S.Hotkeys.Keys.ToArray()){var captured=key;Input(p,HotkeyLabel(key),S.Hotkeys[key],v=>S.Hotkeys[captured]=v);}p.Children.Add(Action("Effacer tous les raccourcis",()=>{foreach(var key in S.Hotkeys.Keys.ToArray())S.Hotkeys[key]="";Save();ShowPage(8);}));}
    private void Preferences(){var p=Card("Au quotidien");Check(p,"Démarrer Jerry à l’ouverture de session",S.LaunchAtLogin,v=>S.LaunchAtLogin=v);Check(p,"Fermer vers la zone de notification",S.MinimizeToTray,v=>S.MinimizeToTray=v);p.Children.Add(Theme.Text("Les réglages sont enregistrés automatiquement. Pour arrêter tous les services et quitter, utilisez Quitter Jerry.",13,Theme.Muted));p.Children.Add(Action("Quitter Jerry",controller.Quit));Range(p,"Fréquence de rendu",S.UpdateRate,15,120,15,v=>S.UpdateRate=(int)v," Hz");p=Card("Menu de la zone de notification");foreach(var key in S.TrayItems.Keys.ToArray()){var item=key;Check(p,HotkeyLabel(item),S.TrayItems[item],v=>S.TrayItems[item]=v);}p.Children.Add(Action("Réinitialiser tous les réglages…",()=>{if(MessageBox.Show(this,"Restaurer les réglages de Jerry et effacer les raccourcis personnalisés ?","Réinitialiser Jerry",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes){controller.ResetSettings();ShowPage(9);}}));p=Card("Stockage local", "La capture, l’audio et la transcription restent sur cet ordinateur.");p.Children.Add(Theme.Text("Les prises sont enregistrées dans le dossier choisi. Les modèles de transcription restent disponibles hors ligne après leur premier téléchargement.",13,Theme.Muted));}
    private static string OptionLabel(string value)=>value switch {"None"=>"Aucune","LeftControl"=>"Contrôle gauche","RightControl"=>"Contrôle droit","LeftAlt"=>"Alt gauche","RightAlt"=>"Alt droit (AltGr)","LeftShift"=>"Majuscule gauche","RightShift"=>"Majuscule droite","LeftWindows"=>"Windows gauche","RightWindows"=>"Windows droit","fr"=>"Français","en"=>"Anglais","de"=>"Allemand","es"=>"Espagnol","it"=>"Italien","pt"=>"Portugais","nl"=>"Néerlandais","auto"=>"Détection automatique","tiny"=>"Léger · tiny","base"=>"Équilibré · base",_=>value};
    private static string HotkeyLabel(string key)=>key switch{"sound"=>"Sons de clic","keystrokes"=>"Touches à l’écran","locate"=>"Localiser le pointeur","flash"=>"Déclencher un effet de clic","zoom-in"=>"Augmenter le zoom","zoom-out"=>"Réduire le zoom","camera"=>"Caméra","master"=>"Activer / couper les effets","spotlight"=>"Spotlight","ring"=>"Anneau","trail"=>"Traînée","click"=>"Effets de clic","annotation"=>"Dessiner / terminer","clear"=>"Effacer les annotations","zoom"=>"Zoomer / revenir","loupe"=>"Loupe","focus"=>"Focus de fenêtre","record"=>"Enregistrer / arrêter","captions"=>"Sous-titres en direct",_=>key};
    private static string Label(string name)=>name switch{"None"=>"Aucune","White"=>"Blanche","Gradient"=>"Dégradée","Circle"=>"Cercle","HorizontalBar"=>"Barre horizontale","VerticalBar"=>"Barre verticale","Particles"=>"Particules","Glow"=>"Lueur","SolidLine"=>"Ligne continue","Dots"=>"Points","DashedLine"=>"Pointillés","Stars"=>"Étoiles","Kawaii"=>"Kawaii","SingleRing"=>"Anneau simple","RippleWave"=>"Triple onde","RingBurst"=>"Éclat d’anneaux","ColorFlash"=>"Flash coloré","ParticleBurst"=>"Éclat de particules","GlowPulse"=>"Pulsation lumineuse","ScaleBounce"=>"Rebond","Freehand"=>"Crayon","Arrow"=>"Flèche","Rectangle"=>"Rectangle","Line"=>"Ligne","Highlighter"=>"Surligneur","Text"=>"Texte","Number"=>"Repère numéroté","Classic"=>"Classique","Glass"=>"Verre","Keycaps"=>"Touches","Neon"=>"Néon","MouzzGradient"=>"Dégradé","Minimal"=>"Minimal","CleanBar"=>"Bandeau classique","BoldOutline"=>"Contour marqué","WordFill"=>"Remplissage par mot","WordPop"=>"Rebond par mot","SlideUp"=>"Entrée vers le haut","Native"=>"Résolution de l’écran","P720"=>"720p · HD","P1080"=>"1080p · Full HD","P2160"=>"2160p · 4K","Fit"=>"Ajuster · image entière","Crop"=>"Recadrer · remplir sans déformer","Stretch"=>"Étirer · remplir en déformant","Card"=>"Carte 16:9",_=>name};
}
