using System.Text;

namespace Macaroni;

internal static class TaskbarPreview
{
    internal static string ClassName(nint window)
    {
        var name = new StringBuilder(256);
        Native.GetClassName(window, name, name.Capacity);
        return name.ToString();
    }

    internal static bool IsForegroundPreview(nint window) => FindTaskbar(window) != 0;

    internal static nint FindTaskbar(nint window)
    {
        if (window == 0 || Native.GetForegroundWindow() != window || !Native.IsWindowVisible(window)) return 0;
        string name = ClassName(window);
        if (!TaskbarPreviewPolicy.IsPreviewClass(name)) return 0;
        uint thread = Native.GetWindowThreadProcessId(window, out uint process);
        nint found = 0;
        // Modern previews share a generic XAML host class with unrelated shell
        // UI. Require the actual taskbar's process AND UI thread, not explorer.exe
        // or a class name alone. Re-enumerate so Explorer restarts are supported.
        Native.EnumWindows((candidate, _) =>
        {
            string taskbarClass = ClassName(candidate);
            if (taskbarClass is not ("Shell_TrayWnd" or "Shell_SecondaryTrayWnd")) return true;
            uint taskbarThread = Native.GetWindowThreadProcessId(candidate, out uint taskbarProcess);
            if (TaskbarPreviewPolicy.Matches(name, process, thread, taskbarProcess, taskbarThread)) found = candidate;
            return found == 0;
        }, 0);
        return Native.GetForegroundWindow() == window && Native.IsWindowVisible(window) ? found : 0;
    }
}
