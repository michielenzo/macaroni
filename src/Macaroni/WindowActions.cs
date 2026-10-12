using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Macaroni;

internal sealed class WindowActions : IDisposable
{
    private readonly Dictionary<nint, long> recent = new();
    private readonly DedicatedWindows dedicated = new();
    private readonly HashSet<string> busy = new();
    private readonly Native.EventProc foregroundCallback;
    private readonly nint foregroundHook;
    private readonly CancellationTokenSource lifetime = new();
    internal WindowActions()
    {
        foregroundCallback = (_, _, window, _, _, _, _) => recent[window] = Environment.TickCount64;
        foregroundHook = Native.SetWinEventHook(3, 3, 0, foregroundCallback, 0, 0, 0);
        recent[Native.GetForegroundWindow()] = Environment.TickCount64;
    }
    private static string Expand(string value) => Environment.ExpandEnvironmentVariables(value);
    private static string Identity(string key, Mapping mapping) => key + JsonSerializer.Serialize(mapping);
    internal (WindowCandidate Window, string Title, bool Current)[] DedicatedChoices(string key, Mapping mapping)
    {
        var candidates = DedicatedCandidates(mapping);
        var current = dedicated.Current(Identity(key, mapping), candidates);
        return candidates.OrderByDescending(w => w.LastFocused).Select(w => (w, Title(w.Handle), w.Handle == current)).ToArray();
    }
    internal string AssignDedicated(string key, Mapping mapping, nint handle, WindowCandidate? expected = null)
    {
        if (mapping.Action != "application" || mapping.Selection != "dedicated") throw new Exception($"Key {key} is not a dedicated application mapping.");
        if (busy.Contains(key)) throw new Exception($"Key {key} is still opening a window. Try assigning it again once that finishes.");
        var candidates = DedicatedCandidates(mapping);
        var chosen = expected ?? candidates.FirstOrDefault(w => w.Handle == handle);
        if (chosen.Handle == 0 || chosen.Handle != handle || !dedicated.Assign(Identity(key, mapping), chosen, candidates))
            throw new Exception($"Key {key} expects {mapping.Process}. Focus a matching application window and try again; the chosen window may have closed.");
        return Title(handle);
    }
    internal async Task Execute(string key, Mapping mapping, Configuration config)
    {
        if (!busy.Add(key)) return;
        try
        {
            if (mapping.Action == "command")
            {
                var command = StartInfo(mapping.Executable, mapping.Arguments, mapping.WorkingDirectory);
                if (mapping.Mode == "hidden")
                {
                    command.CreateNoWindow = true;
                    command.WindowStyle = ProcessWindowStyle.Hidden;
                    using var process = Process.Start(command);
                }
                else
                {
                    var before = AllWindows().ToHashSet();
                    var terminal = StartInfo(config.Terminal, config.TerminalArguments.Concat(new[] { Expand(mapping.Executable) }).Concat(mapping.Arguments), mapping.WorkingDirectory);
                    using var process = Process.Start(terminal);
                    var window = await WaitFor(() => AllWindows().FirstOrDefault(w => !before.Contains(w) && IsProcess(w, config.TerminalProcess)));
                    await Place(window, mapping, config);
                }
                return;
            }
            nint target;
            if (mapping.Action == "folder")
            {
                string path = System.IO.Path.GetFullPath(Expand(mapping.Path));
                if (!Directory.Exists(path)) throw new Exception($"Folder does not exist: {path}");
                target = FolderWindows(path).FirstOrDefault();
                if (target == 0)
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true })?.Dispose();
                    target = await WaitFor(() => FolderWindows(path).FirstOrDefault());
                }
            }
            else
            {
                string identity = Identity(key, mapping);
                target = Find(mapping, identity);
                if (target == 0)
                {
                    using var process = Process.Start(StartInfo(mapping.Executable, mapping.Arguments, mapping.WorkingDirectory));
                    target = await WaitFor(() => Find(mapping, identity));
                }
            }
            await Place(target, mapping, config);
        }
        finally { busy.Remove(key); }
    }
    private static ProcessStartInfo StartInfo(string executable, IEnumerable<string> args, string directory)
    {
        var start = new ProcessStartInfo(Expand(executable)) { UseShellExecute = false };
        foreach (var arg in args) start.ArgumentList.Add(Expand(arg));
        if (!string.IsNullOrWhiteSpace(directory)) start.WorkingDirectory = Expand(directory);
        return start;
    }
    private nint Find(Mapping mapping, string identity)
    {
        if (mapping.Selection == "dedicated")
            return dedicated.Select(identity, DedicatedCandidates(mapping));
        return Candidates(mapping).Where(w => mapping.Selection != "title" || Title(w) == mapping.Title)
            .OrderByDescending(w => recent.GetValueOrDefault(w)).FirstOrDefault();
    }
    private List<WindowCandidate> DedicatedCandidates(Mapping mapping)
    {
            var available = new List<WindowCandidate>();
            foreach (var window in Candidates(mapping))
            {
                try
                {
                    Native.GetWindowThreadProcessId(window, out var pid);
                    using var owner = Process.GetProcessById((int)pid);
                    available.Add(new(window, pid, owner.StartTime.ToUniversalTime().Ticks, recent.GetValueOrDefault(window)));
                }
                // A candidate may close while its process identity is read.
                catch (ArgumentException) { }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
            }
            return available;
    }
    private static IEnumerable<nint> Candidates(Mapping m) => AllWindows().Where(w => IsProcess(w, m.Process));
    private static bool IsProcess(nint window, string name)
    {
        try
        {
            Native.GetWindowThreadProcessId(window, out var pid);
            using var process = Process.GetProcessById((int)pid);
            return string.Equals(process.ProcessName, System.IO.Path.GetFileNameWithoutExtension(name), StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }
    private static List<nint> AllWindows()
    {
        var windows = new List<nint>();
        Native.EnumWindows((w, _) => { if (Native.IsWindowVisible(w) && Title(w).Length > 0) windows.Add(w); return true; }, 0);
        return windows;
    }
    private static string Title(nint window)
    {
        var text = new StringBuilder(32768);
        Native.GetWindowText(window, text, text.Capacity);
        return text.ToString();
    }
    private IEnumerable<nint> FolderWindows(string path)
    {
        var result = new List<nint>();
        object? shell = null, windows = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!);
            windows = ((dynamic)shell!).Windows();
            for (int i = 0; i < (int)((dynamic)windows).Count; i++)
            {
                object? item = null;
                try
                {
                    item = ((dynamic)windows).Item(i);
                    string location = (string)((dynamic)item!).LocationURL;
                    if (Uri.TryCreate(location, UriKind.Absolute, out var uri) && uri.IsFile &&
                        string.Equals(System.IO.Path.GetFullPath(uri.LocalPath).TrimEnd('\\'), path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                        result.Add((nint)(long)((dynamic)item).HWND);
                }
                catch (COMException) { }
                finally { if (item != null && Marshal.IsComObject(item)) Marshal.FinalReleaseComObject(item); }
            }
        }
        finally
        {
            if (windows != null && Marshal.IsComObject(windows)) Marshal.FinalReleaseComObject(windows);
            if (shell != null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
        }
        return result.OrderByDescending(w => recent.GetValueOrDefault(w));
    }
    private async Task<nint> WaitFor(Func<nint> find)
    {
        var timeout = Stopwatch.StartNew();
        while (timeout.Elapsed < TimeSpan.FromSeconds(15))
        {
            var window = find();
            if (window != 0) return window;
            await Task.Delay(100, lifetime.Token);
        }
        throw new Exception("No matching window appeared within 15 seconds. Check process, title, and launch arguments; dedicated actions require a new window.");
    }
    private async Task Place(nint window, Mapping mapping, Configuration config)
    {
        var destination = MonitorCatalog.Resolve(mapping.Monitor, config);
        WindowPlacement.Apply(window, mapping.Layout, destination);
        await ForegroundActivation.Focus(window, lifetime.Token);
    }
    public void Dispose()
    {
        lifetime.Cancel();
        Native.UnhookWinEvent(foregroundHook);
        lifetime.Dispose();
    }
}
