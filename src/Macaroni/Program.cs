using Microsoft.Win32;
using System.Diagnostics;

namespace Macaroni;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, "Local\\Macaroni.Tray", out bool first);
        if (!first) return;
        ApplicationConfiguration.Initialize();
        try { Application.Run(new TrayApplication()); }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Macaroni", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}

internal sealed class TrayApplication : ApplicationContext
{
    private readonly string directory = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Macaroni");
    private string ConfigPath => System.IO.Path.Combine(directory, "config.json");
    private string StatePath => System.IO.Path.Combine(directory, "enabled.txt");
    private readonly Control dispatcher = new();
    private readonly NotifyIcon tray;
    private readonly ToolStripMenuItem toggle = new();
    private readonly ToolStripMenuItem startup = new("Start with Windows");
    private readonly ToolStripMenuItem dedicatedMenu = new("Dedicated windows");
    private readonly System.Windows.Forms.Timer reload = new() { Interval = 350 };
    private readonly FileSystemWatcher watcher;
    private readonly WindowActions actions = new();
    private readonly KeyboardHook keyboard;
    private Configuration configuration = new();
    private bool enabled;
    private bool stopping;
    private int configurationRevision;
    private bool invalidConfigurationFile;
    private string[] invalidMappingKeys = [];
    internal TrayApplication()
    {
        Directory.CreateDirectory(directory);
        if (Environment.GetCommandLineArgs().Contains("--diagnostics"))
        {
            actions.Diagnostics = WriteDiagnostic;
            WriteDiagnostic("Diagnostic session started.");
        }
        _ = dispatcher.Handle;
        bool firstRun = !File.Exists(ConfigPath);
        if (firstRun) File.Copy(System.IO.Path.Combine(AppContext.BaseDirectory, "config.example.json"), ConfigPath);
        enabled = !File.Exists(StatePath) || File.ReadAllText(StatePath).Trim() != "false";
        var menu = new ContextMenuStrip();
        menu.Items.Add(toggle);
        menu.Items.Add(dedicatedMenu);
        menu.Opening += (_, _) => PopulateDedicatedMenu();
        menu.Items.Add("Open configuration", null, (_, _) => Open(ConfigPath));
        menu.Items.Add("List applications", null, (_, _) => ListApplications());
        menu.Items.Add("Reload configuration", null, (_, _) => LoadConfiguration());
        menu.Items.Add("Show monitor identities", null, (_, _) => ShowMonitors());
        menu.Items.Add(startup);
        menu.Items.Add("Open logs", null, (_, _) => Open(directory));
        menu.Items.Add("Exit", null, (_, _) => ExitThread());
        toggle.Click += (_, _) => Toggle();
        startup.Click += (_, _) => { try { SetStartup(!startup.Checked); } catch (Exception ex) { Report(ex); } };
        tray = new NotifyIcon { Visible = true, ContextMenuStrip = menu };
        tray.DoubleClick += (_, _) => Toggle();
        UpdateTray();
        LoadConfiguration();
        reload.Tick += (_, _) => { reload.Stop(); LoadConfiguration(); };
        watcher = new FileSystemWatcher(directory, "config.json") { NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size };
        watcher.Changed += Changed; watcher.Created += Changed; watcher.Deleted += Changed; watcher.Renamed += Changed;
        watcher.EnableRaisingEvents = true;
        keyboard = new KeyboardHook((d, ctrl) => ShortcutPolicy.Capture(d, ctrl, enabled, configuration), (d, ctrl, window) =>
            dispatcher.BeginInvoke((Action)(() => Dispatch(d, ctrl, window))));
        using var run = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        startup.Checked = run?.GetValue("Macaroni") != null;
        if (firstRun) { try { SetStartup(true); } catch (Exception ex) { Report(ex); } }
    }
    private void Changed(object sender, FileSystemEventArgs args)
    {
        if (stopping) return;
        try { dispatcher.BeginInvoke((Action)(() => { reload.Stop(); reload.Start(); })); }
        catch (InvalidOperationException) { }
    }
    private async void LoadConfiguration()
    {
        int revision = ++configurationRevision;
        try
        {
            var candidate = Configuration.Load(File.ReadAllText(ConfigPath));
            await AppCatalog.ResolveAsync(candidate);
            if (!stopping && revision == configurationRevision)
            {
                configuration = candidate;
                invalidConfigurationFile = false;
                invalidMappingKeys = candidate.Errors.Select(error => error.Split(" disabled:")[0].Replace("Key ", "")).ToArray();
                UpdateTray();
                if (candidate.Errors.Count > 0) Report(new Exception($"Loaded {candidate.Mappings.Count} working mappings. " + string.Join(" ", candidate.Errors)));
            }
        }
        catch (Exception ex)
        {
            if (!stopping && revision == configurationRevision)
            {
                invalidConfigurationFile = true;
                UpdateTray();
                Report(new Exception("Configuration unchanged: " + ex.Message));
            }
        }
    }
    private readonly WorkspaceRunner workspaces = new();
    private AssignmentPalette? palette;
    private async void Dispatch(int digit, bool control, nint window)
    {
        try
        {
            if (digit == 0) { if (!control) Toggle(notify: true); return; }
            var snapshot = configuration;
            string key = digit.ToString();
            if (!enabled || !snapshot.Mappings.TryGetValue(key, out var binding)) return;
            if (control) { AssignFocused(key, binding, window); return; }
            if (workspaces.IsRunning(key)) return;
            var reservations = actions.Reservations(key, binding);
            actions.Diagnostics?.Invoke($"Key {key}: {binding.Actions.Count} actions; reserved=" + string.Join(",", reservations.Select(p => $"{p.Key}=0x{p.Value:X}")));
            var failures = await workspaces.Run(key, binding, (id, action, used) => actions.ExecuteAction(id, action, snapshot, used), actions.Focus, reservations, w => Native.GetForegroundWindow() != w, WindowPlacement.RaiseWorkspace);
            if (actions.Diagnostics != null)
                foreach (var pair in actions.Reservations(key, binding)) actions.Diagnostics($"Key {key} completed, {pair.Key}: {WindowActions.WindowState(pair.Value)}");
            if (failures.Count > 0 && !stopping) Report(new Exception(string.Join("\n", failures)));
        }
        catch (OperationCanceledException) when (stopping) { }
        catch (Exception ex) { if (!stopping) Report(ex); }
    }
    private void AssignFocused(string key, KeyBinding binding, nint window)
    {
        if (workspaces.IsRunning(key)) throw new Exception($"Key {key} is still running. Try assigning again when it finishes.");
        var matches = ActionLabels.Assignable(binding, a => WindowActions.MatchesApplication(window, a));
        if (matches.Length == 0) throw new Exception($"The focused application does not match any dedicated action on key {key}.");
        var candidates = matches.Select(a => actions.DedicatedChoices(WorkspaceRunner.ActionId(key, a), a).FirstOrDefault(c => c.Window.Handle == window).Window).ToArray();
        if (matches.Length == 1) { Assign(key, binding, matches[0], window, candidates[0]); return; }
        var allLabels = ActionLabels.For(binding);
        var owners = actions.Reservations(key, binding);
        string? ownerId = owners.FirstOrDefault(p => p.Value == window).Key;
        string? ownerLabel = binding.Actions.FirstOrDefault(a => WorkspaceRunner.ActionId(key, a) == ownerId) is { } owner ? allLabels[binding.Actions.IndexOf(owner)] : null;
        var labels = matches.Select(a => allLabels[binding.Actions.IndexOf(a)] + (WorkspaceRunner.ActionId(key, a) == ownerId ? " · currently assigned here" : ownerLabel != null ? " · transfers window" : "")).ToArray();
        var title = new System.Text.StringBuilder(4096);
        Native.GetWindowText(window, title, title.Capacity);
        palette?.Close();
        string explanation = ownerLabel == null ? "Choose an action for this window. Assignments belong to this key only."
            : $"Assigned to {ownerLabel}. Choosing another action transfers it; the old action will use another window.";
        palette = new AssignmentPalette(title.ToString(), labels, Screen.FromHandle(window), index => Assign(key, binding, matches[index], window, candidates[index]), explanation);
        palette.Show();
    }
    private void Assign(string key, KeyBinding binding, Mapping mapping, nint window, WindowCandidate? expected = null)
    {
        try
        {
            if (!enabled) throw new Exception("Enable Macaroni before assigning a dedicated window.");
            if (workspaces.IsRunning(key)) throw new Exception($"Key {key} is still running. Try again when it finishes.");
            if (!configuration.Mappings.TryGetValue(key, out var current) || !ReferenceEquals(current, binding) || !binding.Actions.Contains(mapping))
                throw new Exception("The configuration changed. Open the window picker again.");
            string title = actions.AssignDedicated(WorkspaceRunner.ActionId(key, mapping), mapping, window, expected);
            foreach (var other in binding.Actions.Where(a => !ReferenceEquals(a, mapping) && a.Action == "application" && a.Selection == "dedicated"))
                actions.ReleaseIfAssigned(WorkspaceRunner.ActionId(key, other), other, window);
            string label = ActionLabels.For(binding)[binding.Actions.IndexOf(mapping)];
            tray.ShowBalloonTip(3500, "Macaroni", $"{label} now uses ‘{title}’.", ToolTipIcon.Info);
        }
        catch (Exception ex) { Report(ex); }
    }
    private void PopulateDedicatedMenu()
    {
        foreach (var item in dedicatedMenu.DropDownItems.Cast<ToolStripItem>().ToArray()) item.Dispose();
        dedicatedMenu.DropDownItems.Clear();
        dedicatedMenu.Enabled = enabled;
        try
        {
            foreach (var (key, binding) in configuration.Mappings.OrderBy(pair => pair.Key))
            {
                var keyEntry = new ToolStripMenuItem($"Key {key}");
                var labels = ActionLabels.For(binding);
                var owners = actions.Reservations(key, binding);
                for (int i = 0; i < binding.Actions.Count; i++)
                {
                    var mapping = binding.Actions[i];
                    if (mapping.Action != "application" || mapping.Selection != "dedicated") continue;
                    var choices = actions.DedicatedChoices(WorkspaceRunner.ActionId(key, mapping), mapping);
                    var entry = new ToolStripMenuItem(labels[i].Replace("&", "&&"));
                    entry.DropDownItems.Add(new ToolStripMenuItem("Current: " + (choices.FirstOrDefault(c => c.Current).Title ?? "Not assigned").Replace("&", "&&")) { Enabled = false });
                    var picker = new ToolStripMenuItem("Choose window…");
                    picker.DropDownItems.Add(new ToolStripMenuItem("Selecting transfers ownership within this key.") { Enabled = false });
                    foreach (var choice in choices)
                    {
                        string? ownerId = owners.FirstOrDefault(p => p.Value == choice.Window.Handle).Key;
                        var owner = binding.Actions.FirstOrDefault(a => WorkspaceRunner.ActionId(key, a) == ownerId);
                        string hint = choice.Current ? " — currently assigned here" : owner != null ? $" — transfer from {labels[binding.Actions.IndexOf(owner)]}" : " — unassigned";
                        var option = new ToolStripMenuItem(($"{choice.Title}{hint} (PID {choice.Window.Pid}, window {choice.Window.Handle:X})").Replace("&", "&&")) { Checked = choice.Current };
                        option.Click += (_, _) => Assign(key, binding, mapping, choice.Window.Handle, choice.Window);
                        picker.DropDownItems.Add(option);
                    }
                    if (choices.Length == 0) picker.DropDownItems.Add(new ToolStripMenuItem("No matching windows open") { Enabled = false });
                    entry.DropDownItems.Add(picker); keyEntry.DropDownItems.Add(entry);
                }
                if (keyEntry.DropDownItems.Count > 0) dedicatedMenu.DropDownItems.Add(keyEntry); else keyEntry.Dispose();
            }
            if (dedicatedMenu.DropDownItems.Count == 0) dedicatedMenu.DropDownItems.Add(new ToolStripMenuItem("No dedicated mappings configured") { Enabled = false });
        }
        catch (Exception ex) { Report(ex); }
    }
    private void Toggle(bool notify = false)
    {
        enabled = !enabled;
        UpdateTray();
        if (notify)
            tray.ShowBalloonTip(3500, enabled ? "Macaroni enabled" : "Macaroni disabled",
                enabled ? "Numpad shortcuts are active." : "Numpad shortcuts are paused. Press numpad 0 with Num Lock off to enable them again.",
                ToolTipIcon.Info);
        try
        {
            File.WriteAllText(StatePath + ".tmp", enabled ? "true" : "false");
            File.Move(StatePath + ".tmp", StatePath, true);
        }
        catch (Exception ex) { Report(ex); }
    }
    private void UpdateTray()
    {
        toggle.Text = enabled ? "Disable Macaroni" : "Enable Macaroni";
        string status = enabled ? "Macaroni — enabled" : "Macaroni — disabled";
        if (invalidConfigurationFile) status += " | Invalid configuration file";
        else if (invalidMappingKeys.Length > 0) status += " | Invalid keys: " + string.Join(", ", invalidMappingKeys);
        // Stay within the tooltip limit on older Windows/NotifyIcon versions.
        tray.Text = status.Length <= 63 ? status : status[..60] + "…";
        tray.Icon = invalidConfigurationFile || invalidMappingKeys.Length > 0
            ? SystemIcons.Error
            : enabled ? SystemIcons.Application : SystemIcons.Warning;
    }
    private void SetStartup(bool value)
    {
        using var run = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (value) run.SetValue("Macaroni", $"\"{Environment.ProcessPath}\"");
        else run.DeleteValue("Macaroni", false);
        startup.Checked = value;
    }
    private void Open(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose(); }
        catch (Exception ex) { Report(ex); }
    }
    private void ShowMonitors()
    {
        try
        {
            var numbered = MonitorCatalog.WindowsDisplays();
            var report = MonitorCatalog.Connected().Select(display => new
            {
                windowsNumbers = numbered.Where(n => n.Screen.DeviceName == display.Screen.DeviceName).Select(n => n.Number).ToArray(),
                deviceName = display.Screen.DeviceName,
                bounds = display.Screen.Bounds.ToString(),
                primary = display.Screen.Primary,
                identity = display.Identity
            });
            var path = System.IO.Path.Combine(directory, "monitors.json");
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(report, Configuration.JsonOptions));
            Open(path);
        }
        catch (Exception ex) { Report(ex); }
    }
    private async void ListApplications()
    {
        try
        {
            var catalog = await AppCatalog.ReadAsync();
            if (stopping) return;
            var path = System.IO.Path.Combine(directory, "applications.json");
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(catalog, Configuration.JsonOptions));
            Open(path);
        }
        catch (Exception ex) { if (!stopping) Report(ex); }
    }
    private void Report(Exception ex)
    {
        try { File.AppendAllText(System.IO.Path.Combine(directory, "macaroni.log"), $"{DateTimeOffset.Now:O} {ex}\n"); } catch { }
        tray.ShowBalloonTip(5000, "Macaroni", ex.Message, ToolTipIcon.Warning);
    }
    private void WriteDiagnostic(string message)
    {
        try
        {
            string path = System.IO.Path.Combine(directory, "diagnostics.log");
            if (File.Exists(path) && new FileInfo(path).Length > 262144) File.Move(path, path + ".previous", true);
            File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}\n");
        }
        catch { /* Diagnostics must never prevent a shortcut from running. */ }
    }
    protected override void ExitThreadCore()
    {
        stopping = true;
        palette?.Close(); palette?.Dispose();
        keyboard.Dispose(); watcher.Dispose(); reload.Dispose(); actions.Dispose();
        tray.Visible = false; tray.Dispose(); dispatcher.Dispose();
        base.ExitThreadCore();
    }
}
