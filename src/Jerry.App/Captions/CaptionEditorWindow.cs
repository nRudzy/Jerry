using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Documents;
using System.Windows.Threading;
using System.Threading;
using Jerry.App.Media;
using Jerry.Core.Captions;
using Jerry.Core.Settings;
using Microsoft.Win32;

namespace Jerry.App.Captions;

public sealed class CaptionEditorWindow : Window
{
    private readonly AppSettings _settings;
    private readonly CaptionService _captions;
    private CaptionDocument _document = new();
    private readonly ObservableCollection<CaptionRow> _rows = [];
    private readonly DataGrid _grid = new();
    private readonly DataGrid _wordGrid = new();
    private readonly ObservableCollection<CaptionWordRow> _wordRows = [];
    private CaptionRow? _wordCueRow;
    private readonly MediaElement _video = new() { LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Close, Stretch = Stretch.Uniform };
    private readonly Grid _stage = new() { Width = 1920, Height = 1080, Background = Brushes.Black };
    private readonly Canvas _canvas = new() { IsHitTestVisible = false };
    private readonly Border _captionBox = new() { CornerRadius = new CornerRadius(12), Padding = new Thickness(16, 8, 16, 8) };
    private readonly TextBlock _captionText = new() { TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _pathLabel = new() { Text = "Importez une vidéo ou un fichier audio pour commencer.", TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _status = new() { Text = "Audio et vidéo restent sur cet ordinateur. Le premier usage télécharge un modèle local.", TextWrapping = TextWrapping.Wrap };
    private readonly ProgressBar _progress = new() { Minimum = 0, Maximum = 1, Height = 5, Margin = new Thickness(0, 6, 0, 0) };
    private readonly Slider _seek = new() { Minimum = 0, Maximum = 1, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 8, 0) };
    private readonly TextBlock _clock = new() { Text = "00:00:00.000", VerticalAlignment = VerticalAlignment.Center, MinWidth = 95 };
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly List<Control> _busyControls = [];
    private readonly Button _cancel = new() { Content = "Annuler", IsEnabled = false, Margin = new Thickness(4), Padding = new Thickness(12, 5, 12, 5) };
    private CancellationTokenSource? _operation;
    private bool _seeking;
    private bool _playing;
    private bool _closed;

    public CaptionEditorWindow(AppSettings settings, CaptionService captions, ToolchainService tools, string? initialPath = null)
    {
        _settings = settings;
        _captions = captions;
        Title = "Jerry · Studio de sous-titres locaux";
        Width = 1180; Height = 920; MinWidth = 900; MinHeight = 720;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(244, 246, 250));
        Foreground = new SolidColorBrush(Color.FromRgb(23, 32, 48));
        InstallLightResources();
        _cancel.Content = Label("Annuler");
        _settings.Captions.Lines = Math.Clamp(_settings.Captions.Lines, 1, 3);
        var root = new DockPanel { Margin = new Thickness(18) };
        Content = root;
        var heading = new StackPanel();
        DockPanel.SetDock(heading, Dock.Top);
        heading.Children.Add(new TextBlock { Text = "Sous-titres", FontSize = 27, FontWeight = FontWeights.SemiBold });
        heading.Children.Add(_pathLabel);
        var actions = new WrapPanel { Margin = new Thickness(0, 8, 0, 6) };
        actions.Children.Add(Button("Importer média…", ImportMedia));
        actions.Children.Add(Button("Transcrire", async () => await RunOperation(Transcribe)));
        actions.Children.Add(Button("Préparer le modèle", async () => await RunOperation(ct => _captions.EnsureModelAsync(Progress(), ct))));
        actions.Children.Add(Button("Ouvrir projet…", OpenProject));
        actions.Children.Add(Button("Enregistrer projet…", SaveProject));
        actions.Children.Add(Button("Exporter SRT…", ExportSrt));
        actions.Children.Add(Button("Exporter MP4…", async () => await ExportVideo()));
        _cancel.Click += (_, _) => _operation?.Cancel();
        actions.Children.Add(_cancel);
        heading.Children.Add(actions);
        root.Children.Add(heading);
        var statusPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        statusPanel.Children.Add(_status); statusPanel.Children.Add(_progress);
        DockPanel.SetDock(statusPanel, Dock.Bottom); root.Children.Add(statusPanel);
        var main = new Grid();
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 190 });
        main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 140 });
        main.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.Children.Add(main);
        _stage.Children.Add(_video);
        _captionBox.Child = _captionText;
        _canvas.Children.Add(_captionBox); _stage.Children.Add(_canvas);
        var preview = new Border { Background = Brushes.Black, CornerRadius = new CornerRadius(9), ClipToBounds = true, Child = new Viewbox { Stretch = Stretch.Uniform, Child = _stage } };
        main.Children.Add(preview);
        var transport = new DockPanel { Margin = new Thickness(0, 6, 0, 6) };
        Grid.SetRow(transport, 1); main.Children.Add(transport);
        transport.Children.Add(Button("Lecture / pause", TogglePlayback, false));
        DockPanel.SetDock(_clock, Dock.Right); transport.Children.Add(_clock); transport.Children.Add(_seek);
        _seek.PreviewMouseLeftButtonDown += (_, _) => _seeking = true;
        _seek.PreviewMouseLeftButtonUp += (_, _) => { _video.Position = TimeSpan.FromSeconds(_seek.Value); _seeking = false; RenderPreview(); };
        _seek.ValueChanged += (_, _) => { if (_seeking) { _video.Position = TimeSpan.FromSeconds(_seek.Value); RenderPreview(); } };
        _video.MediaOpened += (_, _) =>
        {
            if (_video.NaturalVideoWidth > 0) { _stage.Width = _video.NaturalVideoWidth; _stage.Height = _video.NaturalVideoHeight; }
            if (_video.NaturalDuration.HasTimeSpan) _seek.Maximum = Math.Max(1, _video.NaturalDuration.TimeSpan.TotalSeconds);
            _video.Pause(); _playing = false; RenderPreview();
        };
        _video.MediaEnded += (_, _) => { _playing = false; _video.Pause(); };
        _video.MediaFailed += (_, args) => _status.Text = "Aperçu Windows indisponible pour ce codec. La transcription et l’export FFmpeg restent utilisables. " + args.ErrorException.Message;
        var options = BuildOptions(); Grid.SetRow(options, 2); main.Children.Add(options);
        ConfigureGrid(); ConfigureWordGrid();
        var transcriptPanel = new Grid();
        transcriptPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.7, GridUnitType.Star) });
        transcriptPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _grid.Margin = new Thickness(0, 0, 8, 0); transcriptPanel.Children.Add(_grid);
        var wordPanel = new DockPanel();
        var wordHeading = new TextBlock { Text = "Minutage mot par mot · sélectionnez un sous-titre", FontSize = 12, Margin = new Thickness(0, 0, 0, 5) };
        DockPanel.SetDock(wordHeading, Dock.Top); wordPanel.Children.Add(wordHeading); wordPanel.Children.Add(_wordGrid);
        Grid.SetColumn(wordPanel, 1); transcriptPanel.Children.Add(wordPanel);
        Grid.SetRow(transcriptPanel, 3); main.Children.Add(transcriptPanel);
        var edits = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
        edits.Children.Add(Button("Ajouter une ligne", AddCue));
        edits.Children.Add(Button("Supprimer la sélection", DeleteCue));
        edits.Children.Add(new TextBlock { Text = "Texte corrigé : mots recalés. Longueur changée : corrections réinitialisées.", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) });
        Grid.SetRow(edits, 4); main.Children.Add(edits);
        _captions.StatusChanged += OnStatus;
        _timer.Tick += (_, _) =>
        {
            if (!_seeking) _seek.Value = Math.Clamp(_video.Position.TotalSeconds, 0, _seek.Maximum);
            _clock.Text = TimeText(_video.Position);
            RenderPreview();
        };
        _timer.Start();
        KeyDown += (_, args) =>
        {
            if (Keyboard.Modifiers != ModifierKeys.Control || _operation != null) return;
            if (args.Key == Key.O) { ImportMedia(); args.Handled = true; }
            if (args.Key == Key.S) { SaveProject(); args.Handled = true; }
        };
        Closed += (_, _) =>
        {
            _closed = true; _operation?.Cancel(); _timer.Stop(); _video.Close();
            _captions.StatusChanged -= OnStatus;
        };
        if (!string.IsNullOrWhiteSpace(initialPath)) Loaded += (_, _) => LoadMedia(initialPath);
    }

    private void InstallLightResources()
    {
        FontFamily = new FontFamily("Segoe UI");
        foreach (var type in new[] { typeof(TextBlock), typeof(Button), typeof(TextBox), typeof(CheckBox), typeof(ComboBox) })
        {
            var style = new Style(type);
            style.Setters.Add(new Setter(type == typeof(TextBlock) ? TextBlock.ForegroundProperty : Control.ForegroundProperty, Foreground));
            if (type == typeof(Button) || type == typeof(TextBox) || type == typeof(ComboBox))
            {
                style.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.White));
                style.Setters.Add(new Setter(Control.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(190, 199, 213))));
            }
            Resources[type] = style;
        }
    }

    private TextBlock Label(string text) => new() { Text = text, Foreground = Foreground };

    private Button Button(string title, Action action, bool busy = true)
    {
        var button = new Button { Content = Label(title), Margin = new Thickness(3), Padding = new Thickness(10, 5, 10, 5) };
        button.Click += (_, _) => { if (_operation == null || !busy) { try { action(); } catch (Exception error) { ShowError(error); } } };
        if (busy) _busyControls.Add(button);
        return button;
    }

    private FrameworkElement BuildOptions()
    {
        var panel = new WrapPanel { Margin = new Thickness(0, 6, 0, 8) };
        void Add(string label, FrameworkElement control)
        {
            var field = new StackPanel { Margin = new Thickness(0, 0, 12, 7) };
            field.Children.Add(new TextBlock { Text = label, FontSize = 11, Margin = new Thickness(0, 0, 0, 3) });
            field.Children.Add(control); panel.Children.Add(field);
            if (control is Control input) _busyControls.Add(input);
        }
        ComboBox Combo(IEnumerable<string> values, string selected, Action<string> change, double width)
        {
            var item = new FrameworkElementFactory(typeof(TextBlock));
            item.SetBinding(TextBlock.TextProperty, new Binding("."));
            item.SetValue(TextBlock.ForegroundProperty, Foreground);
            var combo = new ComboBox { ItemsSource = values, SelectedItem = selected, Width = width, ItemTemplate = new DataTemplate { VisualTree = item } };
            combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is string value) { change(value); RenderPreview(); } };
            return combo;
        }
        TextBox Text(string value, Action<string> change, double width)
        {
            var box = new TextBox { Text = value, Width = width, Padding = new Thickness(4) };
            box.LostFocus += (_, _) => { change(box.Text); RenderPreview(); };
            return box;
        }
        Slider Slide(double min, double max, double value, double step, Action<double> change, bool percent = false)
        {
            var slider = new Slider { Minimum = min, Maximum = max, Value = Math.Clamp(value, min, max), TickFrequency = step, IsSnapToTickEnabled = true, Width = 105, ToolTip = percent ? (value * 100).ToString("0", CultureInfo.CurrentCulture) + " %" : value.ToString("0.##", CultureInfo.CurrentCulture) };
            slider.ValueChanged += (_, _) => { change(slider.Value); slider.ToolTip = percent ? (slider.Value * 100).ToString("0", CultureInfo.CurrentCulture) + " %" : slider.Value.ToString("0.##", CultureInfo.CurrentCulture); RenderPreview(); };
            return slider;
        }
        var options = _settings.Captions;
        Add("Style vidéo", Combo(Enum.GetNames<CaptionStyle>(), options.Style.ToString(), value => options.Style = Enum.Parse<CaptionStyle>(value), 135));
        var fontPicker = Combo(CaptionFonts.Choices(), options.FontFamily, value => options.FontFamily = value, 160);
        fontPicker.IsEditable = true; fontPicker.IsTextSearchEnabled = true; fontPicker.Text = options.FontFamily;
        fontPicker.LostFocus += (_, _) => { if (!string.IsNullOrWhiteSpace(fontPicker.Text)) { options.FontFamily = fontPicker.Text.Trim(); RenderPreview(); } };
        Add("Police", fontPicker);
        Add("Taille", Slide(14, 120, options.FontSize, 1, value => options.FontSize = value));
        Add("Position X (%)", Slide(.05, .95, options.X, .01, value => options.X = value, true));
        Add("Position Y (%)", Slide(.05, .95, options.Y, .01, value => options.Y = value, true));
        Add("Couleur", Text(options.Color, value => { if (ValidColor(value)) options.Color = value; }, 85));
        Add("Accent", Text(options.AccentColor, value => { if (ValidColor(value)) options.AccentColor = value; }, 85));
        Add("Lignes", Combo(["1", "2", "3"], options.Lines.ToString(CultureInfo.InvariantCulture), value => options.Lines = int.Parse(value, CultureInfo.InvariantCulture), 50));
        Add("Mots / sous-titre", Combo(Enumerable.Range(1, 30).Select(number => number.ToString(CultureInfo.InvariantCulture)), options.WordsPerCaption.ToString(CultureInfo.InvariantCulture), RegroupCaptions, 65));
        var background = new CheckBox { Content = Label("Fond"), IsChecked = options.Background, VerticalAlignment = VerticalAlignment.Center };
        background.Checked += (_, _) => { options.Background = true; RenderPreview(); };
        background.Unchecked += (_, _) => { options.Background = false; RenderPreview(); };
        Add("Lisibilité", background);
        Add("Langue¹", Combo(["auto", "fr", "en", "es", "de", "it", "pt", "nl", "ja", "zh"], options.Language, value => options.Language = value, 65));
        Add("Modèle¹", Combo(["base", "tiny"], options.Model, value => options.Model = value, 75));
        panel.ToolTip = "Changer les mots par sous-titre rétablit le texte et les temps de la transcription d’origine, en annulant les corrections. ¹ Langue et modèle sont appliqués à la prochaine transcription. Base est plus précis ; tiny demande moins de ressources. La prévisualisation est indicative ; le MP4 utilise le rendu ASS de FFmpeg.";
        return panel;
    }

    private void RegroupCaptions(string value)
    {
        try
        {
            CommitEdits();
            var count = int.Parse(value, CultureInfo.InvariantCulture);
            _settings.Captions.WordsPerCaption = count;
            if (_document.Cues.Count == 0) return;
            CaptionTiming.RegroupFromOriginal(_document, count);
            SetDocument(_document);
            _status.Text = "Regroupement effectué : le texte et le minutage d’origine ont été rétablis. Les corrections ont été réinitialisées.";
        }
        catch (Exception error) { ShowError(error); }
    }

    private bool ValidColor(string value)
    {
        if (value.Length == 7 && value[0] == '#' && value[1..].All(Uri.IsHexDigit)) return true;
        _status.Text = "Couleur invalide : utilisez #RRGGBB, par exemple #7DE5C3.";
        return false;
    }

    private void ConfigureGrid()
    {
        _grid.ItemsSource = _rows; _grid.AutoGenerateColumns = false; _grid.CanUserAddRows = false;
        _grid.CanUserDeleteRows = false; _grid.SelectionMode = DataGridSelectionMode.Single;
        _grid.HeadersVisibility = DataGridHeadersVisibility.Column; _grid.RowHeaderWidth = 0;
        _grid.Columns.Add(new DataGridTextColumn { Header = Label("Début"), Binding = new Binding(nameof(CaptionRow.StartText)) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus, ValidatesOnDataErrors = true }, Width = 120 });
        _grid.Columns.Add(new DataGridTextColumn { Header = Label("Fin"), Binding = new Binding(nameof(CaptionRow.EndText)) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus, ValidatesOnDataErrors = true }, Width = 120 });
        _grid.Columns.Add(new DataGridTextColumn { Header = Label("Texte (double-clic pour modifier)"), Binding = new Binding(nameof(CaptionRow.Text)) { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = Label("Mots"), Binding = new Binding(nameof(CaptionRow.TimingStatus)), IsReadOnly = true, Width = 100 });
        _grid.SelectionChanged += (_, _) =>
        {
            try { CommitWordEdits(); }
            catch (Exception error) { _status.Text = error.Message; return; }
            if (_grid.SelectedItem is CaptionRow selected) ShowWords(selected);
            else { _wordRows.Clear(); _wordCueRow = null; }
            if (_grid.SelectedItem is CaptionRow row && !_playing)
            {
                _video.Position = row.Cue.Start;
                _seek.Value = Math.Clamp(row.Cue.Start.TotalSeconds, 0, _seek.Maximum);
                RenderPreview();
            }
        };
        _grid.RowEditEnding += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            if (_closed) return;
            foreach (var row in _rows)
                if (row.Error.Length == 0) row.Apply();
            if (_grid.SelectedItem is CaptionRow selected && !_wordRows.Any(word => word.IsDirty)) ShowWords(selected);
            RenderPreview();
        });
        _busyControls.Add(_grid);
    }

    private void ConfigureWordGrid()
    {
        _wordGrid.ItemsSource = _wordRows; _wordGrid.AutoGenerateColumns = false;
        _wordGrid.CanUserAddRows = false; _wordGrid.CanUserDeleteRows = false;
        _wordGrid.HeadersVisibility = DataGridHeadersVisibility.Column;
        _wordGrid.Columns.Add(new DataGridTextColumn { Header = Label("Mot"), Binding = new Binding(nameof(CaptionWordRow.Text)) { ValidatesOnDataErrors = true }, Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _wordGrid.Columns.Add(new DataGridTextColumn { Header = Label("Début"), Binding = new Binding(nameof(CaptionWordRow.StartText)) { ValidatesOnDataErrors = true }, Width = 112 });
        _wordGrid.Columns.Add(new DataGridTextColumn { Header = Label("Fin"), Binding = new Binding(nameof(CaptionWordRow.EndText)) { ValidatesOnDataErrors = true }, Width = 112 });
        _wordGrid.RowEditEnding += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            if (_closed) return;
            try { CommitWordEdits(); RenderPreview(); }
            catch (Exception error) { _status.Text = error.Message; }
        });
        _busyControls.Add(_wordGrid);
    }

    private void ShowWords(CaptionRow row)
    {
        _wordCueRow = row; _wordRows.Clear();
        foreach (var word in CaptionTiming.GetWords(row.Cue)) _wordRows.Add(new CaptionWordRow(word));
    }

    private void CommitWordEdits()
    {
        _wordGrid.CommitEdit(DataGridEditingUnit.Cell, true);
        _wordGrid.CommitEdit(DataGridEditingUnit.Row, true);
        if (_wordCueRow == null || !_wordRows.Any(word => word.IsDirty)) return;
        foreach (var row in _wordRows)
            if (row.Error.Length > 0) throw new ArgumentException(row.Error);
        CaptionTiming.UpdateWords(_wordCueRow.Cue, _wordRows.Select(word => word.ToWord()));
        _wordCueRow.Refresh();
        foreach (var row in _wordRows) row.Accept();
    }

    private void ImportMedia()
    {
        var dialog = new OpenFileDialog { Title = "Importer une vidéo ou un fichier audio", Filter = "Médias|*.mp4;*.mov;*.mkv;*.webm;*.avi;*.wav;*.mp3;*.m4a;*.flac;*.ogg|Tous les fichiers|*.*" };
        if (dialog.ShowDialog(this) == true) LoadMedia(dialog.FileName);
    }

    private void LoadMedia(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Le média n’est plus disponible. Réimportez son fichier.", path);
        _video.Close(); _playing = false;
        _document = new CaptionDocument { MediaPath = Path.GetFullPath(path) }; _rows.Clear();
        _pathLabel.Text = _document.MediaPath;
        _video.Source = new Uri(_document.MediaPath); _video.Play(); _video.Pause();
        _status.Text = "Média importé. Choisissez la langue et le modèle, puis cliquez sur Transcrire.";
        RenderPreview();
    }

    private async Task Transcribe(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_document.MediaPath)) throw new ArgumentException("Importez d’abord un média.");
        _video.Pause(); _playing = false;
        var document = await _captions.TranscribeAsync(_document.MediaPath, Progress(), cancellationToken);
        SetDocument(document);
        _status.Text = $"{document.Cues.Count} sous-titres. Vérifiez le texte et le minutage avant l’export. Les horodatages Whisper restent des estimations.";
    }

    private void SetDocument(CaptionDocument document)
    {
        _wordCueRow = null; _wordRows.Clear();
        _document = document; _rows.Clear();
        foreach (var cue in document.Cues) _rows.Add(new CaptionRow(cue));
        _pathLabel.Text = document.MediaPath;
        if (_rows.Count > 0) _grid.SelectedIndex = 0;
        _seek.Maximum = Math.Max(_seek.Maximum, document.Cues.Select(c => c.End.TotalSeconds).DefaultIfEmpty(1).Max());
        RenderPreview();
    }

    private void CommitEdits()
    {
        CommitWordEdits();
        _grid.CommitEdit(DataGridEditingUnit.Cell, true);
        _grid.CommitEdit(DataGridEditingUnit.Row, true);
        foreach (var row in _rows)
        {
            if (row.Error.Length > 0) throw new ArgumentException(row.Error);
            row.Apply();
        }
        _document.Cues = _rows.Select(row => row.Cue).OrderBy(cue => cue.Start).ToList();
    }

    private void SaveProject()
    {
        CommitEdits();
        var dialog = new SaveFileDialog { Title = "Enregistrer le projet modifiable", Filter = "Projet Jerry|*.jerrycaptions.json", DefaultExt = ".jerrycaptions.json", FileName = MediaName() + ".jerrycaptions.json" };
        if (dialog.ShowDialog(this) != true) return;
        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(_document, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(false));
        _status.Text = "Projet enregistré : " + dialog.FileName;
    }

    private void OpenProject()
    {
        var dialog = new OpenFileDialog { Title = "Ouvrir un projet de sous-titres", Filter = "Projet Jerry|*.jerrycaptions.json;*.json" };
        if (dialog.ShowDialog(this) != true) return;
        var document = JsonSerializer.Deserialize<CaptionDocument>(File.ReadAllText(dialog.FileName)) ?? throw new InvalidDataException("Projet vide ou non reconnu.");
        _ = CaptionExporter.ToSrt(document);
        if (!Path.IsPathRooted(document.MediaPath)) document.MediaPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(dialog.FileName)!, document.MediaPath));
        if (File.Exists(document.MediaPath)) LoadMedia(document.MediaPath);
        else { _video.Close(); _video.Source = null; _status.Text = "Le média original est introuvable ; l’édition et l’export SRT restent disponibles."; }
        SetDocument(document);
    }

    private void ExportSrt()
    {
        CommitEdits();
        if (_document.Cues.Count == 0) throw new ArgumentException("Transcrivez le média ou ajoutez des lignes avant l’export.");
        var dialog = new SaveFileDialog { Title = "Exporter les sous-titres SRT", Filter = "Sous-titres SRT|*.srt", FileName = MediaName() + ".srt" };
        if (dialog.ShowDialog(this) != true) return;
        File.WriteAllText(dialog.FileName, CaptionExporter.ToSrt(_document), new UTF8Encoding(true));
        _status.Text = "SRT exporté : " + dialog.FileName;
    }

    private async Task ExportVideo()
    {
        try
        {
            CommitEdits();
            if (_document.Cues.Count == 0) throw new ArgumentException("Transcrivez d’abord le média ou ajoutez des lignes.");
            var dialog = new SaveFileDialog { Title = "Exporter la vidéo avec sous-titres incrustés", Filter = "Vidéo MP4|*.mp4", FileName = MediaName() + "-sous-titres.mp4" };
            if (dialog.ShowDialog(this) != true) return;
            await RunOperation(ct => _captions.ExportVideoAsync(_document, dialog.FileName, Progress(), ct));
        }
        catch (Exception error) { ShowError(error); }
    }

    private void AddCue()
    {
        CommitEdits();
        var start = _video.Position;
        var cue = new CaptionCue();
        CaptionTiming.UpdateCue(cue, "Nouveau sous-titre", start, start + TimeSpan.FromSeconds(2));
        var row = new CaptionRow(cue); _rows.Add(row); _document.Cues.Add(cue); _grid.SelectedItem = row; _grid.ScrollIntoView(row);
    }

    private void DeleteCue()
    {
        if (_grid.SelectedItem is not CaptionRow row) return;
        _rows.Remove(row); _document.Cues.Remove(row.Cue); RenderPreview();
    }

    private void TogglePlayback()
    {
        if (_video.Source == null) return;
        if (_playing) _video.Pause(); else _video.Play();
        _playing = !_playing;
    }

    private void RenderPreview()
    {
        if (_closed) return;
        var time = _video.Position;
        var cue = _document.Cues.FirstOrDefault(c => c.Start <= time && c.End > time);
        _captionBox.Visibility = cue == null ? Visibility.Collapsed : Visibility.Visible;
        if (cue == null) return;
        var options = _settings.Captions;
        var words = CaptionTiming.GetWords(cue);
        _captionText.Inlines.Clear();
        try { _captionText.FontFamily = CaptionFonts.ResolveFamily(options.FontFamily); }
        catch (Exception error) { _status.Text = error.Message; _captionBox.Visibility = Visibility.Collapsed; return; }
        _captionText.FontSize = Math.Clamp(options.FontSize, 10, 200);
        _captionText.FontWeight = CaptionFonts.Weight(options.FontFamily);
        _captionText.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        _captionText.LineHeight = Math.Clamp(options.FontSize, 10, 200);
        _captionText.Foreground = Brush(options.Color, Brushes.White);
        _captionText.MaxWidth = _stage.Width * .9;
        var perLine = Math.Max(1, (int)Math.Ceiling(words.Count / (double)Math.Clamp(options.Lines, 1, 3)));
        for (var i = 0; i < words.Count; i++)
        {
            if (i > 0) _captionText.Inlines.Add(new Run(i % perLine == 0 ? "\n" : " "));
            var word = words[i];
            var run = new Run(word.Text);
            var active = word.Start <= time && (i == words.Count - 1 ? cue.End : words[i + 1].Start) > time;
            if (options.Style == CaptionStyle.WordFill && word.Start <= time || options.Style == CaptionStyle.WordPop && active)
                run.Foreground = Brush(options.AccentColor, Brushes.Aquamarine);
            if (options.Style == CaptionStyle.WordPop && active) run.FontSize = options.FontSize * 1.15;
            if (options.Style == CaptionStyle.SlideUp)
            {
                var entry = Math.Clamp((time - word.Start).TotalMilliseconds / 220, 0, 1);
                var opacity = Math.Min(Math.Clamp((time - word.Start).TotalMilliseconds / 180, 0, 1), Math.Clamp((cue.End - time).TotalMilliseconds / 120, 0, 1));
                var label = new TextBlock { Text = word.Text, FontFamily = _captionText.FontFamily, FontSize = _captionText.FontSize, FontWeight = _captionText.FontWeight, Foreground = _captionText.Foreground, Opacity = opacity, RenderTransform = new TranslateTransform(0, 40 * (1 - entry)) };
                _captionText.Inlines.Add(new InlineUIContainer(label) { BaselineAlignment = BaselineAlignment.Center });
            }
            else _captionText.Inlines.Add(run);
        }
        _captionBox.Background = options.Background && options.Style == CaptionStyle.CleanBar ? new SolidColorBrush(Color.FromArgb(170, 0, 0, 0)) : Brushes.Transparent;
        _captionText.Effect = options.Style == CaptionStyle.BoldOutline || options.Background && options.Style is CaptionStyle.WordFill or CaptionStyle.WordPop or CaptionStyle.SlideUp
            ? new DropShadowEffect { Color = Colors.Black, ShadowDepth = options.Style == CaptionStyle.BoldOutline ? 1 : 0, BlurRadius = options.Style == CaptionStyle.BoldOutline ? 6 : 3, Opacity = 1 } : null;
        _captionBox.Measure(new Size(_stage.Width * .95, _stage.Height));
        var x = _stage.Width * Math.Clamp(options.X, .03, .97) - _captionBox.DesiredSize.Width / 2;
        var y = _stage.Height * Math.Clamp(options.Y, .03, .97) - _captionBox.DesiredSize.Height / 2;
        _captionBox.Opacity = 1;
        Canvas.SetLeft(_captionBox, x); Canvas.SetTop(_captionBox, y);
    }

    private static Brush Brush(string color, Brush fallback)
    {
        try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)); }
        catch { return fallback; }
    }

    private async Task RunOperation(Func<CancellationToken, Task> operation)
    {
        if (_operation != null) return;
        using var cancellation = new CancellationTokenSource();
        _operation = cancellation;
        foreach (var control in _busyControls) control.IsEnabled = false;
        _cancel.IsEnabled = true; _progress.Value = 0;
        try { await operation(cancellation.Token); }
        catch (OperationCanceledException) { _status.Text = "Opération annulée. Les fichiers déjà produits restent récupérables dans le dossier local Jerry/work/captions."; }
        catch (Exception error) { ShowError(error); }
        finally
        {
            _operation = null;
            foreach (var control in _busyControls) control.IsEnabled = true;
            _cancel.IsEnabled = false;
        }
    }

    private IProgress<double> Progress() => new Progress<double>(value => { if (!_closed) _progress.Value = Math.Clamp(value, 0, 1); });
    private void OnStatus(string value) => Dispatcher.BeginInvoke(() => { if (!_closed) _status.Text = value; });
    private void ShowError(Exception error) { if (_closed) return; _status.Text = error.Message; MessageBox.Show(this, error.Message, "Jerry · Sous-titres", MessageBoxButton.OK, MessageBoxImage.Error); }
    private string MediaName() => string.IsNullOrEmpty(_document.MediaPath) ? "sous-titres" : Path.GetFileNameWithoutExtension(_document.MediaPath);
    internal static string TimeText(TimeSpan value) => $"{(long)value.TotalHours:00}:{value.Minutes:00}:{value.Seconds:00}.{value.Milliseconds:000}";
}

