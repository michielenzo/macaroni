using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Macaroni;

internal sealed class WindowActions : IDisposable
{
    internal Action<string>? Diagnostics { get; set; }
    internal static string WindowState(nint window) => $"hwnd=0x{window:X}, exists={Native.IsWindow(window)}, visible={Native.IsWindowVisible(window)}, minimized={Native.IsIconic(window)}, maximized={Native.IsZoomed(window)}, foreground=0x{Native.GetForegroundWindow():X}";
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
    internal Dictionary<string, nint> Reservations(string key, KeyBinding binding) => binding.Actions
        .Where(a => a.Action == "application" && a.Selection == "dedicated")
        .Select(a => (Id: WorkspaceRunner.ActionId(key, a), Choices: DedicatedChoices(WorkspaceRunner.ActionId(key, a), a)))
        .Where(p => p.Choices.Any(c => c.Current)).ToDictionary(p => p.Id, p => p.Choices.First(c => c.Current).Window.Handle);
    internal void ReleaseIfAssigned(string id, Mapping mapping, nint window)
    {
        if (dedicated.Current(Identity(id, mapping), DedicatedCandidates(mapping)) == window) dedicated.Clear(Identity(id, mapping));
    }
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
            throw new Exception($"Key {key} expects {(string.IsNullOrWhiteSpace(mapping.StartMenuName) ? mapping.Process : mapping.StartMenuName)}. Focus a matching application window and try again; the chosen window may have closed.");
        return Title(handle);
    }
    internal async Task Execute(string key, Mapping mapping, Configuration config)
    {
        var window = await ExecuteAction(key, mapping, config, new HashSet<nint>());
        if (mapping.Focus && window != 0) await Focus(window);
    }
    internal Task Focus(nint window) => ForegroundActivation.Focus(window, lifetime.Token);
    internal async Task<nint> ExecuteAction(string key, Mapping mapping, Configuration config, IReadOnlySet<nint> excluded)
    {
        if (!busy.Add(key)) return 0;
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
                    var terminal = StartInfo(config.Terminal.Executable, config.Terminal.Arguments.Concat(new[] { Expand(mapping.Executable) }).Concat(mapping.Arguments), mapping.WorkingDirectory);
                    using var process = Process.Start(terminal);
                    var window = await WaitFor(() => WindowClaims.Claim(AllWindows().FirstOrDefault(w => !before.Contains(w) && !excluded.Contains(w) && IsProcess(w, config.Terminal.Process)), excluded));
                    Place(window, mapping, config);
                    return window;
                }
                return 0;
            }
            nint target;
            if (mapping.Action == "folder")
            {
                string path = System.IO.Path.GetFullPath(Expand(mapping.Path));
                if (!Directory.Exists(path)) throw new Exception($"Folder does not exist: {path}");
                target = WindowClaims.Claim(FolderWindows(path).FirstOrDefault(w => !excluded.Contains(w)), excluded);
                if (target == 0)
                {
                    Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true })?.Dispose();
                    target = await WaitFor(() => WindowClaims.Claim(FolderWindows(path).FirstOrDefault(w => !excluded.Contains(w)), excluded));
                }
            }
            else
            {
                string identity = Identity(key, mapping);
                target = Find(mapping, identity, excluded);
                if (target == 0 || mapping.LaunchAlways)
                {
                    using var process = StartApplication(mapping);
                    // Single-instance editors forward file arguments to their
                    // existing window and then exit. Give that launcher time
                    // to hand off before selecting and placing the window.
                    if (target != 0 && process != null)
                    {
                        using var handoff = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                        handoff.CancelAfter(TimeSpan.FromSeconds(2));
                        try { await process.WaitForExitAsync(handoff.Token); }
                        catch (OperationCanceledException) when (!lifetime.IsCancellationRequested) { }
                    }
                    target = await WaitFor(() => Find(mapping, identity, excluded));
                }
            }
            Diagnostics?.Invoke($"Action {key}, layout={mapping.Layout}, before placement: {WindowState(target)}");
            Place(target, mapping, config);
            Diagnostics?.Invoke($"Action {key}, after placement: {WindowState(target)}, bounds={WindowPlacement.VisibleBounds(target)}");
            return target;
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
    private static InstalledApp ResolveApp(Mapping mapping) => mapping.Installed ??= AppNames.Resolve(mapping.StartMenuName, AppCatalog.Read());
    private static Process? StartApplication(Mapping mapping)
    {
        if (string.IsNullOrWhiteSpace(mapping.StartMenuName) || !string.IsNullOrWhiteSpace(mapping.Executable))
            return Process.Start(StartInfo(mapping.Executable, mapping.Arguments, mapping.WorkingDirectory));
        var app = ResolveApp(mapping);
        if (app.Launcher.StartsWith("shell:AppsFolder\\", StringComparison.OrdinalIgnoreCase))
        {
            if (mapping.Arguments.Length > 0) throw new Exception("Packaged app arguments require an explicit executable override.");
            return Process.Start(StartInfo("explorer.exe", [app.Launcher], mapping.WorkingDirectory));
        }
        // Launch the shortcut itself to retain Chrome app/profile arguments,
        // working directory and any other shell launch metadata.
        var start = StartInfo(app.Launcher, mapping.Arguments, mapping.WorkingDirectory);
        start.UseShellExecute = true;
        return Process.Start(start);
    }
    private nint Find(Mapping mapping, string identity, IReadOnlySet<nint> excluded)
    {
        if (mapping.Selection == "dedicated")
            return WindowClaims.Claim(dedicated.Select(identity, DedicatedCandidates(mapping).Where(w => !excluded.Contains(w.Handle))), excluded);
        return WindowClaims.Claim(Candidates(mapping).Where(w => !excluded.Contains(w) && (mapping.Selection != "title" || Title(w) == mapping.Title))
            .OrderByDescending(w => recent.GetValueOrDefault(w)).FirstOrDefault(), excluded);
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
    private static IEnumerable<nint> Candidates(Mapping m) => AllWindows().Where(w => MatchesApplication(w, m));
    internal static bool MatchesApplication(nint window, Mapping mapping)
    {
        if (string.IsNullOrWhiteSpace(mapping.StartMenuName)) return IsProcess(window, mapping.Process);
        var app = ResolveApp(mapping);
        string id = WindowAppIdentity.Read(window);
        if (app.StrictIdentity)
        {
            // Never fall back to chrome.exe for installed web apps: that could
            // adopt a browser or a completely different web app's window.
            return AppNames.IdentityMatches(app, id) && (string.IsNullOrWhiteSpace(mapping.Process) || IsProcess(window, mapping.Process));
        }
        string process = string.IsNullOrWhiteSpace(mapping.Process) ? app.Process : mapping.Process;
        if (!IsProcess(window, process)) return false;
        if (process.Equals("chrome", StringComparison.OrdinalIgnoreCase) && id.Contains("._crx_", StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
    private static bool IsProcess(nint window, string name)
    {
        try
        {
            // explorer.exe also owns the desktop and taskbar. Application
            // mappings must only adopt actual File Explorer folder windows.
            if (System.IO.Path.GetFileNameWithoutExtension(name).Equals("explorer", StringComparison.OrdinalIgnoreCase))
            {
                var className = new StringBuilder(256);
                Native.GetClassName(window, className, className.Capacity);
                if (className.ToString() is not ("CabinetWClass" or "ExploreWClass")) return false;
            }
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
    private static void Place(nint window, Mapping mapping, Configuration config)
    {
        var destination = MonitorCatalog.Resolve(mapping.Monitor, config);
        WindowPlacement.Apply(window, mapping.Layout, destination, activate: false);
    }
    public void Dispose()
    {
        lifetime.Cancel();
        Native.UnhookWinEvent(foregroundHook);
        lifetime.Dispose();
    }
}
