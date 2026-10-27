namespace Jerry.Core.Settings;
public readonly record struct EffectActivationChange(bool SpotlightActivated);
public sealed class EffectActivation(AppSettings initial)
{
    private HashSet<string> enabled = Snapshot(initial);
    public EffectActivationChange Apply(AppSettings settings)
    {
        var current = Snapshot(settings);
        if (current.Except(enabled).Any()) settings.Enabled = true;
        var spotlight = current.Contains("spotlight") && !enabled.Contains("spotlight");
        enabled = current; return new(spotlight);
    }
    private static HashSet<string> Snapshot(AppSettings s)
    {
        var result = new HashSet<string>();
        if (s.Spotlight.Enabled) result.Add("spotlight"); if (s.Ring.Enabled) result.Add("ring");
        if (s.Trail.Enabled) result.Add("trail"); if (s.Click.Enabled) result.Add("click");
        if (s.Sound.Enabled) result.Add("sound"); if (s.Locate.Enabled) result.Add("locate");
        if (s.Annotation.Enabled) result.Add("annotation"); if (s.Keystrokes.Enabled) result.Add("keys");
        if (s.Zoom.Enabled) result.Add("zoom"); if (s.Zoom.LoupeEnabled) result.Add("loupe");
        if (s.Focus.Enabled) result.Add("focus"); if (s.Camera.Enabled) result.Add("camera");
        if (s.Captions.LiveEnabled) result.Add("captions"); return result;
    }
}
