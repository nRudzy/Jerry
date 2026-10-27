using Jerry.Core.Input;

namespace Jerry.Core.Tests;

public sealed class InputTests
{
    [Theory]
    [InlineData("Ctrl+Alt+R", HotkeyModifiers.Control | HotkeyModifiers.Alt, 82u)]
    [InlineData("Shift+F8", HotkeyModifiers.Shift, 119u)]
    [InlineData("Win+Space", HotkeyModifiers.Windows, 32u)]
    public void ParsesExplicitWindowsShortcuts(string text, HotkeyModifiers modifiers, uint key)
    {
        Assert.True(Hotkey.TryParse(text, out var actual, out var error), error);
        Assert.Equal(new Hotkey(modifiers, key), actual);
    }

    [Theory]
    [InlineData("Ctrl+Ctrl+R")]
    [InlineData("Ctrl+A+B")]
    [InlineData("Ctrl")]
    [InlineData("Fish+R")]
    public void RejectsAmbiguousOrUnsupportedShortcut(string text)
        => Assert.False(Hotkey.TryParse(text, out _, out _));

    [Fact]
    public void StraightMovementDoesNotTriggerLocateButRapidReversalsDo()
    {
        var detector = new ShakeDetector();
        for (var i = 0; i < 8; i++) Assert.False(detector.Observe(i * 90, 100, i * 40, .6));
        var triggered = false;
        for (var i = 0; i < 9; i++) triggered |= detector.Observe(i % 2 == 0 ? 400 : 650, 100, 500 + i * 35, .6);
        Assert.True(triggered);
        Assert.False(detector.Observe(400, 100, 900, .6));
    }
}
