using System.Runtime.InteropServices;

namespace Macaroni;

internal static class WindowPlacement
{
    [StructLayout(LayoutKind.Sequential)] private struct Placement
    {
        public uint Length, Flags, Show;
        public Point Min, Max;
        public Native.Rect Normal;
    }
    [DllImport("user32.dll")] private static extern bool GetWindowPlacement(nint window, ref Placement placement);
    [DllImport("user32.dll")] private static extern bool SetWindowPlacement(nint window, ref Placement placement);
    private static void Show(nint window, uint command, bool activate)
    {
        if (activate) { Native.ShowWindow(window, (int)command); return; }
        var placement = new Placement { Length = (uint)Marshal.SizeOf<Placement>() };
        if (!GetWindowPlacement(window, ref placement)) throw new Exception("Could not read window state.");
        placement.Show = command == 9 ? 4u : command;
        nint previous = Native.GetForegroundWindow();
        if (!SetWindowPlacement(window, ref placement)) throw new Exception("Could not change window state.");
        // Windows can activate a window while changing it to maximized even
        // through SetWindowPlacement. Preserve the user's active window; the
        // workspace runner handles the designated focus action separately.
        if (previous != 0 && previous != window && Native.IsWindow(previous) && Native.GetForegroundWindow() != previous)
            Native.SetForegroundWindow(previous);
    }
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, uint attribute, out Native.Rect value, int size);

    private static Rectangle Outer(nint window)
    {
        if (!Native.GetWindowRect(window, out var rect)) throw new Exception("Could not read the target window's position.");
        return Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
    }
    internal static Rectangle VisibleBounds(nint window)
    {
        // DWM's visible frame excludes invisible resize borders. Matching that
        // frame to the work area avoids gaps around tiled windows. The app must
        // be per-monitor DPI aware so both APIs use physical pixel coordinates.
        if (DwmGetWindowAttribute(window, 9, out var frame, Marshal.SizeOf<Native.Rect>()) == 0 && frame.Right > frame.Left && frame.Bottom > frame.Top)
            return Rectangle.FromLTRB(frame.Left, frame.Top, frame.Right, frame.Bottom);
        return Outer(window);
    }
    internal static bool Apply(nint window, string layout, Screen destination, bool activate = true)
    {
        if (layout == "maximize" && Native.IsZoomed(window) && !Native.IsIconic(window) && Screen.FromHandle(window).DeviceName == destination.DeviceName)
        {
            RaiseWithoutActivation(window);
            return false;
        }

        bool restored = Native.IsIconic(window) || Native.IsZoomed(window);
        if (restored) Show(window, 9, activate);
        var visible = VisibleBounds(window);
        var wanted = Layouts.Bounds(layout == "maximize" ? "preserve" : layout, destination.WorkingArea, visible.Size);
        bool changed = restored;
        // A move to a different DPI can change resize-border widths. Re-read
        // after the first move and correct once; never loop on app minimum sizes.
        for (int attempt = 0; attempt < 2 && !Layouts.Matches(visible, wanted); attempt++)
        {
            var outer = Outer(window);
            int x = wanted.Left - (visible.Left - outer.Left), y = wanted.Top - (visible.Top - outer.Top);
            int width = wanted.Width + outer.Width - visible.Width, height = wanted.Height + outer.Height - visible.Height;
            if (!Native.SetWindowPos(window, 0, x, y, width, height, 0x14)) throw new Exception("Windows refused to apply the window layout.");
            changed = true;
            visible = VisibleBounds(window);
        }
        if (layout == "maximize" && !Native.IsZoomed(window)) { Show(window, 3, activate); changed = true; }
        RaiseWithoutActivation(window);
        return changed;
    }
    private static void RaiseWithoutActivation(nint window)
    {
        // Correct geometry does not mean the window is visible to the user:
        // another application may cover it. Raise every workspace member even
        // on an otherwise unchanged layout, without moving/resizing or taking
        // keyboard focus (HWND_TOP; SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE).
        if (!Native.SetWindowPos(window, 0, 0, 0, 0, 0, 0x13))
            throw new Exception("Windows refused to bring the workspace window forward.");
        // Some shell windows ignore an ordinary HWND_TOP move without
        // activation. A temporary topmost transition raises them reliably;
        // immediately remove it, preserving pre-existing always-on-top state.
        if ((Native.GetWindowLong(window, -20) & 0x8) == 0)
        {
            if (!Native.SetWindowPos(window, -1, 0, 0, 0, 0, 0x13)) throw new Exception("Windows refused to raise the workspace window.");
            if (!Native.SetWindowPos(window, -2, 0, 0, 0, 0, 0x13)) throw new Exception("Windows refused to restore normal window stacking.");
        }
    }
    internal static void RaiseWorkspace(IReadOnlyList<nint> windows, nint focus)
    {
        // Once focus has settled, keep siblings together immediately behind the
        // focus window. Raising each independently during placement can leave
        // the previously active app interleaved between workspace members.
        foreach (nint window in windows.Where(w => w != focus && Native.IsWindow(w)))
            RaiseWithoutActivation(window);
        if (focus != 0 && Native.IsWindow(focus)) RaiseWithoutActivation(focus);
    }
}
