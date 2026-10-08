using System.Runtime.InteropServices;
using Macaroni;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
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
}
