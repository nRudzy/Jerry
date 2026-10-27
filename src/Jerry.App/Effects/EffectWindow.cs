using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Jerry.App.Platform;
using Jerry.App.Captions;
using Jerry.Core.Recording;
using Jerry.Core.Settings;

namespace Jerry.App.Effects;

public sealed class EffectWindow : Window
{
    private readonly EffectVisual visual;
    public EffectWindow(EffectScene scene, PixelRect monitor)
    {
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; ShowActivated = false; Topmost = true;
        Left = monitor.X; Top = monitor.Y; Width = monitor.Width; Height = monitor.Height;
        visual = new EffectVisual(scene, monitor); Content = visual;
        SourceInitialized += (_, _) => { WindowTools.MakeOverlay(this); WindowTools.Position(this, monitor); };
        Loaded += (_, _) => { WindowTools.Position(this, monitor); Refresh(); };
    }
    public void Refresh() => visual.InvalidateVisual();
}

internal sealed class EffectVisual(EffectScene scene, PixelRect monitor) : FrameworkElement
{
    private double PixelsPerDip => VisualTreeHelper.GetDpi(this).PixelsPerDip;
    private static readonly Dictionary<string, SolidColorBrush> Colors = new(StringComparer.OrdinalIgnoreCase);
    internal static SolidColorBrush ColorBrush(string? value)
    {
        var key = string.IsNullOrWhiteSpace(value) ? "#7DE5C3" : value;
        if (Colors.TryGetValue(key, out var brush)) return brush;
        try { brush = new((Color)ColorConverter.ConvertFromString(key)); } catch { brush = new(Color.FromRgb(125, 229, 195)); }
        brush.Freeze(); if (Colors.Count > 128) Colors.Clear(); Colors[key] = brush; return brush;
    }
    private static Pen Pen(Brush brush, double width) => new(brush, Math.Max(.1, width)) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
    private Rect MonitorRect => new(monitor.X, monitor.Y, monitor.Width, monitor.Height);
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (!scene.Settings.Enabled) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        dc.PushTransform(new ScaleTransform(1 / dpi.DpiScaleX, 1 / dpi.DpiScaleY));
        dc.PushTransform(new TranslateTransform(-monitor.X, -monitor.Y));
        dc.PushClip(new RectangleGeometry(MonitorRect));
        DrawFocus(dc); DrawSpotlight(dc); DrawTrail(dc); DrawRing(dc); DrawClicks(dc); DrawLocate(dc); DrawAnnotations(dc);
        if (MonitorRect.Contains(scene.Cursor)) { DrawKeys(dc); DrawCaption(dc); }
        dc.Pop(); dc.Pop(); dc.Pop();
    }
    private void DrawFocus(DrawingContext dc)
    {
        if (!scene.Settings.Focus.Enabled) return;
        Geometry g = new RectangleGeometry(MonitorRect);
        if (scene.FocusBounds is Rect active && !active.IsEmpty) g = new CombinedGeometry(GeometryCombineMode.Exclude, g, new RectangleGeometry(active));
        dc.PushOpacity(Math.Clamp(scene.Settings.Focus.Opacity, 0, 1)); dc.DrawGeometry(Brushes.Black, null, g); dc.Pop();
    }
    private void DrawSpotlight(DrawingContext dc)
    {
        var s = scene.Settings.Spotlight; if (!s.Enabled) return;
        var c = scene.Cursor;
        Geometry hole = s.Shape switch
        {
            SpotlightShape.HorizontalBar => new RectangleGeometry(new Rect(monitor.X, c.Y - s.BarThickness / 2, monitor.Width, s.BarThickness)),
            SpotlightShape.VerticalBar => new RectangleGeometry(new Rect(c.X - s.BarThickness / 2, monitor.Y, s.BarThickness, monitor.Height)),
            _ => new EllipseGeometry(c, s.Radius, s.Radius)
        };
        var outside = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(MonitorRect), hole);
        dc.PushOpacity(Math.Clamp(s.Opacity, 0, 1)); dc.DrawGeometry(ColorBrush(s.Color), null, outside); dc.Pop();
        if (s.AreaOpacity > 0) { dc.PushOpacity(Math.Clamp(s.AreaOpacity, 0, 1)); dc.DrawGeometry(ColorBrush(s.HighlightColor), null, hole); dc.Pop(); }
    }
    private void DrawRing(DrawingContext dc)
    {
        var s = scene.Settings.Ring; if (!s.Enabled) return;
        var pulse = s.Animate ? 1 + .12 * Math.Sin(scene.Now / 1000d * Math.PI * 2 * s.Speed) : 1;
        var radius = Math.Max(1, s.Radius * pulse); var color = ColorBrush(s.Color); var point = scene.RingPosition;
        dc.PushOpacity(Math.Clamp(s.Opacity, 0, 1));
        if (s.NeonGlow)
        {
            foreach (var spread in new[] { 12d, 7d, 3d }) { dc.PushOpacity(.10); dc.DrawEllipse(null, Pen(color, s.Thickness + spread), point, radius, radius); dc.Pop(); }
            dc.DrawEllipse(null, Pen(Brushes.White, Math.Max(.7, s.Thickness * .3)), point, radius, radius);
        }
        dc.DrawEllipse(null, Pen(color, s.Thickness), point, radius, radius); dc.Pop();
    }
    private void DrawTrail(DrawingContext dc)
    {
        var s = scene.Settings.Trail; if (!s.Enabled || scene.Trails.Count == 0) return;
        var now = scene.Now; var brush = ColorBrush(s.Color);
        for (var i = 0; i < scene.Trails.Count; i++)
        {
            var sample = scene.Trails[i]; var life = Math.Clamp(1 - (now - sample.Created) / (double)Math.Max(50, s.FadeMilliseconds), 0, 1);
            if (life <= 0) continue;
            dc.PushOpacity(life * Math.Clamp(s.Opacity, 0, 1)); var size = Math.Max(1, s.Width); var p = sample.Point;
            switch (s.Style)
            {
                case TrailStyle.SolidLine:
                case TrailStyle.DashedLine:
                    if (i > 0) { var pen = Pen(brush, size * (.3 + .7 * life)); if (s.Style == TrailStyle.DashedLine) pen.DashStyle = new DashStyle(new[] { 2d, 2d }, i * .1); dc.DrawLine(pen, scene.Trails[i - 1].Point, p); }
                    break;
                case TrailStyle.Dots: dc.DrawEllipse(brush, null, p, size / 2, size / 2); break;
                case TrailStyle.Glow:
                    for (var layer = 3; layer > 0; layer--) { dc.PushOpacity(.16); dc.DrawEllipse(brush, null, p, size * layer, size * layer); dc.Pop(); }
                    dc.DrawEllipse(brush, null, p, size / 2, size / 2); break;
                case TrailStyle.Stars: dc.DrawGeometry(brush, null, Star(p, size * 1.5 * (.4 + life * .6), sample.Seed * 6)); break;
                case TrailStyle.Kawaii:
                    var center = new Point(p.X, p.Y - (1 - life) * 15);
                    dc.DrawGeometry(brush, null, Heart(center, size * 1.9));
                    dc.DrawEllipse(Brushes.White, null, new(center.X - size * .4, center.Y - size * .15), size * .17, size * .17);
                    dc.DrawEllipse(Brushes.White, null, new(center.X + size * .4, center.Y - size * .15), size * .17, size * .17); break;
                default:
                    var drift = new Point(p.X + Math.Sin(sample.Seed * 15) * (1 - life) * 18, p.Y + Math.Cos(sample.Seed * 15) * (1 - life) * 18);
                    dc.DrawEllipse(brush, null, drift, size * life * .7, size * life * .7); break;
            }
            dc.Pop();
        }
    }
    private void DrawClicks(DrawingContext dc)
    {
        foreach (var click in scene.Clicks)
        {
            var t = Math.Clamp((scene.Now - click.Created) / click.Duration, 0, 1); if (t >= 1) continue;
            var eased = 1 - Math.Pow(1 - t, 3); var b = ColorBrush(click.Color); var p = click.Point; var radius = click.Size * .5;
            dc.PushOpacity(1 - t);
            switch (click.Style)
            {
                case ClickStyle.RippleWave:
                    for (var ring = 0; ring < 3; ring++) { var phase = (t - ring * .16) / .68; if (phase < 0 || phase > 1) continue; dc.PushOpacity(1 - phase); dc.DrawEllipse(null, Pen(b, click.Thickness), p, Math.Max(.1, radius * phase), Math.Max(.1, radius * phase)); dc.Pop(); } break;
                case ClickStyle.RingBurst:
                    dc.DrawEllipse(null, Pen(b, click.Thickness), p, Math.Max(.1, radius * eased), Math.Max(.1, radius * eased));
                    for (var ray = 0; ray < 12; ray++) { var a = ray * Math.PI / 6; dc.DrawLine(Pen(b, click.Thickness * .7), Radial(p, radius * (.4 + eased * .4), a), Radial(p, radius * (.6 + eased * .8), a)); } break;
                case ClickStyle.ColorFlash: dc.PushOpacity(.45 * (1 - t)); dc.DrawEllipse(b, null, p, radius * (.5 + eased), radius * (.5 + eased)); dc.Pop(); break;
                case ClickStyle.ParticleBurst:
                    for (var particle = 0; particle < 18; particle++) { var a = particle * Math.PI * 2 / 18; var q = Radial(p, radius * eased * (particle % 2 == 0 ? 1.6 : 1), a); var r = Math.Max(.2, click.Thickness * (1 - t)); dc.DrawEllipse(b, null, q, r, r); } break;
                case ClickStyle.GlowPulse:
                    for (var layer = 4; layer >= 1; layer--) { dc.PushOpacity(.08 + .08 * (4 - layer)); var r = radius * eased * (layer / 2d); dc.DrawEllipse(b, null, p, Math.Max(.1, r), Math.Max(.1, r)); dc.Pop(); } break;
                case ClickStyle.ScaleBounce:
                    var scale = .4 + .7 * Math.Abs(Math.Sin(t * Math.PI * 1.7)) * (1 - t * .3); dc.DrawEllipse(null, Pen(b, click.Thickness), p, radius * scale, radius * scale); break;
                default: dc.DrawEllipse(null, Pen(b, click.Thickness), p, Math.Max(.1, radius * eased), Math.Max(.1, radius * eased)); break;
            }
            dc.Pop();
        }
    }
    private void DrawLocate(DrawingContext dc)
    {
        var t = (scene.Now - scene.LocateStarted) / 1000d; if (t is < 0 or >= 1) return;
        var brush = ColorBrush("#7DE5C3"); dc.PushOpacity((1 - t) * .9);
        var radius = 30 + t * 150; dc.DrawEllipse(null, Pen(brush, 4 * (1 - t) + 1), scene.LocatePoint, radius, radius);
        dc.DrawLine(Pen(brush, 2), new(scene.LocatePoint.X - 16, scene.LocatePoint.Y), new(scene.LocatePoint.X + 16, scene.LocatePoint.Y));
        dc.DrawLine(Pen(brush, 2), new(scene.LocatePoint.X, scene.LocatePoint.Y - 16), new(scene.LocatePoint.X, scene.LocatePoint.Y + 16)); dc.Pop();
    }
    private void DrawAnnotations(DrawingContext dc)
    {
        if (!scene.Settings.Annotation.Enabled) return;
        foreach (var stroke in scene.Strokes)
        {
            if (stroke.Points.Count == 0) continue;
            var opacity = stroke.FadeSeconds > 0 && stroke != scene.ActiveStroke ? Math.Clamp(1 - (scene.Now - stroke.CompletedAt - stroke.FadeSeconds * 1000) / 400, 0, 1) : 1;
            dc.PushOpacity(opacity); var b = ColorBrush(stroke.Color); var pen = Pen(b, stroke.Width); var first = stroke.Points[0]; var last = stroke.Points[^1];
            switch (stroke.Tool)
            {
                case AnnotationTool.Freehand:
                    if (stroke.Points.Count == 1) dc.DrawEllipse(b, null, first, stroke.Width / 2, stroke.Width / 2); else dc.DrawGeometry(null, pen, Polyline(stroke.Points)); break;
                case AnnotationTool.Highlighter:
                    dc.PushOpacity(.3);
                    if (stroke.Snap && Math.Abs(last.Y - first.Y) < Math.Max(8, Math.Abs(last.X - first.X) * .12)) dc.DrawLine(pen, first, new(last.X, first.Y));
                    else if(stroke.Points.Count>1) dc.DrawGeometry(null, pen, Polyline(stroke.Points));
                    else dc.DrawEllipse(b, null, first, stroke.Width / 2, stroke.Width / 2);
                    dc.Pop(); break;
                case AnnotationTool.Arrow:
                    dc.DrawLine(pen, first, last); var direction = first - last; if(direction.Length>1){direction.Normalize();var normal=new Vector(-direction.Y,direction.X);var head=Math.Max(12,stroke.Width*4);dc.DrawLine(pen,last,last+direction*head+normal*head*.5);dc.DrawLine(pen,last,last+direction*head-normal*head*.5);}break;
                case AnnotationTool.Rectangle: dc.DrawRectangle(null, pen, Bounds(first, last)); break;
                case AnnotationTool.Circle: dc.DrawEllipse(null, pen, new((first.X + last.X) / 2, (first.Y + last.Y) / 2), Math.Abs(first.X - last.X) / 2, Math.Abs(first.Y - last.Y) / 2); break;
                case AnnotationTool.Line: dc.DrawLine(pen, first, last); break;
                case AnnotationTool.Number:
                    var radius = stroke.CalloutSize / 2; dc.DrawEllipse(b, Pen(Brushes.White, 2), first, radius, radius);
                    var number = Text(stroke.Number.ToString(CultureInfo.CurrentCulture), Math.Max(10, stroke.CalloutSize * .55), Brushes.Black, bold:true); dc.DrawText(number, new(first.X-number.Width/2,first.Y-number.Height/2));break;
                case AnnotationTool.Text:
                    var label = Text(stroke.Text, stroke.FontSize, b, bold:true); var geometry = label.BuildGeometry(first);dc.DrawGeometry(b, Pen(ColorBrush("#101724"), 2), geometry);break;
            }
            dc.Pop();
        }
    }
    private void DrawKeys(DrawingContext dc)
    {
        var s = scene.Settings.Keystrokes; if (!s.Enabled) return;
        var y = monitor.Bottom - 75d;
        foreach (var key in scene.Keys.AsEnumerable().Reverse())
        {
            var age = scene.Now - key.Updated; var remaining = s.DurationMilliseconds - age; var fade = Math.Clamp(remaining / 300d, 0, 1); var appear = Math.Clamp((scene.Now - key.Created) / 100d, 0, 1);
            var text = Text(key.Text, s.FontSize, Brushes.White, bold:s.Style==KeyDisplayStyle.Keycaps);text.MaxTextWidth=Math.Max(100,monitor.Width*.8);
            var width = Math.Min(monitor.Width * .85, text.Width + 34); var height = text.Height + 18;var box=new Rect(monitor.X+(monitor.Width-width)/2,y-height,width,height);
            var accent = ColorBrush(s.AccentColor); Brush fill = ColorBrush("#D9141F30"); Pen? border = null;var round=10d;
            switch(s.Style)
            {
                case KeyDisplayStyle.Glass: fill=ColorBrush("#99475467");border=Pen(ColorBrush("#70FFFFFF"),1);round=14;break;
                case KeyDisplayStyle.Keycaps:fill=ColorBrush("#F2273345");border=Pen(ColorBrush("#91A3B8"),1.5);round=6;break;
                case KeyDisplayStyle.Neon:border=Pen(accent,2);fill=ColorBrush("#E608101C");break;
                case KeyDisplayStyle.MouzzGradient:fill=new LinearGradientBrush(((SolidColorBrush)accent).Color,Color.FromRgb(102,93,220),35);break;
                case KeyDisplayStyle.Minimal:fill=Brushes.Transparent;break;
            }
            dc.PushOpacity(fade*appear);dc.DrawRoundedRectangle(fill,border,box,round,round);
            if(s.Style==KeyDisplayStyle.Keycaps)dc.DrawLine(Pen(ColorBrush("#708397"),2),new(box.Left+8,box.Bottom-4),new(box.Right-8,box.Bottom-4));
            if(s.Style==KeyDisplayStyle.Neon){dc.PushOpacity(.15);dc.DrawRoundedRectangle(null,Pen(accent,7),box,round,round);dc.Pop();}
            dc.DrawText(text,new(box.X+(box.Width-text.Width)/2,box.Y+8));dc.Pop();y=box.Top-8;
        }
    }
    private void DrawCaption(DrawingContext dc)
    {
        var s=scene.Settings.Captions;if(!s.LiveEnabled||scene.Caption.Length==0)return;
        var since=scene.Now-scene.CaptionUpdated;
        var fade = s.SmoothAnimation
            ? Math.Clamp(since / 120d, 0, 1) * (s.FadeMilliseconds <= 0 ? (since > s.LingerSeconds * 1000 ? 0 : 1) : Math.Clamp(1 - (since - s.LingerSeconds * 1000) / s.FadeMilliseconds, 0, 1))
            : (since <= s.LingerSeconds * 1000 ? 1 : 0);
        var width=Math.Max(120,monitor.Width*.82);var maxWords=Math.Clamp(s.WordsPerCaption,1,30);var words=scene.Caption.Split(' ',StringSplitOptions.RemoveEmptyEntries);var visible=string.Join(" ",words.TakeLast(maxWords));
        var text=Text(visible,s.FontSize,ColorBrush(s.Color),s.FontFamily,s.Style!=CaptionStyle.Minimal);text.MaxTextWidth=width;text.MaxLineCount=Math.Clamp(s.Lines,1,3);text.Trimming=TextTrimming.WordEllipsis;text.TextAlignment=TextAlignment.Center;
        var x=monitor.X+Math.Clamp(s.X,0,1)*monitor.Width;var y=monitor.Y+Math.Clamp(s.Y,0,1)*monitor.Height;
        var box=new Rect(Math.Clamp(x-width/2,monitor.X+10,Math.Max(monitor.X+10,monitor.Right-width-10)),Math.Clamp(y-text.Height/2,monitor.Y+10,Math.Max(monitor.Y+10,monitor.Bottom-text.Height-30)),width,text.Height+20);
        dc.PushOpacity(fade);
        if(s.Background)dc.DrawRoundedRectangle(ColorBrush("#D80B1220"),null,box,12,12);
        var point=new Point(box.Left,box.Top+8);
        if(s.Style==CaptionStyle.BoldOutline)dc.DrawGeometry(ColorBrush(s.Color),Pen(Brushes.Black,4),text.BuildGeometry(point));
        else dc.DrawText(text,point);
        // Live input has no word timestamps; synchronized word animation belongs to the timed export/editor.
        dc.Pop();
    }
    private FormattedText Text(string value,double size,Brush color,string family="Segoe UI",bool bold=false)
    {
        var bundled = family is "Montserrat" or "Oswald";
        return new(value,CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface(CaptionFonts.ResolveFamily(family),FontStyles.Normal,bundled?CaptionFonts.Weight(family):bold?FontWeights.SemiBold:FontWeights.Normal,FontStretches.Normal),Math.Clamp(size,6,200),color,PixelsPerDip);
    }
    private static Rect Bounds(Point a,Point b)=>new(new Point(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y)),new Point(Math.Max(a.X,b.X),Math.Max(a.Y,b.Y)));
    private static Point Radial(Point p,double radius,double angle)=>new(p.X+Math.Cos(angle)*radius,p.Y+Math.Sin(angle)*radius);
    private static StreamGeometry Polyline(IReadOnlyList<Point> points)
    {
        var geometry=new StreamGeometry();using(var ctx=geometry.Open()){ctx.BeginFigure(points[0],false,false);ctx.PolyLineTo(points.Skip(1).ToArray(),true,false);}geometry.Freeze();return geometry;
    }
    private static StreamGeometry Star(Point center,double radius,double angle)
    {
        var geometry=new StreamGeometry();using(var ctx=geometry.Open()){ctx.BeginFigure(Radial(center,radius,angle-Math.PI/2),true,true);for(var i=1;i<10;i++)ctx.LineTo(Radial(center,i%2==0?radius:radius*.42,angle-Math.PI/2+i*Math.PI/5),true,false);}geometry.Freeze();return geometry;
    }
    private static StreamGeometry Heart(Point p,double size)
    {
        var g=new StreamGeometry();using(var c=g.Open()){c.BeginFigure(new(p.X,p.Y+size*.65),true,true);c.BezierTo(new(p.X-size*1.4,p.Y-size*.25),new(p.X-size*.65,p.Y-size),new(p.X,p.Y-size*.35),true,false);c.BezierTo(new(p.X+size*.65,p.Y-size),new(p.X+size*1.4,p.Y-size*.25),new(p.X,p.Y+size*.65),true,false);}g.Freeze();return g;
    }
}
