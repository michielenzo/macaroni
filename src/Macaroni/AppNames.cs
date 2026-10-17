namespace Macaroni;

internal sealed record InstalledApp(string Name, string Launcher, string Process, string AppId, bool StrictIdentity, string Target);

internal static class AppNames
{
    internal static void ResolveMappings(Configuration config, IEnumerable<InstalledApp> catalog)
    {
        var apps = catalog.ToArray();
        foreach (var (key, mapping) in config.Mappings.Where(p => !string.IsNullOrWhiteSpace(p.Value.StartMenuName)).ToArray())
        {
            try
            {
                mapping.Installed = Resolve(mapping.StartMenuName, apps);
                if (mapping.Installed.StrictIdentity && string.IsNullOrWhiteSpace(mapping.Installed.AppId))
                    throw new Exception($"‘{mapping.StartMenuName}’ has no usable app identity. Reinstall its Start menu shortcut or use an explicit mapping.");
            }
            catch (Exception ex) { config.Skip(key, ex.Message); }
        }
    }
    internal static InstalledApp Resolve(string name, IEnumerable<InstalledApp> apps)
    {
        var entries = apps.ToArray();
        var matches = entries.Where(app => app.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase))
            .DistinctBy(app => (app.Launcher.ToUpperInvariant(), app.AppId.ToUpperInvariant())).ToArray();
        if (matches.Length == 1) return matches[0];
        if (matches.Length > 1) throw new Exception($"Application name ‘{name}’ is ambiguous: " + string.Join("; ", matches.Select(app => app.Launcher)) + ". Use executable/process to choose explicitly.");
        var suggestions = entries.Select(app => app.Name).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(candidate => candidate.Contains(name.Trim(), StringComparison.OrdinalIgnoreCase) || name.Contains(candidate, StringComparison.OrdinalIgnoreCase))
            .ThenBy(candidate => Distance(name.ToLowerInvariant(), candidate.ToLowerInvariant())).Take(5);
        throw new Exception($"No Start menu application named ‘{name}’. Nearby names: {string.Join(", ", suggestions)}. Use List applications in the tray.");
    }
    private static int Distance(string a, string b)
    {
        var row = Enumerable.Range(0, b.Length + 1).ToArray();
        for (int i = 1; i <= a.Length; i++)
        {
            int diagonal = row[0]; row[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int old = row[j];
                row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), diagonal + (a[i - 1] == b[j - 1] ? 0 : 1));
                diagonal = old;
            }
        }
        return row[b.Length];
    }
    internal static bool IdentityMatches(InstalledApp app, string windowId) =>
        !string.IsNullOrEmpty(app.AppId) && app.AppId.Equals(windowId, StringComparison.OrdinalIgnoreCase);
}