internal sealed class CaptionRow(CaptionCue cue) : INotifyPropertyChanged, IDataErrorInfo
{
    public CaptionCue Cue { get; } = cue;
    private string _start = CaptionEditorWindow.TimeText(cue.Start);
    private string _end = CaptionEditorWindow.TimeText(cue.End);
    private string _text = cue.Text;
    public event PropertyChangedEventHandler? PropertyChanged;
    public string StartText { get => _start; set { _start = value; Changed(nameof(StartText)); } }
    public string EndText { get => _end; set { _end = value; Changed(nameof(EndText)); } }
    public string Text { get => _text; set { _text = value; Changed(nameof(Text)); } }
    public string TimingStatus => Cue.WordTimingsEstimated ? "Estimés / édités" : Cue.WordTimingsEdited ? "Manuels" : "Whisper";
    public string Error => TryTime(StartText, out var start) && TryTime(EndText, out var end) && start >= TimeSpan.Zero && end > start
        ? "" : "Minutage invalide. Utilisez hh:mm:ss.fff avec une fin postérieure au début.";
    public string this[string columnName] => columnName is nameof(StartText) or nameof(EndText) ? Error : "";
    public void Apply()
    {
        if (Error.Length > 0) throw new ArgumentException(Error);
        TryTime(StartText, out var start); TryTime(EndText, out var end);
        CaptionTiming.UpdateCue(Cue, Text, start, end);
        Changed(nameof(TimingStatus));
    }
    public void Refresh()
    {
        _start = CaptionEditorWindow.TimeText(Cue.Start); _end = CaptionEditorWindow.TimeText(Cue.End); _text = Cue.Text;
        Changed(nameof(StartText)); Changed(nameof(EndText)); Changed(nameof(Text)); Changed(nameof(TimingStatus));
    }
    private void Changed(string property) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    internal static bool TryTime(string text, out TimeSpan value)
    {
        text = text.Trim().Replace(',', '.');
        if (TimeSpan.TryParseExact(text, [@"hh\:mm\:ss\.fff", @"h\:mm\:ss\.fff", @"hh\:mm\:ss", @"h\:mm\:ss", @"mm\:ss\.fff", @"mm\:ss"], CultureInfo.InvariantCulture, out value)) return true;
        if (double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var seconds) && double.IsFinite(seconds) && seconds >= 0 && seconds < TimeSpan.MaxValue.TotalSeconds)
        { value = TimeSpan.FromSeconds(seconds); return true; }
        value = default; return false;
    }
}

