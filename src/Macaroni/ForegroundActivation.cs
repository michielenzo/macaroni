using System.Runtime.InteropServices;

namespace Macaroni;

internal static class ForegroundActivation
{
    internal static async Task Focus(nint window, CancellationToken cancellation, Action<string>? log = null, Func<bool>? isCurrent = null)
    {
        bool Current() { cancellation.ThrowIfCancellationRequested(); return isCurrent?.Invoke() != false; }
        if (!Current() || Native.GetForegroundWindow() == window) return;
        Native.SetForegroundWindow(window);
        if (await Confirm(window, cancellation, Current) || !Current()) return;

        log?.Invoke($"Focus recovery: target={Describe(window)}; foreground={Describe(Native.GetForegroundWindow())}");
        nint preview = Native.GetForegroundWindow();
        nint taskbar = TaskbarPreview.FindTaskbar(preview);
        if (taskbar != 0)
        {
            // Don't interrupt a held taskbar click or drag. The wait is bounded
            // and asynchronous so another shortcut can supersede this request.
            for (int i = 0; i < 10 && !InputReleased() && Current(); i++) await Task.Delay(30, cancellation);
            if (!Current()) return;
            if (InputReleased() && TaskbarPreview.FindTaskbar(preview) == taskbar)
            {
                // XAML taskbar previews can ignore Escape and WM_CANCELMODE.
                // Activate their taskbar on the same shell UI thread: Explorer
                // then dismisses its own popup. No fake close/deactivation
                // messages, global Escape, or attached input queues are needed.
                bool accepted = Native.SetForegroundWindow(taskbar);
                log?.Invoke($"Taskbar preview: activate owner accepted={accepted}; preview={Describe(preview)}; taskbar={Describe(taskbar)}");
                for (int i = 0; i < 5 && Current() && Native.IsWindowVisible(preview) && Native.GetForegroundWindow() == preview; i++)
                    await Task.Delay(30, cancellation);
                if (!Current()) return;
                log?.Invoke($"Taskbar preview: dismissed={!Native.IsWindowVisible(preview)}; foreground={Describe(Native.GetForegroundWindow())}");
                Native.SetForegroundWindow(window);
                if (await Confirm(window, cancellation, Current)) { log?.Invoke("Focus recovery: confirmed after taskbar dismissal."); return; }
            }
            else log?.Invoke("Taskbar preview: dismissal skipped; input held or foreground changed.");
        }
        if (!Current()) return;

        // Windows unlocks foreground activation on Alt input. Our suppressed
        // numpad event does not grant the tray process foreground permission.
        // The Ctrl mask prevents activating the current application's menu.
        // Never release a modifier the user is physically holding.
        if (InputReleased())
        {
            Native.Input[] inputs = [Key(0x11, false), Key(0x12, false), Key(0x12, true), Key(0x11, true)];
            uint sent = Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Native.Input>());
            if (sent > 0 && sent < inputs.Length)
            {
                Native.Input[] releases = [Key(0x12, true), Key(0x11, true)];
                Native.SendInput(2, releases, Marshal.SizeOf<Native.Input>());
            }
            if (sent == inputs.Length) Native.SetForegroundWindow(window);
        }
        if (!await Confirm(window, cancellation, Current) && Current())
        {
            log?.Invoke($"Focus recovery: failed; target={Describe(window)}; foreground={Describe(Native.GetForegroundWindow())}");
            throw new Exception("The window was placed, but focus could not be confirmed. Windows may be blocking activation; see macaroni.log for details. " +
                $"Target=0x{window:X}, foreground=0x{Native.GetForegroundWindow():X}.");
        }
        if (Current()) log?.Invoke("Focus recovery: confirmed after activation fallback.");
    }

    internal static string Describe(nint window)
    {
        uint thread = Native.GetWindowThreadProcessId(window, out uint process);
        return $"hwnd=0x{window:X}, class={TaskbarPreview.ClassName(window)}, pid={process}, thread={thread}";
    }

    private static bool InputReleased() => new[] { 0x01, 0x02, 0x04, 0x05, 0x06, 0x10, 0x11, 0x12, 0x5B, 0x5C }
        .All(key => (Native.GetAsyncKeyState(key) & 0x8000) == 0);

    private static Native.Input Key(ushort key, bool up) => new()
    {
        Type = 1,
        Data = new Native.InputData { Keyboard = new Native.KeyboardInput { Key = key, Flags = up ? 2u : 0u } }
    };

    private static async Task<bool> Confirm(nint window, CancellationToken cancellation, Func<bool> current)
    {
        // Cross-thread activation can finish after SetForegroundWindow returns.
        // Verify the foreground handle instead of trusting the API return value.
        for (int i = 0; i < 5 && current(); i++)
        {
            if (Native.GetForegroundWindow() == window) return true;
            await Task.Delay(30, cancellation);
        }
        return Native.GetForegroundWindow() == window;
    }
}
