using System.Runtime.InteropServices;
using Macaroni;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--monitors"))
        {
            foreach (var display in MonitorCatalog.WindowsDisplays())
                Console.WriteLine($"Windows number {display.Number}: {display.Screen.DeviceName} {display.Screen.Bounds}");
            foreach (var display in MonitorCatalog.Connected())
                Console.WriteLine($"{display.Screen.DeviceName} {display.Screen.Bounds}\n{display.Identity}");
            return;
        }
        using var dispatcher = new Control();
        _ = dispatcher.Handle;
        // Never intercept user input in a smoke test.
        using var hook = new KeyboardHook(_ => false, _ => throw new Exception("Unexpected captured input"));
        Console.WriteLine("PASS: install non-intercepting keyboard hook");
        int count = 0;
        if (!Native.EnumWindows((_, _) => { count++; return true; }, 0)) throw new Exception("EnumWindows failed");
        Console.WriteLine($"PASS: enumerate {count} desktop windows");
        foreach (var screen in Screen.AllScreens) Console.WriteLine($"PASS: display {screen.DeviceName} {screen.WorkingArea}");
        object shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!)!;
        object windows = ((dynamic)shell).Windows();
        Console.WriteLine($"PASS: Explorer automation accessible ({((dynamic)windows).Count} shell windows)");
        Marshal.FinalReleaseComObject(windows);
        Marshal.FinalReleaseComObject(shell);
        using var timer = new System.Windows.Forms.Timer { Interval = 250 };
        timer.Tick += async (_, _) =>
        {
            timer.Stop();
            try
            {
                if (args.Contains("--adopt-terminal"))
                {
                    var config = Configuration.Parse(File.ReadAllText(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Macaroni", "config.json")));
                    var mapping = config.Mappings["7"];
                    if (mapping.Process != "WindowsTerminal" || mapping.Selection != "dedicated") throw new Exception("Expected key 7 to be a dedicated Windows Terminal mapping.");
                    var before = TerminalWindows();
                    if (before.Count == 0) throw new Exception("Open a terminal first; this check must not launch one.");
                    using var actions = new WindowActions();
                    await actions.Execute("7", mapping, config);
                    var adopted = Native.GetForegroundWindow();
                    if (!before.Contains(adopted) || !before.SetEquals(TerminalWindows())) throw new Exception("Dedicated action did not adopt an existing terminal without launching another.");
                    await actions.Execute("7", mapping, config);
                    if (Native.GetForegroundWindow() != adopted || !before.SetEquals(TerminalWindows())) throw new Exception("Dedicated selection did not remain on the adopted terminal.");
                    Console.WriteLine("PASS: dedicated key 7 adopts and reuses an existing terminal without opening another window");
                }
                if (args.Contains("--place-chrome"))
                {
                    var config = Configuration.Parse(File.ReadAllText(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Macaroni", "config.json")));
                    var mapping = config.Mappings["4"];
                    if (mapping.Process != "chrome" || mapping.Monitor != 2) throw new Exception("Expected key 4 to select Chrome on monitor 2.");
                    using var actions = new WindowActions();
                    await actions.Execute("4", mapping, config);
                    var actual = Screen.FromHandle(Native.GetForegroundWindow());
                    var expected = MonitorCatalog.Resolve(2, config);
                    if (actual.DeviceName != expected.DeviceName) throw new Exception("Chrome is on the wrong monitor.");
                    Console.WriteLine($"PASS: Chrome focused on assigned monitor 2: {actual.DeviceName} {actual.Bounds}");
                }
                if (args.Contains("--focus-terminal"))
                {
                    var terminalIds = System.Diagnostics.Process.GetProcessesByName("WindowsTerminal")
                        .Select(p => { using (p) return (uint)p.Id; }).ToHashSet();
                    nint target = 0;
                    Native.EnumWindows((w, _) =>
                    {
                        Native.GetWindowThreadProcessId(w, out uint pid);
                        if (Native.IsWindowVisible(w) && terminalIds.Contains(pid)) { target = w; return false; }
                        return true;
                    }, 0);
                    if (target == 0) throw new Exception("No visible Windows Terminal window to test.");
                    await ForegroundActivation.Focus(target, CancellationToken.None);
                    if (Native.GetForegroundWindow() != target) throw new Exception("Terminal did not become foreground.");
                    Console.WriteLine("PASS: terminal actually became the foreground window");
                    await ForegroundActivation.Focus(target, CancellationToken.None);
                    Console.WriteLine("PASS: focusing the already active terminal succeeds");
                }
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally { Application.ExitThread(); }
        };
        timer.Start();
        Application.Run();
        Console.WriteLine("PASS: Windows message loop completed");
    }
    private static HashSet<nint> TerminalWindows()
    {
        var ids = System.Diagnostics.Process.GetProcessesByName("WindowsTerminal").Select(p => { using (p) return (uint)p.Id; }).ToHashSet();
        var windows = new HashSet<nint>();
        Native.EnumWindows((window, _) =>
        {
            Native.GetWindowThreadProcessId(window, out uint pid);
            if (Native.IsWindowVisible(window) && ids.Contains(pid)) windows.Add(window);
            return true;
        }, 0);
        return windows;
    }
}
