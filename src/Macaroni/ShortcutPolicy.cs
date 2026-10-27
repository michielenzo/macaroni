namespace Macaroni;

internal static class ShortcutPolicy
{
    internal static bool Capture(int digit, bool control, bool enabled, Configuration config)
    {
        if (digit == 0) return control;
        if (!enabled || !config.Mappings.TryGetValue(digit.ToString(), out var mapping)) return false;
        return !control || mapping.Actions.Any(a => a.Action == "application" && a.Selection == "dedicated");
    }
}
