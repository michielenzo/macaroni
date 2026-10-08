using System.Runtime.InteropServices;

namespace Macaroni;

internal static class ForegroundActivation
{
    internal static async Task Focus(nint window, CancellationToken cancellation)
    {
        if (Native.GetForegroundWindow() == window) return;
        Native.SetForegroundWindow(window);
        if (await Confirm(window, cancellation)) return;

        // Windows unlocks foreground activation on Alt input. Our suppressed
        // numpad event does not grant the tray process foreground permission.
        // Use a balanced, Ctrl-masked Alt tap only for this requested action;
        // the Ctrl mask prevents activating the current application's menu.
        // Never release a modifier the user is physically holding.
        int[] modifiers = [0x10, 0x11, 0x12, 0x5B, 0x5C];
        if (modifiers.All(key => (Native.GetAsyncKeyState(key) & 0x8000) == 0))
        {
            Native.Input[] inputs = [Key(0x11, false), Key(0x12, false), Key(0x12, true), Key(0x11, true)];
            uint sent = Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Native.Input>());
            if (sent > 0 && sent < inputs.Length)
            {
                // Best-effort cleanup if Windows accepted only part of the batch.
                Native.Input[] releases = [Key(0x12, true), Key(0x11, true)];
                Native.SendInput(2, releases, Marshal.SizeOf<Native.Input>());
            }
            if (sent == inputs.Length) Native.SetForegroundWindow(window);
        }
        if (!await Confirm(window, cancellation))
            throw new Exception("The window was placed, but focus could not be confirmed. Windows may be blocking activation; see macaroni.log for the target and foreground handles. " +
                $"Target=0x{window:X}, foreground=0x{Native.GetForegroundWindow():X}.");
    }

    private static Native.Input Key(ushort key, bool up) => new()
    {
        Type = 1,
        Data = new Native.InputData { Keyboard = new Native.KeyboardInput { Key = key, Flags = up ? 2u : 0u } }
    };

    private static async Task<bool> Confirm(nint window, CancellationToken cancellation)
    {
        // Cross-thread activation can finish after SetForegroundWindow returns.
        // Check the actual foreground window instead of treating its return
        // value as proof that focus succeeded or failed.
        for (int i = 0; i < 5; i++)
        {
            if (Native.GetForegroundWindow() == window) return true;
            await Task.Delay(30, cancellation);
        }
        return Native.GetForegroundWindow() == window;
    }
}
