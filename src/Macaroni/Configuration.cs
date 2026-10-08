using System.Text.Json;

namespace Macaroni;

public sealed class Configuration
{
    public int Version { get; set; } = 1;
    public string Terminal { get; set; } = "wt.exe";
    public string TerminalProcess { get; set; } = "WindowsTerminal";
    public string[] TerminalArguments { get; set; } = ["-w", "new"];
    public Dictionary<string, Mapping> Mappings { get; set; } = new();
    public static Configuration Parse(string json)
    {
        var config = JsonSerializer.Deserialize<Configuration>(json, JsonOptions) ?? throw new Exception("Empty configuration.");
        if (config.Version != 1 || string.IsNullOrWhiteSpace(config.Terminal) || string.IsNullOrWhiteSpace(config.TerminalProcess) || config.TerminalArguments is null || config.TerminalArguments.Any(a => a is null) || config.Mappings is null)
            throw new Exception("Invalid configuration version, terminal, or mappings.");
        foreach (var (key, m) in config.Mappings)
        {
            if (key.Length != 1 || key[0] < '1' || key[0] > '9' || m is null) throw new Exception("Mappings must use keys 1–9. Key 0 is reserved.");
            if (m.Action is not ("application" or "folder" or "command")) throw new Exception($"Key {key}: invalid action.");
            if (m.Monitor < 1 || m.Size is not ("maximize" or "preserve")) throw new Exception($"Key {key}: invalid monitor or size.");
            if (m.Selection is not ("recent" or "title" or "dedicated")) throw new Exception($"Key {key}: invalid selection.");
            if (m.Action == "application" && (string.IsNullOrWhiteSpace(m.Executable) || string.IsNullOrWhiteSpace(m.Process))) throw new Exception($"Key {key}: executable and process are required.");
            if (m.Action == "application" && m.Selection == "title" && string.IsNullOrEmpty(m.Title)) throw new Exception($"Key {key}: title is required.");
            if (m.Action == "folder" && string.IsNullOrWhiteSpace(m.Path)) throw new Exception($"Key {key}: path is required.");
            if (m.Action == "command" && (string.IsNullOrWhiteSpace(m.Executable) || m.Mode is not ("hidden" or "terminal"))) throw new Exception($"Key {key}: executable and valid mode are required.");
            if (m.Arguments is null || m.Arguments.Any(a => a is null)) throw new Exception($"Key {key}: arguments must be an array of strings.");
        }
        return config;
    }
    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, WriteIndented = true, UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
    public static int ResolveMonitor(int requested, IEnumerable<int> available)
    {
        var sorted = available.Order().ToArray();
        return sorted.LastOrDefault(n => n <= requested, sorted.First());
    }
}

public sealed class Mapping
{
    public string Action { get; set; } = "application";
    public string Executable { get; set; } = "";
    public string Process { get; set; } = "";
    public string[] Arguments { get; set; } = [];
    public string Selection { get; set; } = "recent";
    public string Title { get; set; } = "";
    public string Path { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public string Mode { get; set; } = "hidden";
    public int Monitor { get; set; } = 1;
    public string Size { get; set; } = "maximize";
}
