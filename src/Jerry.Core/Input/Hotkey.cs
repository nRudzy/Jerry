namespace Jerry.Core.Input;

[Flags]
public enum HotkeyModifiers : uint { None = 0, Alt = 1, Control = 2, Shift = 4, Windows = 8 }
public readonly record struct Hotkey(HotkeyModifiers Modifiers, uint VirtualKey)
{
    public static bool TryParse(string? text, out Hotkey hotkey, out string? error)
    {
        hotkey = default; error = null;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var modifiers = HotkeyModifiers.None;
        uint key = 0;
        foreach (var part in text.Split('+', StringSplitOptions.TrimEntries))
        {
            var modifier = part.ToUpperInvariant() switch
            {
                "CTRL" or "CONTROL" => HotkeyModifiers.Control,
                "ALT" => HotkeyModifiers.Alt, "SHIFT" => HotkeyModifiers.Shift,
                "WIN" or "WINDOWS" => HotkeyModifiers.Windows, _ => HotkeyModifiers.None
            };
            if (modifier != HotkeyModifiers.None)
            {
                if ((modifiers & modifier) != 0) { error = "Modificateur répété."; return false; }
                modifiers |= modifier; continue;
            }
            var upper = part.ToUpperInvariant();
            uint candidate = (uint)(upper.Length == 1 && char.IsAsciiLetterOrDigit(upper[0]) ? upper[0] : upper switch
            {
                "SPACE" or "ESPACE" => 32, "ENTER" => 13, "TAB" => 9,
                "ESC" or "ESCAPE" => 27, "BACKSPACE" => 8,
                "UP" => 38, "DOWN" => 40, "LEFT" => 37, "RIGHT" => 39,
                "HOME" => 36, "END" => 35, "INSERT" => 45, "DELETE" => 46,
                "PLUS" => 187, "MINUS" => 189, _ => 0
            });
            if (upper.StartsWith('F') && int.TryParse(upper.AsSpan(1), out var function) && function is >= 1 and <= 24)
                candidate = (uint)(111 + function);
            if (key != 0 || candidate == 0) { error = "Choisir une touche et, si souhaité, Ctrl, Alt, Shift ou Win."; return false; }
            key = candidate;
        }
        if (key == 0) { error = "Une touche est nécessaire après les modificateurs."; return false; }
        hotkey = new(modifiers, key); return true;
    }
}
