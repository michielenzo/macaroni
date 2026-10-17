using System.Text;
using System.Text.Json;

namespace Macaroni;

public static class ConfigurationMigration
{
    public static string Upgrade(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true, IndentCharacter = '\t', IndentSize = 1, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                writer.WritePropertyName(property.Name);
                if (!property.Name.Equals("mappings", StringComparison.OrdinalIgnoreCase) || property.Value.ValueKind != JsonValueKind.Object) { property.Value.WriteTo(writer); continue; }
                writer.WriteStartObject();
                foreach (var entry in property.Value.EnumerateObject())
                {
                    writer.WritePropertyName(entry.Name);
                    bool grouped = entry.Value.ValueKind == JsonValueKind.Object && entry.Value.EnumerateObject().Any(p => p.Name.Equals("actions", StringComparison.OrdinalIgnoreCase));
                    if (grouped || entry.Value.ValueKind != JsonValueKind.Object) { entry.Value.WriteTo(writer); continue; }
                    writer.WriteStartObject(); writer.WritePropertyName("actions"); writer.WriteStartArray(); writer.WriteStartObject();
                    foreach (var field in entry.Value.EnumerateObject()) field.WriteTo(writer);
                    bool hidden = entry.Value.EnumerateObject().Any(p => p.Name.Equals("action", StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() == "command") &&
                        !entry.Value.EnumerateObject().Any(p => p.Name.Equals("mode", StringComparison.OrdinalIgnoreCase) && p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() == "terminal");
                    // Preserve the old single-action focus behavior explicitly.
                    if (!hidden && !entry.Value.EnumerateObject().Any(p => p.Name.Equals("focus", StringComparison.OrdinalIgnoreCase))) writer.WriteBoolean("focus", true);
                    writer.WriteEndObject(); writer.WriteEndArray(); writer.WriteEndObject();
                }
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }
        string result = Encoding.UTF8.GetString(buffer.ToArray());
        var comments = json.Split('\n').Select(line => line.Trim()).Where(line => line.StartsWith("//")).ToArray();
        if (comments.Length > 0) result = result.Insert(result.IndexOf('\n') + 1, string.Join("\n", comments.Select(c => "\t" + c)) + "\n");
        return result.Replace("\r\n", "\n").Replace("\n", "\r\n") + "\r\n";
    }
}
