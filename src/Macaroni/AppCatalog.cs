using System.Runtime.InteropServices;

namespace Macaroni;

internal static class AppCatalog
{
    internal static Task<InstalledApp[]> ReadAsync()
    {
        var result = new TaskCompletionSource<InstalledApp[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { result.SetResult(Read()); }
            catch (Exception ex) { result.SetException(ex); }
        }) { IsBackground = true, Name = "Macaroni Start menu catalog" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return result.Task;
    }
    internal static InstalledApp[] Read()
    {
        var apps = new List<InstalledApp>();
        object? shell = null, links = null, folder = null, items = null;
        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")!);
            links = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
            foreach (string root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.Programs), Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms) }.Distinct())
            {
                if (!Directory.Exists(root)) continue;
                foreach (string path in Directory.EnumerateFiles(root, "*.lnk", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }))
                {
                    object? link = null, directory = null, item = null;
                    try
                    {
                        link = ((dynamic)links!).CreateShortcut(path);
                        string target = Environment.ExpandEnvironmentVariables((string)((dynamic)link).TargetPath);
                        string arguments = (string)((dynamic)link).Arguments;
                        if (!target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                        directory = ((dynamic)shell!).NameSpace(System.IO.Path.GetDirectoryName(path));
                        item = ((dynamic)directory!).ParseName(System.IO.Path.GetFileName(path));
                        string appId = Convert.ToString(((dynamic)item!).ExtendedProperty("System.AppUserModel.ID")) ?? "";
                        bool webApp = arguments.Contains("--app-id=", StringComparison.OrdinalIgnoreCase);
                        string process = System.IO.Path.GetFileNameWithoutExtension(target);
                        if (process.Equals("chrome_proxy", StringComparison.OrdinalIgnoreCase)) process = "chrome";
                        if (process.Equals("wt", StringComparison.OrdinalIgnoreCase)) process = "WindowsTerminal";
                        apps.Add(new(System.IO.Path.GetFileNameWithoutExtension(path), path, process, appId, webApp, target));
                    }
                    catch (COMException) { }
                    finally { Release(item); Release(directory); Release(link); }
                }
            }
            folder = ((dynamic)shell!).NameSpace("shell:AppsFolder");
            items = ((dynamic)folder!).Items();
            for (int i = 0; i < (int)((dynamic)items).Count; i++)
            {
                object? item = null;
                try
                {
                    item = ((dynamic)items).Item(i);
                    string appId = Convert.ToString(((dynamic)item!).ExtendedProperty("System.AppUserModel.ID")) ?? "";
                    // Desktop entries are resolved from their real shortcuts;
                    // AppsFolder supplies version-independent packaged IDs.
                    if (!appId.Contains('!')) continue;
                    apps.Add(new((string)((dynamic)item!).Name, "shell:AppsFolder\\" + appId, "", appId, true, ""));
                }
                catch (COMException) { }
                finally { Release(item); }
            }
        }
        finally { Release(items); Release(folder); Release(links); Release(shell); }
        return apps.OrderBy(app => app.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }
    private static void Release(object? value) { if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value); }

    internal static void Resolve(Configuration config) => Resolve(config, Read());
    internal static async Task ResolveAsync(Configuration config)
    {
        if (!config.Mappings.Values.Any(m => !string.IsNullOrWhiteSpace(m.StartMenuName))) return;
        Resolve(config, await ReadAsync());
    }
    private static void Resolve(Configuration config, InstalledApp[] catalog)
    {
        foreach (var mapping in config.Mappings.Values.Where(m => !string.IsNullOrWhiteSpace(m.StartMenuName)))
        {
            mapping.Installed = AppNames.Resolve(mapping.StartMenuName, catalog);
            if (mapping.Installed.StrictIdentity && string.IsNullOrWhiteSpace(mapping.Installed.AppId))
                throw new Exception($"‘{mapping.StartMenuName}’ has no usable app identity. Reinstall its Start menu shortcut or use an explicit mapping.");
        }
    }
}
