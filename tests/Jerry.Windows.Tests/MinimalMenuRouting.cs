using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using Jerry.App;
using Jerry.App.Platform;
using Jerry.App.UI;

namespace Jerry.Windows.Tests;

internal static partial class Program
{
    private static async Task QuickMenuPointerRoutingAsync(AppController controller, QuickMenuWindow menu)
    {
        var pressed = typeof(AppController).GetMethod("PointerPressed", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Existing own controller input route was not found.");
        var input = typeof(AppController).GetField("input", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(controller) as GlobalInput
            ?? throw new InvalidOperationException("Existing own input service was not available.");
        var originalEnabled = controller.Settings.Enabled;
        try
        {
            if (!controller.DrawingMode) controller.ToggleDrawing();
            controller.ShowQuickMenu(); await PumpAsync(50);
            Check(controller.DrawingMode && input.CaptureMouse && menu.IsVisible, "Drawing mode has actual mouse capture armed while own quick menu is open");
            Check(Native.GetWindowRect(new WindowInteropHelper(menu).Handle, out var rect), "Own menu physical rectangle available for direct input-route regression");
            pressed.Invoke(controller, [new PointerInput((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2, 1, Environment.TickCount64)]);
            await PumpAsync(30);
            Check(menu.IsVisible, "Own pointer-pressed route keeps menu open for a point inside its native rectangle");
            var area = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(menu).Handle).WorkingArea;
            var x = area.Left + 2; var y = area.Top + 2;
            if (x >= rect.Left && x < rect.Right && y >= rect.Top && y < rect.Bottom) { x = area.Right - 2; y = area.Bottom - 2; }
            pressed.Invoke(controller, [new PointerInput(x, y, 1, Environment.TickCount64)]); await PumpAsync(30);
            Check(!menu.IsVisible && controller.DrawingMode && input.CaptureMouse, "Own outside pointer route dismisses menu while drawing capture remains armed");
            controller.ToggleDrawing();
            controller.Settings.Enabled = false; controller.SettingsChanged();
            controller.ShowQuickMenu(); await PumpAsync(40);
            Check(menu.IsVisible && !controller.Settings.Enabled, "Own menu can open while master effects are disabled");
            Check(Native.GetWindowRect(new WindowInteropHelper(menu).Handle, out rect), "Disabled-master menu native rectangle available");
            x = area.Left + 2; y = area.Top + 2;
            if (x >= rect.Left && x < rect.Right && y >= rect.Top && y < rect.Bottom) { x = area.Right - 2; y = area.Bottom - 2; }
            pressed.Invoke(controller, [new PointerInput(x, y, 1, Environment.TickCount64)]); await PumpAsync(30);
            Check(!menu.IsVisible && !controller.Settings.Enabled, "Own outside pointer route dismisses before master-disabled early return");
        }
        finally
        {
            if (controller.DrawingMode) controller.ToggleDrawing();
            controller.Settings.Enabled = originalEnabled; controller.SettingsChanged();
            menu.Hide();
        }
    }
}
