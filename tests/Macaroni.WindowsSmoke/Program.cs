using System.Runtime.InteropServices;
using Macaroni;

internal static class Program
{
    [DllImport("user32.dll")] private static extern bool PostMessage(nint window, uint message, nint wParam, nint lParam);
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--explorer-restore"))
        {
            using var source = new Form { Text = "Macaroni Explorer restore test" };
            var explorerWindows = new List<nint>();
            string root = Path.Combine(Path.GetTempPath(), "Macaroni-restore-" + Guid.NewGuid());
            source.Shown += async (_, _) =>
            {
                try
                {
                    using var actions = new WindowActions();
                    var config = new Configuration();
                    var binding = new KeyBinding();
                    var layouts = new[] { "top-left", "top-right", "bottom-left", "bottom-right" };
                    int monitor = MonitorCatalog.WindowsDisplays().First(d => d.Screen.DeviceName == Screen.FromHandle(source.Handle).DeviceName).Number;
                    for (int i = 0; i < 4; i++)
                    {
                        string folder = Path.Combine(root, i.ToString()); Directory.CreateDirectory(folder);
                        nint window = await actions.ExecuteAction("setup/" + i, new Mapping { Action = "folder", Path = folder, Monitor = monitor, Layout = layouts[i] }, config, new HashSet<nint>());
                        explorerWindows.Add(window);
                        var mapping = new Mapping { Action = "application", Executable = "explorer.exe", Process = "explorer", Selection = "dedicated", Monitor = monitor, Layout = layouts[i], Position = i, Focus = i == 1 };
                        binding.Actions.Add(mapping);
                        actions.AssignDedicated("1/" + i, mapping, window);
                    }
                    var runner = new WorkspaceRunner();
                    foreach (nint window in explorerWindows) Native.ShowWindow(window, 6);
                    source.Activate(); await Task.Delay(250);
                    var allReservations = actions.Reservations("1", binding);
                    var allErrors = await runner.Run("1", binding, (id, a, excluded) => actions.ExecuteAction(id, a, config, excluded), actions.Focus, allReservations, w => Native.GetForegroundWindow() != w, WindowPlacement.RaiseWorkspace);
                    await Task.Delay(300);
                    if (allErrors.Count > 0 || explorerWindows.Any(Native.IsIconic)) throw new Exception("Restoring all Explorer windows failed: " + string.Join(" ", allErrors) + "; minimized=" + string.Join(",", explorerWindows.Select(Native.IsIconic)));
                    var stacking = new List<nint>();
                    Native.EnumWindows((w, _) => { if (w == source.Handle || explorerWindows.Contains(w)) stacking.Add(w); return true; }, 0);
                    if (stacking.Last() != source.Handle) throw new Exception("Restored workspace windows are still behind the previously active application. Order: " + string.Join(",", stacking.Select(w => w == source.Handle ? "source" : "explorer-" + explorerWindows.IndexOf(w))));
                    Console.WriteLine("PASS: all four minimized Explorer windows restore together");
                    // A workspace must also appear above the previous app when
                    // its windows already have the correct geometry.
                    source.Activate(); await Task.Delay(100);
                    var repeatErrors = await runner.Run("1", binding, (id, a, excluded) => actions.ExecuteAction(id, a, config, excluded), actions.Focus, actions.Reservations("1", binding), w => Native.GetForegroundWindow() != w, WindowPlacement.RaiseWorkspace);
                    await Task.Delay(300);
                    stacking.Clear();
                    Native.EnumWindows((w, _) => { if (w == source.Handle || explorerWindows.Contains(w)) stacking.Add(w); return true; }, 0);
                    if (repeatErrors.Count > 0 || stacking.Last() != source.Handle) throw new Exception("Already placed workspace stays behind previous app: " + string.Join(",", stacking.Select(w => w == source.Handle ? "source" : "explorer-" + explorerWindows.IndexOf(w))));
                    if (Native.GetForegroundWindow() != explorerWindows[1]) throw new Exception("Raising siblings changed designated focus.");
                    if (explorerWindows.Any(w => (Native.GetWindowLong(w, -20) & 0x8) != 0)) throw new Exception("Raising workspace left an Explorer window always on top.");
                    Console.WriteLine("PASS: already placed workspace raised above previous app without changing designated focus");
                    for (int i = 0; i < explorerWindows.Count; i++)
                    {
                        Native.ShowWindow(explorerWindows[i], 6); source.Activate(); await Task.Delay(250);
                        if (!Native.IsIconic(explorerWindows[i])) throw new Exception("Explorer did not minimize.");
                        var reservations = actions.Reservations("1", binding);
                        if (reservations.GetValueOrDefault("1/" + i) != explorerWindows[i]) throw new Exception("Minimized Explorer lost dedicated identity.");
                        var errors = await runner.Run("1", binding, (id, a, excluded) => actions.ExecuteAction(id, a, config, excluded), actions.Focus, reservations, w => Native.GetForegroundWindow() != w, WindowPlacement.RaiseWorkspace);
                        await Task.Delay(300);
                        if (errors.Count > 0 || Native.IsIconic(explorerWindows[i])) throw new Exception($"Explorer {i} did not restore: " + string.Join(" ", errors));
                        var after = actions.Reservations("1", binding);
                        if (reservations.Any(p => after.GetValueOrDefault(p.Key) != p.Value)) throw new Exception("Explorer assignments changed.");
                        if (!Layouts.Matches(WindowPlacement.VisibleBounds(explorerWindows[i]), Layouts.Bounds(layouts[i], Screen.FromHandle(source.Handle).WorkingArea, Size.Empty))) throw new Exception("Restored Explorer has wrong layout.");
                        Console.WriteLine($"PASS: Explorer corner {i} restores and retains dedicated assignment");
                    }
                }
                catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
                finally
                {
                    foreach (var window in explorerWindows) PostMessage(window, 0x10, 0, 0);
                    // These uniquely named test directories contain no user data.
                    // Delete only empty directories; never recursively remove files.
                    if (Directory.Exists(root))
                    {
                        foreach (string folder in Directory.GetDirectories(root))
                            try { Directory.Delete(folder); } catch (IOException) { }
                        try { Directory.Delete(root); } catch (IOException) { }
                    }
                    source.Close();
                }
            };
            Application.Run(source); return;
        }
        if (args.Length == 2 && args[0] == "--palette-render")
        {
            using var palette = new AssignmentPalette("Example terminal window", ["Project terminal — monitor 2, left · focus · currently assigned here", "Logs terminal — monitor 2, right · transfers window"], Screen.PrimaryScreen!, _ => { }, "Assigned to Project terminal. Choosing another action transfers it; the old action will use another window.");
            palette.Show(); Application.DoEvents();
            using var bitmap = new Bitmap(palette.Width, palette.Height);
            palette.DrawToBitmap(bitmap, new Rectangle(Point.Empty, palette.Size));
            bitmap.Save(args[1]); palette.Close();
            Console.WriteLine("PASS: assignment palette rendered");
            return;
        }
        if (args.Contains("--workspace-test"))
        {
            using var source = new Form { Text="Macaroni source window", Width=400, Height=240 };
            using var left = new Form { Text="Macaroni left test window" };
            using var right = new Form { Text="Macaroni right test window" };
            source.Shown += async (_, _) =>
            {
                try
                {
                    left.Show(); right.Show(); source.Activate(); await Task.Delay(100);
                    var binding = new KeyBinding { Actions = [new Mapping {Action="folder", Path="C:/", Layout="left", Position=0}, new Mapping {Action="folder", Path="D:/", Layout="right", Position=1, Focus=true}] };
                    var runner = new WorkspaceRunner();
                    foreach (bool focus in new[] {true,false})
                    {
                        binding.Actions[1].Focus=focus;
                        source.Activate(); await Task.Delay(100);
                        var errors = await runner.Run("1",binding,async (id,action,used)=>
                        {
                            var form=action.Position==0 ? left : right;
                            nint previous=Native.GetForegroundWindow();
                            WindowPlacement.Apply(form.Handle,action.Layout,Screen.FromHandle(source.Handle),activate:false);
                            if (Native.GetForegroundWindow()!=previous) throw new Exception("Placement stole focus.");
                            await Task.Delay(100);
                            return form.Handle;
                        },window=>ForegroundActivation.Focus(window,CancellationToken.None));
                        if(errors.Count>0) throw new Exception(string.Join(" ",errors));
                        if(Native.GetForegroundWindow()!=(focus?right.Handle:source.Handle)) throw new Exception("Wrong final workspace focus.");
                        Console.WriteLine($"PASS: two real windows tiled; explicit focus={focus} respected");
                    }
                    source.Activate(); await Task.Delay(100);
                    WindowPlacement.Apply(left.Handle,"maximize",Screen.FromHandle(source.Handle),activate:false);
                    await Task.Delay(100);
                    if(Native.GetForegroundWindow()!=source.Handle) throw new Exception("Maximize stole focus.");
                    Console.WriteLine("PASS: maximization without focus preserves the active window");
                    source.Activate(); await Task.Delay(100);
                    var processName = System.Diagnostics.Process.GetCurrentProcess().ProcessName + ".exe";
                    int monitor = MonitorCatalog.WindowsDisplays().First(d=>d.Screen.DeviceName==Screen.FromHandle(source.Handle).DeviceName).Number;
                    var dedicatedBinding = new KeyBinding {Actions=[
                        new Mapping {Action="application",Executable=Environment.ProcessPath!,Process=processName,Selection="dedicated",Layout="left",Monitor=monitor,Position=0,Focus=true},
                        new Mapping {Action="application",Executable=Environment.ProcessPath!,Process=processName,Selection="dedicated",Layout="right",Monitor=monitor,Position=1}]};
                    var config = new Configuration();
                    using var actualActions = new WindowActions();
                    var reservations = new Dictionary<string,nint> {["source"]=source.Handle};
                    var windows = new List<nint>();
                    var dedicatedErrors = await runner.Run("2",dedicatedBinding, async (id,action,used)=>
                    {
                        var window=await actualActions.ExecuteAction(id,action,config,used); windows.Add(window);return window;
                    },actualActions.Focus,reservations);
                    if(dedicatedErrors.Count>0 || windows.Distinct().Count()!=2 || windows.Contains(source.Handle)) throw new Exception("Dedicated group did not select two distinct existing windows: "+string.Join(" ",dedicatedErrors));
                    var remembered=actualActions.Reservations("2",dedicatedBinding);
                    if(remembered.Count!=2) throw new Exception("Dedicated assignments were not tracked independently.");
                    var adoptedFirst=windows[0];windows.Clear();
                    remembered["source"]=source.Handle;
                    await runner.Run("2",dedicatedBinding,async(id,action,used)=>{var window=await actualActions.ExecuteAction(id,action,config,used);windows.Add(window);return window;},actualActions.Focus,remembered);
                    if(windows.Count!=2 || windows[0]!=adoptedFirst || windows.Distinct().Count()!=2) throw new Exception("Repeated dedicated workspace did not retain both assignments.");
                    Console.WriteLine("PASS: two same-application dedicated actions adopt distinct existing windows and retain both assignments");
                    foreach (var minimized in new[] { "2/0", "2/1" })
                    {
                        var beforeMinimize = actualActions.Reservations("2", dedicatedBinding);
                        nint target = beforeMinimize[minimized];
                        Native.ShowWindow(target, 6);
                        source.Activate(); await Task.Delay(150);
                        if (!Native.IsIconic(target)) throw new Exception("Test window did not minimize.");
                        var restoreReservations = actualActions.Reservations("2", dedicatedBinding);
                        if (!restoreReservations.TryGetValue(minimized, out var reservedTarget) || reservedTarget != target)
                            throw new Exception("Minimized dedicated window lost its assignment.");
                        restoreReservations["source"] = source.Handle;
                        var restoreErrors = await runner.Run("2", dedicatedBinding,
                            (id, action, used) => actualActions.ExecuteAction(id, action, config, used), actualActions.Focus, restoreReservations);
                        await Task.Delay(150);
                        if (restoreErrors.Count > 0 || Native.IsIconic(target)) throw new Exception("Dedicated window remained minimized: " + string.Join(" ", restoreErrors));
                        var afterRestore = actualActions.Reservations("2", dedicatedBinding);
                        if (beforeMinimize.Any(p => afterRestore.GetValueOrDefault(p.Key) != p.Value)) throw new Exception("Restoring changed dedicated assignments.");
                        Console.WriteLine($"PASS: minimized dedicated window {minimized} restored without changing assignments");
                    }
                    var transferred = remembered["2/1"];
                    actualActions.AssignDedicated("2/0",dedicatedBinding.Actions[0],transferred);
                    actualActions.ReleaseIfAssigned("2/1",dedicatedBinding.Actions[1],transferred);
                    var transferredReservations=actualActions.Reservations("2",dedicatedBinding);
                    if(transferredReservations.Count!=1 || transferredReservations["2/0"]!=transferred) throw new Exception("Transfer did not release the previous owner.");
                    transferredReservations["source"]=source.Handle;
                    var transferErrors=await runner.Run("2",dedicatedBinding,(id,a,used)=>actualActions.ExecuteAction(id,a,config,used),actualActions.Focus,transferredReservations);
                    var afterTransfer=actualActions.Reservations("2",dedicatedBinding);
                    if(transferErrors.Count>0 || afterTransfer.Count!=2 || afterTransfer["2/0"]!=transferred || afterTransfer["2/1"]==transferred) throw new Exception("Previous owner reclaimed transferred window.");
                    actualActions.AssignDedicated("4/0",dedicatedBinding.Actions[0],transferred);
                    if(actualActions.Reservations("2",dedicatedBinding)["2/0"]!=transferred || actualActions.Reservations("4",dedicatedBinding)["4/0"]!=transferred) throw new Exception("Separate keys cannot share a dedicated window.");
                    Console.WriteLine("PASS: transfer remains stable on repeat and ownership is scoped to each key");
                }
                catch(Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode=1; }
                finally { left.Close();right.Close();source.Close(); }
            };
            Application.Run(source); return;
        }
        if (args.Contains("--partial-config"))
        {
            var candidate = Configuration.Load(ConfigurationMigration.Upgrade("""{"mappings":{"1":{"action":"folder","path":"C:/"},"2":{"action":"folder"},"3":{"action":"application","startMenuName":"Macaroni missing-app test 12345"},"4":{"action":"application","startMenuName":"Google Chrome"}}}"""));
            AppCatalog.Resolve(candidate);
            if (!candidate.Mappings.Keys.SequenceEqual(["1", "4"]) || candidate.Errors.Count != 2 || candidate.Mappings["4"].Actions[0].Installed == null)
                throw new Exception("Partial configuration loading or Windows name resolution failed.");
            Console.WriteLine("PASS: actual Windows catalog failure and invalid mapping isolated; valid folder and Chrome mappings still loaded");
            return;
        }
        if (args.Length == 2 && args[0] == "--validate-config")
        {
            var candidate = Configuration.Parse(File.ReadAllText(args[1]));
            AppCatalog.Resolve(candidate);
            if (candidate.Errors.Count > 0) throw new Exception(string.Join("\n", candidate.Errors));
            Native.EnumWindows((window, _) =>
            {
                var name = new System.Text.StringBuilder(256);
                Native.GetClassName(window, name, name.Capacity);
                if (name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd")
                    if (WindowActions.MatchesApplication(window, new Mapping { Action = "application", Executable = "explorer.exe", Process = "explorer" }))
                        throw new Exception("Explorer mapping incorrectly matched the desktop/taskbar.");
                return true;
            }, 0);
            Console.WriteLine("PASS: Explorer application mappings exclude desktop/taskbar windows");
            Console.WriteLine($"PASS: strict configuration validated; {candidate.Mappings.Count} mappings and all Start menu names resolved");
            return;
        }
        if (args.Contains("--apps"))
        {
            var apps = AppCatalog.Read();
            Console.WriteLine($"PASS: discovered {apps.Length} Start menu applications");
            foreach (string name in new[] { "Google Chrome", "Gmail", "ChatGPT", "tag-watch" })
            {
                var matches = apps.Where(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToArray();
                foreach (var app in matches) Console.WriteLine($"APP {app.Name}: process={app.Process}, strict={app.StrictIdentity}, id={app.AppId}");
            }
            var chrome = AppNames.Resolve("google chrome", apps);
            if (chrome.Process != "chrome" || !File.Exists(chrome.Launcher)) throw new Exception("Chrome shortcut resolution failed.");
            Console.WriteLine("PASS: Google Chrome resolves by case-insensitive display name");
            Native.EnumWindows((window, _) =>
            {
                if (!Native.IsWindowVisible(window)) return true;
                var title = new System.Text.StringBuilder(2048);
                Native.GetWindowText(window, title, title.Capacity);
                if (title.Length == 0) return true;
                string id = WindowAppIdentity.Read(window);
                foreach (var app in apps.Where(a => a.StrictIdentity))
                    if (AppNames.IdentityMatches(app, id))
                    {
                        if (!WindowActions.MatchesApplication(window, new Mapping { Action = "application", StartMenuName = app.Name, Installed = app })) throw new Exception("Identity matched but window rejected.");
                        if (app.Process == "chrome" && WindowActions.MatchesApplication(window, new Mapping { Action = "application", StartMenuName = "Google Chrome", Installed = chrome })) throw new Exception("Chrome app incorrectly matched regular Chrome.");
                        Console.WriteLine($"PASS: open window matched {app.Name} by identity: {id}");
                    }
                return true;
            }, 0);
            return;
        }
        if (args.Contains("--layouts"))
        {
            using var form = new Form { Text = "Macaroni layout verification", StartPosition = FormStartPosition.Manual, Bounds = new Rectangle(100, 100, 600, 400) };
            form.Shown += async (_, _) =>
            {
                try
                {
                    foreach (var screen in Screen.AllScreens)
                    {
                        foreach (var layout in Layouts.Names)
                        {
                            WindowPlacement.Apply(form.Handle, layout, screen);
                            await Task.Delay(100);
                            if (layout == "maximize")
                            {
                                if (!Native.IsZoomed(form.Handle) || Screen.FromHandle(form.Handle).DeviceName != screen.DeviceName) throw new Exception("Maximize failed");
                            }
                            else if (!Layouts.Matches(WindowPlacement.VisibleBounds(form.Handle), Layouts.Bounds(layout, screen.WorkingArea, WindowPlacement.VisibleBounds(form.Handle).Size)))
                                throw new Exception($"{layout}: actual {WindowPlacement.VisibleBounds(form.Handle)}, expected {Layouts.Bounds(layout, screen.WorkingArea, default)}");
                            if (WindowPlacement.Apply(form.Handle, layout, screen)) throw new Exception($"Repeated {layout} changed the window again.");
                            Console.WriteLine($"PASS: {screen.DeviceName} {layout} fits work area; repeat does not move/resize");
                        }
                    }
                }
                catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
                finally { form.Close(); }
            };
            Application.Run(form);
            return;
        }
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
                if (args.Contains("--named-launch"))
                {
                    var config = new Configuration();
                    using var actions = new WindowActions();
                    foreach (string name in new[] { "Google Chrome", "ChatGPT", "Gmail" })
                    {
                        var mapping = new Mapping { Action = "application", StartMenuName = name, LaunchAlways = true };
                        config.Mappings = new() { ["2"] = new KeyBinding { Actions = [mapping] } };
                        AppCatalog.Resolve(config);
                        nint existing = 0;
                        Native.EnumWindows((window, _) =>
                        {
                            if (Native.IsWindowVisible(window) && WindowActions.MatchesApplication(window, mapping)) { existing = window; return false; }
                            return true;
                        }, 0);
                        // Keep the app on its current display during this check.
                        if (existing != 0)
                            mapping.Monitor = MonitorCatalog.WindowsDisplays().First(d => d.Screen.DeviceName == Screen.FromHandle(existing).DeviceName).Number;
                        await actions.Execute("2", mapping, config);
                        if (!WindowActions.MatchesApplication(Native.GetForegroundWindow(), mapping)) throw new Exception("Wrong app focused for " + name);
                        Console.WriteLine($"PASS: {name} launches by Start menu name and focuses a matching window");
                        mapping.LaunchAlways = false;
                        var before = Native.GetForegroundWindow();
                        await actions.Execute("2", mapping, config);
                        if (Native.GetForegroundWindow() != before) throw new Exception("Reuse selected a different window for " + name);
                        Console.WriteLine($"PASS: {name} reuses its existing window");
                    }
                }
                if (args.Contains("--notepad-config"))
                {
                    var configPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Macaroni", "config.json");
                    var settingsPath = args.LastOrDefault(a => a.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) ?? configPath;
                    var config = Configuration.Parse(File.ReadAllText(settingsPath));
                    var mapping = config.Mappings["3"].Actions[0];
                    if (mapping.Action != "application" || mapping.Process != "notepad++" || !mapping.LaunchAlways) throw new Exception("Expected key 3 to open config in Notepad++ on each press.");
                    using var actions = new WindowActions();
                    for (int press = 0; press < 2; press++)
                    {
                        if (press == 1)
                        {
                            using var other = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(mapping.Executable)
                            {
                                UseShellExecute = false,
                                ArgumentList = { System.IO.Path.GetFullPath("README.md") }
                            });
                            await Task.Delay(500);
                        }
                        await actions.Execute("3", mapping, config);
                        await Task.Delay(150);
                        var window = Native.GetForegroundWindow();
                        Native.GetWindowThreadProcessId(window, out var pid);
                        using var owner = System.Diagnostics.Process.GetProcessById((int)pid);
                        var title = new System.Text.StringBuilder(4096);
                        Native.GetWindowText(window, title, title.Capacity);
                        if (owner.ProcessName != "notepad++" || !title.ToString().Contains("config.json")) throw new Exception("Config file was not selected in Notepad++: " + title);
                        if (Screen.FromHandle(window).DeviceName != MonitorCatalog.Resolve(mapping.Monitor, config).DeviceName || !Native.IsZoomed(window)) throw new Exception("Notepad++ placement did not match monitor/layout.");
                        Console.WriteLine($"PASS: key 3 press {press + 1} opens config.json and focuses/maximizes Notepad++ on monitor {mapping.Monitor}");
                    }
                }
                if (args.Contains("--assign-terminal"))
                {
                    var config = Configuration.Parse(File.ReadAllText(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Macaroni", "config.json")));
                    var mapping = config.Mappings["7"].Actions[0];
                    using var actions = new WindowActions();
                    var choices = actions.DedicatedChoices("7", mapping);
                    if (choices.Length == 0) throw new Exception("Open a matching terminal before this check.");
                    var chosen = choices.Last().Window;
                    Native.GetWindowRect(chosen.Handle, out var before);
                    var focusBefore = Native.GetForegroundWindow();
                    actions.AssignDedicated("7", mapping, chosen.Handle, chosen);
                    if (!actions.DedicatedChoices("7", mapping).Any(c => c.Window.Handle == chosen.Handle && c.Current)) throw new Exception("Assigned window not marked current.");
                    Native.GetWindowRect(chosen.Handle, out var after);
                    if (!before.Equals(after) || Native.GetForegroundWindow() != focusBefore) throw new Exception("Assignment moved or activated a window.");
                    bool rejected = false;
                    try { actions.AssignDedicated("7", mapping, dispatcher.Handle); } catch { rejected = true; }
                    if (!rejected || !actions.DedicatedChoices("7", mapping).Any(c => c.Window.Handle == chosen.Handle && c.Current)) throw new Exception("Wrong-window assignment changed the selection.");
                    Console.WriteLine("PASS: actual terminal assignment updates picker state without moving or focusing windows; wrong app rejected");
                }
                if (args.Contains("--adopt-terminal"))
                {
                    var config = Configuration.Parse(File.ReadAllText(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Macaroni", "config.json")));
                    var mapping = config.Mappings["7"].Actions[0];
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
                    var mapping = config.Mappings["4"].Actions[0];
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
