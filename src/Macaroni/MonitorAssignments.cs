namespace Macaroni;

internal static class MonitorAssignments
{
    internal static Dictionary<int, string> NumberPaths(IEnumerable<string> deviceNames) =>
        deviceNames.Select((device, index) => (device, number: index + 1)).ToDictionary(p => p.number, p => p.device);
    internal static Dictionary<int, string> Available(Dictionary<string, string> assignments, IEnumerable<string> connected)
    {
        var present = connected.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return assignments.Where(pair => present.Contains(pair.Value)).ToDictionary(pair => int.Parse(pair.Key), pair => pair.Value);
    }
}
