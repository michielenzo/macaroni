using System.Runtime.InteropServices;

namespace Macaroni;

internal static class WindowPlacement
{
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
    internal static bool Apply(nint window, string layout, Screen destination)
    {
        if (layout == "maximize" && Native.IsZoomed(window) && !Native.IsIconic(window) && Screen.FromHandle(window).DeviceName == destination.DeviceName)
            return false;

        bool restored = Native.IsIconic(window) || Native.IsZoomed(window);
        if (restored) Native.ShowWindow(window, 9);
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
        if (layout == "maximize" && !Native.IsZoomed(window)) { Native.ShowWindow(window, 3); changed = true; }
        return changed;
    }
}
