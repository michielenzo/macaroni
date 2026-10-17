namespace Macaroni;

public sealed class KeyBinding
{
    public string Execution { get; set; } = "parallel";
    public List<Mapping> Actions { get; set; } = [];
}

internal static class ActionLabels
{
    internal static string ProcessName(string value)
    {
        string file = Environment.ExpandEnvironmentVariables(value).Replace('\\', '/').Split('/').Last();
        return (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? file[..^4] : file).ToUpperInvariant();
    }
    internal static string ApplicationIdentity(Mapping action) => action.Installed is { StrictIdentity: true } app
        ? "APP:" + app.AppId.ToUpperInvariant() : action.Installed == null && !string.IsNullOrWhiteSpace(action.StartMenuName)
            ? "NAME:" + action.StartMenuName.Trim().ToUpperInvariant()
            : ProcessName(!string.IsNullOrWhiteSpace(action.Process) ? action.Process : action.Installed?.Process ?? action.StartMenuName);

    internal static string Base(Mapping action)
    {
        if (!string.IsNullOrWhiteSpace(action.Name)) return action.Name;
        if (action.Action == "folder")
        {
            string path = Environment.ExpandEnvironmentVariables(action.Path).Replace('\\', '/').TrimEnd('/');
            return path.Split('/').LastOrDefault() is { Length: > 0 } name ? name : action.Path;
        }
        if (!string.IsNullOrWhiteSpace(action.StartMenuName)) return action.StartMenuName;
        string file = action.Executable.Replace('\\', '/').Split('/').LastOrDefault() ?? action.Executable;
        return file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? file[..^4] : file;
    }
    internal static bool ProducesWindow(Mapping action) => action.Action != "command" || action.Mode == "terminal";
    internal static Mapping[] Assignable(KeyBinding binding, Func<Mapping, bool> matches) => binding.Actions
        .Where(a => a.Action == "application" && a.Selection == "dedicated" && matches(a)).ToArray();
    internal static string[] For(KeyBinding binding)
    {
        var labels = binding.Actions.Select(action => Base(action) +
            (ProducesWindow(action) ? $" — monitor {action.Monitor}, {action.Layout}" : " — hidden") +
            (action.Action == "command" && action.Mode == "terminal" ? " · terminal" : "") +
            (action.Selection == "title" ? $" · {action.Title}" : "") + (action.Focus ? " · focus" : "")).ToArray();
        return labels.Select((label, i) => labels.Count(other => other.Equals(label, StringComparison.OrdinalIgnoreCase)) > 1
            ? $"{label} #{binding.Actions[i].Position + 1}" : label).ToArray();
    }
}
