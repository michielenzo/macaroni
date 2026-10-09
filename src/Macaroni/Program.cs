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
        keyboard = new KeyboardHook(d => d == 0 || enabled && configuration.Mappings.ContainsKey(d.ToString()), d =>
            dispatcher.BeginInvoke((Action)(() => Dispatch(d))));
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
    private async void Dispatch(int digit)
    {
        try
        {
            if (digit == 0) { Toggle(); return; }
            var snapshot = configuration;
            if (enabled && snapshot.Mappings.TryGetValue(digit.ToString(), out var mapping)) await actions.Execute(digit.ToString(), mapping, snapshot);
        }
        catch (Exception ex) { if (!stopping) Report(ex); }
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
