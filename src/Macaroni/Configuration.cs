using System.Text.Json;

namespace Macaroni;

public sealed class Configuration
{
    public int Version { get; set; } = 1;
    public TerminalSettings Terminal { get; set; } = new();
    public Dictionary<string, KeyBinding> Mappings { get; set; } = new();
    public Dictionary<string, string> Monitors { get; set; } = new();
    internal List<string> Errors { get; } = new();
    internal void Skip(string key, string reason) { Mappings.Remove(key); Errors.Add($"Key {key} disabled: {reason}"); }
    internal void SkipAction(string key, Mapping action, string reason)
    {
        if (Mappings.TryGetValue(key, out var binding))
        {
            binding.Actions.Remove(action);
            if (binding.Actions.Count == 0) Mappings.Remove(key);
        }
        Errors.Add($"Key {key} action {action.Position + 1} disabled: {reason}");
    }
    public static Configuration Parse(string json)
    {
        var result = Load(json);
        if (result.Errors.Count > 0) throw new Exception(string.Join(" ", result.Errors));
        return result;
    }
    public static Configuration Load(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new Exception("Configuration must be an object.");
        var root = document.RootElement.EnumerateObject().ToArray();
        var duplicate = root.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null) throw new Exception($"Duplicate top-level property ‘{duplicate.Key}’.");
        string shared = "{" + string.Join(",", root.Where(p => !p.Name.Equals("mappings", StringComparison.OrdinalIgnoreCase)).Select(p => JsonSerializer.Serialize(p.Name) + ":" + p.Value.GetRawText())) + "}";
        using var sharedDocument = JsonDocument.Parse(shared);
        RejectDuplicates(sharedDocument.RootElement, "configuration");
        var config = JsonSerializer.Deserialize<Configuration>(shared, JsonOptions) ?? throw new Exception("Empty configuration.");
        if (config.Version != 1 || config.Terminal is null || string.IsNullOrWhiteSpace(config.Terminal.Executable) || string.IsNullOrWhiteSpace(config.Terminal.Process) || config.Terminal.Arguments is null || config.Terminal.Arguments.Any(a => a is null) || config.Monitors is null)
            throw new Exception("Invalid configuration version, terminal, or monitors.");
        foreach (var (number, identity) in config.Monitors)
            if (!int.TryParse(number, out int parsed) || parsed < 1 || number != parsed.ToString() || string.IsNullOrWhiteSpace(identity)) throw new Exception("monitors must map positive numbers to identities.");
        if (config.Monitors.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != config.Monitors.Count) throw new Exception("Each monitor identity can only have one number.");
        var mappings = root.FirstOrDefault(p => p.Name.Equals("mappings", StringComparison.OrdinalIgnoreCase));
        if (mappings.Value.ValueKind == JsonValueKind.Undefined) return config;
        if (mappings.Value.ValueKind != JsonValueKind.Object) throw new Exception("mappings must be an object.");
        foreach (var group in mappings.Value.EnumerateObject().GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            string key = group.Key;
            if (group.Count() != 1) { config.Skip(key, "duplicate key mappings are not allowed."); continue; }
            try
            {
                if (key.Length != 1 || key[0] < '1' || key[0] > '9') throw new Exception("Keys must be 1–9; 0 is reserved.");
                var value = group.Single().Value;
                if (value.ValueKind != JsonValueKind.Object) throw new Exception("Key configuration must be an object containing actions.");
                var properties = value.EnumerateObject().ToArray();
                if (properties.GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1) || properties.Any(p => !new[] { "actions", "execution" }.Contains(p.Name, StringComparer.OrdinalIgnoreCase)))
                    throw new Exception("Each key accepts only actions and execution.");
                var array = properties.FirstOrDefault(p => p.Name.Equals("actions", StringComparison.OrdinalIgnoreCase)).Value;
                if (array.ValueKind != JsonValueKind.Array) throw new Exception("Each key requires an actions array.");
                var execution = properties.FirstOrDefault(p => p.Name.Equals("execution", StringComparison.OrdinalIgnoreCase)).Value;
                string mode = execution.ValueKind == JsonValueKind.Undefined ? "parallel" : execution.ValueKind == JsonValueKind.String ? execution.GetString()! : "";
                if (mode is not ("parallel" or "sequential")) throw new Exception("execution must be parallel or sequential.");
                var entries = array.EnumerateArray().ToArray();
                if (entries.Length == 0) throw new Exception("actions must contain at least one action.");
                var binding = new KeyBinding { Execution = mode }; config.Mappings[key] = binding;
                for (int i = 0; i < entries.Length; i++)
                {
                    try
                    {
                        RejectDuplicates(entries[i], $"key {key} action {i + 1}");
                        var action = entries[i].Deserialize<Mapping>(JsonOptions) ?? throw new Exception("Action must be an object.");
                        action.Position = i;
                        ValidateMapping(key, action, entries[i]);
                        binding.Actions.Add(action);
                    }
                    catch (Exception ex) { config.Errors.Add($"Key {key} action {i + 1} disabled: {ex.Message}"); }
                }
                var focused = binding.Actions.Where(a => a.Focus).ToArray();
                if (focused.Length > 1)
                    foreach (var action in focused) config.SkipAction(key, action, "Only one action per key may have focus: true.");
                if (binding.Actions.Count == 0) config.Mappings.Remove(key);
            }
            catch (Exception ex) { config.Skip(key, ex.Message); }
        }
        config.ValidateRecentActions();
        return config;
    }
    internal void ValidateRecentActions()
    {
        foreach (var (key, binding) in Mappings.ToArray())
            foreach (var group in binding.Actions.Where(a => a.Action == "application" && a.Selection == "recent")
                .GroupBy(ActionLabels.ApplicationIdentity).Where(g => g.Count() > 1).Select(g => g.ToArray()).ToArray())
                foreach (var action in group)
                    SkipAction(key, action, "Multiple recent actions for the same application are unstable. Use selection: dedicated for separate windows.");
    }
    private static void ValidateMapping(string key, Mapping m, JsonElement fields)
    {
            if (m.Action is not ("application" or "folder" or "command")) throw new Exception($"Key {key}: invalid action.");
            ValidateFields(key, m, fields);
            if (m.Monitor < 1 || !Layouts.Names.Contains(m.Layout)) throw new Exception($"Key {key}: invalid monitor or layout.");
            if (m.Selection is not ("recent" or "title" or "dedicated")) throw new Exception($"Key {key}: invalid selection.");
            if (m.Action == "application" && (string.IsNullOrWhiteSpace(m.StartMenuName) == string.IsNullOrWhiteSpace(m.Executable))) throw new Exception($"Key {key}: supply exactly one of startMenuName or executable.");
            if (m.Action == "application" && string.IsNullOrWhiteSpace(m.StartMenuName) && string.IsNullOrWhiteSpace(m.Process)) throw new Exception($"Key {key}: executable applications require process.");
            if (!string.IsNullOrWhiteSpace(m.StartMenuName) && m.Action != "application") throw new Exception($"Key {key}: app is only supported for application actions.");
            if (m.Action == "application" && m.Selection == "title" && string.IsNullOrWhiteSpace(m.Title)) throw new Exception($"Key {key}: title is required.");
            if (m.Action == "folder" && string.IsNullOrWhiteSpace(m.Path)) throw new Exception($"Key {key}: path is required.");
            if (m.Action == "command" && (string.IsNullOrWhiteSpace(m.Executable) || m.Mode is not ("hidden" or "terminal"))) throw new Exception($"Key {key}: executable and valid mode are required.");
            if (m.Arguments is null || m.Arguments.Any(a => a is null)) throw new Exception($"Key {key}: arguments must be an array of strings.");

        if (fields.EnumerateObject().Any(p => p.Name.Equals("name", StringComparison.OrdinalIgnoreCase)) && string.IsNullOrWhiteSpace(m.Name)) throw new Exception("name cannot be empty.");
        if (!ActionLabels.ProducesWindow(m) && fields.EnumerateObject().Any(p => p.Name.Equals("focus", StringComparison.OrdinalIgnoreCase))) throw new Exception("Hidden commands cannot specify focus.");
    }
    private static void RejectDuplicates(JsonElement element, string location)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new Exception($"Duplicate property or key ‘{property.Name}’ in {location}.");
                RejectDuplicates(property.Value, location + "." + property.Name);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RejectDuplicates(item, location);
    }
    private static void ValidateFields(string key, Mapping mapping, JsonElement fields)
    {
        string[] allowed = mapping.Action switch
        {
            "application" => ["action", "startMenuName", "executable", "process", "arguments", "workingDirectory", "selection", "title", "launchAlways", "monitor", "layout"],
            "folder" => ["action", "path", "monitor", "layout"],
            "command" => ["action", "executable", "arguments", "workingDirectory", "mode", "monitor", "layout"],
            _ => []
        };
        var properties = fields.EnumerateObject().ToArray();
        foreach (var property in properties)
        {
            if (!allowed.Concat(new[] { "name", "focus" }).Contains(property.Name, StringComparer.OrdinalIgnoreCase)) throw new Exception($"Key {key}: {property.Name} does not apply to {mapping.Action}.");
            if (property.Value.ValueKind == JsonValueKind.Null) throw new Exception($"Key {key}: {property.Name} cannot be null.");
        }
        bool Has(string name) => properties.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (mapping.Action == "application")
        {
            if (Has("startMenuName") && Has("executable")) throw new Exception($"Key {key}: startMenuName and executable cannot be combined.");
            if (Has("startMenuName") && string.IsNullOrWhiteSpace(mapping.StartMenuName)) throw new Exception($"Key {key}: startMenuName cannot be empty.");
            if (Has("process") && string.IsNullOrWhiteSpace(mapping.Process)) throw new Exception($"Key {key}: process cannot be empty when provided.");
            if (Has("title") && mapping.Selection != "title") throw new Exception($"Key {key}: title requires selection: title.");
        }
        if (mapping.Action == "command" && mapping.Mode == "hidden" && (Has("monitor") || Has("layout")))
            throw new Exception($"Key {key}: hidden commands cannot have monitor or layout settings.");
    }

    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, WriteIndented = true, UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
    public static int ResolveMonitor(int requested, IEnumerable<int> available)
    {
        var sorted = available.Order().ToArray();
        return sorted.LastOrDefault(n => n <= requested, sorted.First());
    }
}
public sealed class TerminalSettings
{
    public string Executable { get; set; } = "wt.exe";
    public string Process { get; set; } = "WindowsTerminal";
    public string[] Arguments { get; set; } = ["-w", "new"];
}
public sealed class Mapping
{
    public string Name { get; set; } = "";
    public bool Focus { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] internal int Position { get; set; }
    public string StartMenuName { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore] internal InstalledApp? Installed { get; set; }
    public string Action { get; set; } = "";
    public string Executable { get; set; } = "";
    public string Process { get; set; } = "";
    public string[] Arguments { get; set; } = [];
    public bool LaunchAlways { get; set; }
    public string Selection { get; set; } = "recent";
    public string Title { get; set; } = "";
    public string Path { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public string Mode { get; set; } = "hidden";
    public int Monitor { get; set; } = 1;
    public string Layout { get; set; } = "maximize";
}
