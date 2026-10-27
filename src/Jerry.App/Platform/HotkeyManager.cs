using System.Windows.Interop;
using Jerry.Core.Input;

namespace Jerry.App.Platform;

public sealed class HotkeyManager : IDisposable
{
    private readonly HwndSource source = new(new HwndSourceParameters("Jerry shortcuts") { Width = 0, Height = 0, WindowStyle = 0 });
    private readonly Dictionary<int, string> registered = new();
    public event Action<string>? Triggered;
    public HotkeyManager() => source.AddHook(Message);
    public IReadOnlyList<string> Apply(Dictionary<string, string> hotkeys)
    {
        foreach (var id in registered.Keys) Native.UnregisterHotKey(source.Handle, id);
        registered.Clear(); var errors = new List<string>(); var idNext = 1;
        foreach (var pair in hotkeys)
        {
            if (string.IsNullOrWhiteSpace(pair.Value)) continue;
            if (!Hotkey.TryParse(pair.Value, out var value, out var error)) { errors.Add(pair.Key + " : " + error); continue; }
            if (!Native.RegisterHotKey(source.Handle, idNext, (uint)value.Modifiers | 0x4000, value.VirtualKey))
                errors.Add(pair.Value + " est déjà utilisé ou réservé par Windows.");
            else registered[idNext] = pair.Key;
            idNext++;
        }
        return errors;
    }
    private nint Message(nint window, int message, nint w, nint l, ref bool handled)
    {
        if (message == 0x312 && registered.TryGetValue((int)w, out var action)) { Triggered?.Invoke(action); handled = true; }
        return 0;
    }
    public void Dispose()
    { foreach (var id in registered.Keys) Native.UnregisterHotKey(source.Handle, id); source.Dispose(); }
}
