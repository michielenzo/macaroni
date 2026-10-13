using System.Runtime.InteropServices;
using System.Text;

namespace Macaroni;

internal static class WindowAppIdentity
{
    [StructLayout(LayoutKind.Sequential)] private struct PropertyKey { public Guid Format; public uint Id; }
    [StructLayout(LayoutKind.Explicit, Size = 24)] private struct Variant { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public nint Text; }
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out Variant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref Variant value);
        [PreserveSig] int Commit();
    }
    [DllImport("shell32.dll")] private static extern int SHGetPropertyStoreForWindow(nint window, ref Guid iid, out IPropertyStore store);
    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref Variant value);
    [DllImport("kernel32.dll")] private static extern nint OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern int GetApplicationUserModelId(nint process, ref uint length, StringBuilder? id);

    internal static string Read(nint window)
    {
        IPropertyStore? store = null;
        var iid = typeof(IPropertyStore).GUID;
        try
        {
            if (SHGetPropertyStoreForWindow(window, ref iid, out store) == 0)
            {
                var key = new PropertyKey { Format = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), Id = 5 };
                if (store.GetValue(ref key, out var value) == 0)
                {
                    try { if (value.Type == 31 && value.Text != 0) return Marshal.PtrToStringUni(value.Text) ?? ""; }
                    finally { PropVariantClear(ref value); }
                }
            }
        }
        catch (COMException) { }
        finally { if (store != null) Marshal.FinalReleaseComObject(store); }
        Native.GetWindowThreadProcessId(window, out uint pid);
        nint process = OpenProcess(0x1000, false, pid);
        if (process == 0) return "";
        try
        {
            uint size = 0;
            if (GetApplicationUserModelId(process, ref size, null) != 122 || size == 0) return "";
            var id = new StringBuilder((int)size);
            return GetApplicationUserModelId(process, ref size, id) == 0 ? id.ToString() : "";
        }
        finally { CloseHandle(process); }
    }
}
