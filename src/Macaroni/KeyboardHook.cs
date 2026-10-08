using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Macaroni;

internal sealed class KeyboardHook : IDisposable
{
    private readonly Native.HookProc callback;
    private readonly nint hook;
    private readonly KeyPolicy policy = new();
    internal KeyboardHook(Func<int, bool> shouldCapture, Action<int> dispatch)
    {
        callback = (code, msg, data) =>
        {
            if (code >= 0)
            {
                var key = Marshal.PtrToStructure<Native.Keyboard>(data);
                // Extended navigation keys share scan codes with the physical numpad.
                if ((key.Flags & 0x11) == 0 && Digit(key.Scan) is int digit)
                {
                    bool down = msg == 0x100 || msg == 0x104;
                    bool up = msg == 0x101 || msg == 0x105;
                    if (down || up)
                    {
                        var result = policy.Handle(key.Scan, down, (Native.GetKeyState(0x90) & 1) != 0, shouldCapture(digit));
                        if (result.Trigger) dispatch(digit);
                        if (result.Suppress) return 1;
                    }
                }
            }
            return Native.CallNextHookEx(hook, code, msg, data);
        };
        hook = Native.SetWindowsHookEx(13, callback, Native.GetModuleHandle(null), 0);
        if (hook == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    internal static int? Digit(uint scan) => scan switch { 0x52 => 0, 0x4f => 1, 0x50 => 2, 0x51 => 3, 0x4b => 4, 0x4c => 5, 0x4d => 6, 0x47 => 7, 0x48 => 8, 0x49 => 9, _ => null };
    public void Dispose() => Native.UnhookWindowsHookEx(hook);
}