internal sealed class CaptionWordRow(CaptionWord word) : IDataErrorInfo
{
    private CaptionWord _original = word;
    public string Text { get; set; } = word.Text;
    public string StartText { get; set; } = CaptionEditorWindow.TimeText(word.Start);
    public string EndText { get; set; } = CaptionEditorWindow.TimeText(word.End);
    public bool IsDirty => Text != _original.Text || !CaptionRow.TryTime(StartText, out var start) || start != _original.Start || !CaptionRow.TryTime(EndText, out var end) || end != _original.End;
    public string Error => string.IsNullOrWhiteSpace(Text) || Text.Any(char.IsWhiteSpace)
        ? "Une cellule doit contenir un seul mot. Modifiez la ligne du sous-titre pour ajouter ou supprimer des mots."
        : CaptionRow.TryTime(StartText, out var start) && CaptionRow.TryTime(EndText, out var end) && start >= TimeSpan.Zero && end > start
            ? "" : "Le minutage du mot est invalide : utilisez hh:mm:ss.fff avec une fin après le début.";
    public string this[string columnName] => Error;
    public CaptionWord ToWord()
    {
        if (Error.Length > 0) throw new ArgumentException(Error);
        CaptionRow.TryTime(StartText, out var start); CaptionRow.TryTime(EndText, out var end);
        return new CaptionWord(Text, start, end);
    }
    public void Accept() => _original = ToWord();
}
