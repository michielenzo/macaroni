using System.Runtime.InteropServices;
using System.ComponentModel;

namespace Macaroni;

internal static class MonitorCatalog
{
    [StructLayout(LayoutKind.Sequential)] private struct Luid { public uint Low; public int High; }
    [StructLayout(LayoutKind.Sequential)] private struct Source { public Luid Adapter; public uint Id, ModeIndex, Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct Target
    {
        public Luid Adapter;
        public uint Id, ModeIndex, Technology, Rotation, Scaling, RefreshNumerator, RefreshDenominator, ScanLineOrdering;
        public int Available;
        public uint Flags;
    }
    [StructLayout(LayoutKind.Sequential)] private struct DisplayPath { public Source Source; public Target Target; public uint Flags; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct SourceName
    {
        public uint Type, Size;
        public Luid Adapter;
        public uint Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
    }
    [DllImport("user32.dll")] private static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);
    [DllImport("user32.dll")] private static extern int QueryDisplayConfig(uint flags, ref uint paths, [Out] DisplayPath[] pathArray, ref uint modes, nint modeArray, nint topology);
    [DllImport("user32.dll")] private static extern int DisplayConfigGetDeviceInfo(ref SourceName name);

    internal static (int Number, Screen Screen)[] WindowsDisplays()
    {
        // Use active display-path order, as PowerToys Power Display does for
        // Windows Settings' Identify numbers. GDI DISPLAY suffixes are not IDs
        // from Settings. Re-query every action so topology changes take effect.
        const uint activePaths = 2;
        for (int attempt = 0; attempt < 5; attempt++)
        {
            int error = GetDisplayConfigBufferSizes(activePaths, out uint pathCount, out uint modeCount);
            if (error != 0) throw new Win32Exception(error, "Could not read Windows display configuration.");
            var paths = new DisplayPath[pathCount];
            // DISPLAYCONFIG_MODE_INFO is a 16-byte header plus its 48-byte
            // mode union. We only need paths, but Windows requires this buffer.
            nint modes = Marshal.AllocHGlobal(checked((int)modeCount * 64));
            try
            {
                error = QueryDisplayConfig(activePaths, ref pathCount, paths, ref modeCount, modes, 0);
                if (error == 122) continue; // A monitor changed between size/query calls.
                if (error != 0) throw new Win32Exception(error, "Could not read active display paths.");
                var screens = Screen.AllScreens;
                var names = new List<string>();
                for (int i = 0; i < pathCount; i++)
                {
                    var source = new SourceName { Type = 1, Size = (uint)Marshal.SizeOf<SourceName>(), Adapter = paths[i].Source.Adapter, Id = paths[i].Source.Id };
                    error = DisplayConfigGetDeviceInfo(ref source);
                    if (error != 0) throw new Win32Exception(error, "Could not resolve a Windows display path.");
                    names.Add(source.Name);
                }
                var numbered = MonitorAssignments.NumberPaths(names);
                if (numbered.Any(pair => !screens.Any(s => s.DeviceName.Equals(pair.Value, StringComparison.OrdinalIgnoreCase)))) continue;
                if (numbered.Count == 0) throw new Exception("Windows reported no active displays.");
                return numbered.Select(pair => (pair.Key, screens.First(s => s.DeviceName.Equals(pair.Value, StringComparison.OrdinalIgnoreCase)))).ToArray();
            }
            finally { Marshal.FreeHGlobal(modes); }
        }
        throw new Exception("The display layout changed while it was being read. Try the shortcut again.");
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Device
    {
        public int Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Key;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevices(string name, uint index, ref Device device, uint flags);

    internal static (Screen Screen, string Identity)[] Connected() => Screen.AllScreens.Select(screen =>
    {
        // EDD_GET_DEVICE_INTERFACE_NAME returns the monitor interface identity,
        // rather than the GDI DISPLAY number which changes after docking.
        var device = new Device { Size = Marshal.SizeOf<Device>() };
        if (!EnumDisplayDevices(screen.DeviceName, 0, ref device, 1) || string.IsNullOrWhiteSpace(device.Id))
            throw new Exception($"Could not identify monitor {screen.DeviceName}.");
        return (screen, device.Id);
    }).ToArray();

    internal static Screen Resolve(int requested, Configuration config)
    {
        if (config.Monitors.Count == 0)
        {
            var displays = WindowsDisplays();
            int windowsNumber = Configuration.ResolveMonitor(requested, displays.Select(s => s.Number));
            return displays.First(s => s.Number == windowsNumber).Screen;
        }
        var connected = Connected();
        var numbers = MonitorAssignments.Available(config.Monitors, connected.Select(s => s.Identity));
        if (numbers.Count == 0)
            throw new Exception("None of the configured monitors is connected. Update the monitors assignments; no window was moved.");
        int number = Configuration.ResolveMonitor(requested, numbers.Keys);
        return connected.First(s => string.Equals(s.Identity, numbers[number], StringComparison.OrdinalIgnoreCase)).Screen;
    }
}
