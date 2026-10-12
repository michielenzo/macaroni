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
    internal TrayApplication()
    {
        Directory.CreateDirectory(directory);
        _ = dispatcher.Handle;
        bool firstRun = !File.Exists(ConfigPath);
        if (firstRun) File.Copy(System.IO.Path.Combine(AppContext.BaseDirectory, "config.example.json"), ConfigPath);
        enabled = !File.Exists(StatePath) || File.ReadAllText(StatePath).Trim() != "false";
        var menu = new ContextMenuStrip();
        menu.Items.Add(toggle);
        menu.Items.Add(dedicatedMenu);
        menu.Opening += (_, _) => PopulateDedicatedMenu();
        menu.Items.Add("Open configuration", null, (_, _) => Open(ConfigPath));
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
    private void LoadConfiguration()
    {
        try { configuration = Configuration.Parse(File.ReadAllText(ConfigPath)); }
        catch (Exception ex) { Report(new Exception("Configuration unchanged: " + ex.Message)); }
    }
    private async void Dispatch(int digit, bool control, nint window)
    {
        try
        {
            if (digit == 0) { if (!control) Toggle(); return; }
            var snapshot = configuration;
            if (enabled && snapshot.Mappings.TryGetValue(digit.ToString(), out var mapping))
            {
                if (control) Assign(digit.ToString(), mapping, window);
                else await actions.Execute(digit.ToString(), mapping, snapshot);
            }
        }
        catch (Exception ex) { if (!stopping) Report(ex); }
    }
    private void Assign(string key, Mapping mapping, nint window, WindowCandidate? expected = null)
    {
        try
        {
            if (!enabled) throw new Exception("Enable Macaroni before assigning a dedicated window.");
            if (!configuration.Mappings.TryGetValue(key, out var current) || !ReferenceEquals(current, mapping))
                throw new Exception("The configuration changed. Open the window picker again.");
            string title = actions.AssignDedicated(key, mapping, window, expected);
            tray.ShowBalloonTip(3500, "Macaroni", $"Key {key} now uses ‘{title}’.", ToolTipIcon.Info);
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
            foreach (var (key, mapping) in configuration.Mappings.OrderBy(pair => pair.Key))
            {
                if (mapping.Action != "application" || mapping.Selection != "dedicated") continue;
                var choices = actions.DedicatedChoices(key, mapping);
                string title = choices.FirstOrDefault(c => c.Current).Title ?? "Not assigned";
                var entry = new ToolStripMenuItem($"{key}: {title.Replace("&", "&&")}");
                entry.DropDownItems.Add(new ToolStripMenuItem($"Focus a window, then Ctrl + numpad {key}") { Enabled = false });
                var picker = new ToolStripMenuItem("Choose window…");
                foreach (var choice in choices)
                {
                    var option = new ToolStripMenuItem($"{choice.Title.Replace("&", "&&")} (PID {choice.Window.Pid}, window {choice.Window.Handle:X})") { Checked = choice.Current };
                    option.Click += (_, _) => Assign(key, mapping, choice.Window.Handle, choice.Window);
                    picker.DropDownItems.Add(option);
                }
                if (choices.Length == 0) picker.DropDownItems.Add(new ToolStripMenuItem("No matching windows open") { Enabled = false });
                entry.DropDownItems.Add(picker);
                dedicatedMenu.DropDownItems.Add(entry);
            }
            if (dedicatedMenu.DropDownItems.Count == 0) dedicatedMenu.DropDownItems.Add(new ToolStripMenuItem("No dedicated mappings configured") { Enabled = false });
        }
        catch (Exception ex) { Report(ex); }
    }
    private void Toggle()
    {
        enabled = !enabled;
        UpdateTray();
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
        tray.Text = enabled ? "Macaroni — enabled" : "Macaroni — disabled";
        tray.Icon = enabled ? SystemIcons.Application : SystemIcons.Warning;
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
    private void Report(Exception ex)
    {
        try { File.AppendAllText(System.IO.Path.Combine(directory, "macaroni.log"), $"{DateTimeOffset.Now:O} {ex}\n"); } catch { }
        tray.ShowBalloonTip(5000, "Macaroni", ex.Message, ToolTipIcon.Warning);
    }
    protected override void ExitThreadCore()
    {
        stopping = true;
        keyboard.Dispose(); watcher.Dispose(); reload.Dispose(); actions.Dispose();
        tray.Visible = false; tray.Dispose(); dispatcher.Dispose();
        base.ExitThreadCore();
    }
}
