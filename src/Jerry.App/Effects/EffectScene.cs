using System.Windows;
using Jerry.Core.Settings;

namespace Jerry.App.Effects;

public sealed class EffectScene
{
    public AppSettings Settings { get; }
    public Point Cursor { get; private set; }
    public Point RingPosition { get; private set; }
    public Rect? FocusBounds { get; set; }
    private bool drawing;
    public bool Drawing
    {
        get => drawing;
        set { if (drawing == value) return; drawing = value; if (!value) { EndStroke(); if (Settings.Annotation.ClearOnRelease) Clear(); } }
    }
    internal readonly List<TrailSample> Trails = new();
    internal readonly List<ClickSample> Clicks = new();
    internal readonly List<AnnotationStroke> Strokes = new();
    internal readonly List<KeyLabel> Keys = new();
    internal AnnotationStroke? ActiveStroke;
    internal long Now => Environment.TickCount64;
    internal long LocateStarted { get; private set; } = long.MinValue / 2;
    internal Point LocatePoint { get; private set; }
    internal string Caption { get; private set; } = "";
    internal long CaptionUpdated { get; private set; }
    private long lastSpawn = long.MinValue / 2;
    private long lastTick;
    private bool hasCursor;
    private int nextNumber = 1;

    public EffectScene(AppSettings settings) { Settings = settings; lastTick = Now; }
    public void Move(double x, double y)
    {
        var next = new Point(x, y);
        if (!hasCursor) { RingPosition = next; hasCursor = true; }
        var old = Cursor; Cursor = next;
        if (Settings.Enabled && Settings.Trail.Enabled && (next - old).Length > .5 && Now - lastSpawn >= Math.Max(1, Settings.Trail.SpawnIntervalMilliseconds))
        {
            Trails.Add(new(next, Now, Random.Shared.NextDouble())); lastSpawn = Now;
            while (Trails.Count > Math.Clamp(Settings.Trail.Length, 1, 2000)) Trails.RemoveAt(0);
        }
    }
    public void AddClick(int button = 1, bool force = false)
    {
        if (!Settings.Enabled || (!force && !Settings.Click.Enabled)) return;
        Clicks.Add(new(Cursor, Now, Settings.Click.Style, Settings.Click.Color, Settings.Click.Size, Settings.Click.Thickness, Settings.Click.Speed));
        if (Clicks.Count > 100) Clicks.RemoveAt(0);
    }
    public void ShowLocate() { if (!Settings.Enabled) return; LocateStarted = Now; LocatePoint = Cursor; }
    public void BeginStroke(Point physicalPoint)
    {
        if (!Settings.Enabled || !Settings.Annotation.Enabled || !Drawing) return;
        EndStroke();
        var o = Settings.Annotation;
        if (o.Tool == AnnotationTool.Text) return;
        ActiveStroke = new(o.Tool, o.Tool == AnnotationTool.Highlighter ? o.HighlighterColor : o.Color, o.Tool == AnnotationTool.Highlighter ? o.HighlighterWidth : o.Width,
            o.FontSize, o.CalloutSize, o.HighlighterSnap, o.FadeSeconds, Now, o.Tool == AnnotationTool.Number ? nextNumber++ : 0, "");
        ActiveStroke.Points.Add(physicalPoint); Strokes.Add(ActiveStroke);
    }
    public void ContinueStroke(Point point)
    {
        if (ActiveStroke is not { } stroke || stroke.Tool == AnnotationTool.Number) return;
        if (stroke.Tool is AnnotationTool.Freehand or AnnotationTool.Highlighter)
        {
            if ((stroke.Points[^1] - point).Length >= .8) stroke.Points.Add(point);
        }
        else { if (stroke.Points.Count == 1) stroke.Points.Add(point); else stroke.Points[1] = point; }
    }
    public void EndStroke() { if (ActiveStroke is { } stroke) stroke.CompletedAt = Now; ActiveStroke = null; }
    public void AddText(Point point, string text)
    {
        if (!Settings.Enabled || !Settings.Annotation.Enabled || string.IsNullOrWhiteSpace(text)) return;
        var o = Settings.Annotation; var stroke = new AnnotationStroke(AnnotationTool.Text, o.Color, o.Width, o.FontSize, o.CalloutSize, false, o.FadeSeconds, Now, 0, text);
        stroke.Points.Add(point); stroke.CompletedAt = Now; Strokes.Add(stroke);
    }
    public void Clear() { Strokes.Clear(); ActiveStroke = null; nextNumber = 1; }
    public void Undo() { if (Strokes.Count == 0) return; var last = Strokes[^1]; Strokes.RemoveAt(Strokes.Count - 1); if (last == ActiveStroke) ActiveStroke = null; if (last.Number > 0) nextNumber = last.Number; }
    public void ShowKey(string text, bool plain = false)
    {
        if (!Settings.Enabled || !Settings.Keystrokes.Enabled || string.IsNullOrEmpty(text)) return;
        if (plain && Settings.Keystrokes.ModifiersOnly) return;
        if (!Settings.Keystrokes.ShowSpace && text is "Space" or "Espace" or "␣") return;
        if (!Settings.Keystrokes.ShowDelete && text is "Delete" or "Supprimer" or "Backspace" or "⌫") return;
        if (!Settings.Keystrokes.ShowReturn && text is "Return" or "Enter" or "Entrée" or "↵") return;
        var now = Now;
        if (plain && Keys.LastOrDefault() is { Plain: true } last && now - last.Updated < 1000)
        {
            last.Text += text; if (last.Text.Length > 100) last.Text = last.Text[^100..]; last.Updated = now;
        }
        else { Keys.Add(new(text, plain, now)); if (Keys.Count > 5) Keys.RemoveAt(0); }
    }
    public void SetCaption(string text) { Caption = text?.Trim() ?? ""; CaptionUpdated = Now; }
    public bool Tick(long nowMilliseconds)
    {
        var elapsed = Math.Clamp(nowMilliseconds - lastTick, 0, 100); lastTick = nowMilliseconds;
        if (Settings.Enabled && Settings.Ring.Enabled)
        {
            var delta = Cursor - RingPosition;
            var follow = Math.Clamp(Settings.Ring.FollowSpeed, .01, 1);
            var alpha = 1 - Math.Pow(1 - follow, elapsed / 16.6667);
            RingPosition = delta.Length < .15 ? Cursor : RingPosition + delta * alpha;
        }
        else RingPosition = Cursor;
        Trails.RemoveAll(t => nowMilliseconds - t.Created >= Math.Max(50, Settings.Trail.FadeMilliseconds));
        Clicks.RemoveAll(c => nowMilliseconds - c.Created >= c.Duration);
        Keys.RemoveAll(k => nowMilliseconds - k.Updated >= Math.Clamp(Settings.Keystrokes.DurationMilliseconds, 200, 30000));
        Strokes.RemoveAll(s => s != ActiveStroke && s.FadeSeconds > 0 && nowMilliseconds - s.CompletedAt >= s.FadeSeconds * 1000 + 400);
        if (Caption.Length > 0 && nowMilliseconds - CaptionUpdated >= Settings.Captions.LingerSeconds * 1000 + (Settings.Captions.SmoothAnimation ? Settings.Captions.FadeMilliseconds : 0)) Caption = "";
        return NeedsAnimation;
    }
    public bool NeedsAnimation => Settings.Enabled && (
        Trails.Count > 0 || Clicks.Count > 0 || Now - LocateStarted < 1000 ||
        (Settings.Keystrokes.Enabled && Keys.Count > 0) ||
        (Settings.Captions.LiveEnabled && Caption.Length > 0) ||
        Strokes.Any(s => s != ActiveStroke && s.FadeSeconds > 0) ||
        (Settings.Ring.Enabled && (Settings.Ring.Animate || (RingPosition - Cursor).Length > .15)));
}

internal sealed record TrailSample(Point Point, long Created, double Seed);
internal sealed record ClickSample(Point Point, long Created, ClickStyle Style, string Color, double Size, double Thickness, double Speed)
{
    public double Duration => 700 / Math.Clamp(Speed, .1, 10);
}
internal sealed class KeyLabel(string text, bool plain, long now)
{
    public string Text = text;
    public bool Plain = plain;
    public long Created = now;
    public long Updated = now;
}
internal sealed class AnnotationStroke(AnnotationTool tool, string color, double width, double fontSize, double calloutSize, bool snap, double fadeSeconds, long created, int number, string text)
{
    public AnnotationTool Tool { get; } = tool;
    public string Color { get; } = color;
    public double Width { get; } = width;
    public double FontSize { get; } = fontSize;
    public double CalloutSize { get; } = calloutSize;
    public bool Snap { get; } = snap;
    public double FadeSeconds { get; } = fadeSeconds;
    public long Created { get; } = created;
    public long CompletedAt { get; set; } = created;
    public int Number { get; } = number;
    public string Text { get; } = text;
    public List<Point> Points { get; } = new();
}
