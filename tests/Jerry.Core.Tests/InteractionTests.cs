using Jerry.Core.Input;
using Jerry.Core.Recording;
using Jerry.Core.Settings;

namespace Jerry.Core.Tests;

public sealed class InteractionTests
{
    [Fact]
    public void ACombinedModifierCannotBecomeAnIsolatedTap()
    {
        var tap = new KeyTapGesture();
        tap.Begin(100, otherKeysHeld: true);
        Assert.False(tap.Release(160));
        tap.Begin(200, otherKeysHeld: false);
        Assert.True(tap.Release(270));
        tap.Begin(400, otherKeysHeld: false); tap.Invalidate();
        Assert.False(tap.Release(450));
    }
    [Fact]
    public void MasterStaysOffUntilAnAdditionalEffectIsEnabled()
    {
        var settings = new AppSettings();
        var tracking = new EffectActivation(settings);
        settings.Enabled = false;
        tracking.Apply(settings);
        Assert.False(settings.Enabled);
        settings.Spotlight.Enabled = true;
        var result = tracking.Apply(settings);
        Assert.True(settings.Enabled);
        Assert.True(result.SpotlightActivated);
    }
    [Fact]
    public void ZoomInterpolatesScaleAndCursorInsteadOfJumping()
    {
        var motion = new ZoomMotion(); motion.Reset(100, 100, 0);
        Assert.Equal(1, motion.Advance(2, 100, 100, 0, 200, .3).Scale);
        Assert.Equal(1.5, motion.Advance(2, 100, 100, 100, 200, .3).Scale, 3);
        Assert.Equal(2, motion.Advance(2, 100, 100, 200, 200, .3).Scale);
        var moved = motion.Advance(2, 500, 100, 216, 200, .3);
        Assert.InRange(moved.X, 100.1, 499.9);
        Assert.True(moved.IsMoving);
    }
    [Fact]
    public void LoupeStaysInsideNegativeOriginMonitorAtItsEdges()
    {
        var result = MagnificationGeometry.LensBounds(new(-1920, 0, 1920, 1080), -1, 1079, 210);
        Assert.True(result.X >= -1920 && result.Right <= 0);
        Assert.True(result.Y >= 0 && result.Bottom <= 1080);
    }
    [Fact]
    public void NewlyDiscoveredControlsHaveSafeAndConsistentBounds()
    {
        var settings = new AppSettings(); settings.Keystrokes.FontSize = 110;
        settings.Spotlight.HoldDelayMilliseconds = -3; settings.Zoom.AnimationMilliseconds = -10;
        settings.UpdateRate = 0; SettingsStore.Validate(settings);
        Assert.Equal(110, settings.Keystrokes.FontSize);
        Assert.InRange(settings.Spotlight.HoldDelayMilliseconds, 0, 2000);
        Assert.InRange(settings.Zoom.AnimationMilliseconds, 0, 2000);
        Assert.InRange(settings.UpdateRate, 15, 120);
        settings.Captions.FadeMilliseconds = 3000;
        SettingsStore.Validate(settings);
        Assert.Equal(3000, settings.Captions.FadeMilliseconds);
    }
}
