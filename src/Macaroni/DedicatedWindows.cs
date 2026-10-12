namespace Macaroni;

internal readonly record struct WindowCandidate(nint Handle, uint Pid, long Started, long LastFocused);

internal sealed class DedicatedWindows
{
    private readonly Dictionary<string, WindowCandidate> tracked = new();
    internal nint Current(string mapping, IEnumerable<WindowCandidate> candidates) =>
        tracked.TryGetValue(mapping, out var current) && candidates.Any(w => SameWindow(w, current)) ? current.Handle : 0;

    internal bool Assign(string mapping, WindowCandidate chosen, IEnumerable<WindowCandidate> candidates)
    {
        if (!candidates.Any(w => SameWindow(w, chosen))) return false;
        tracked[mapping] = chosen;
        return true;
    }

    private static bool SameWindow(WindowCandidate a, WindowCandidate b) => a.Handle == b.Handle && a.Pid == b.Pid && a.Started == b.Started;

    internal nint Select(string mapping, IEnumerable<WindowCandidate> candidates)
    {
        var available = candidates.ToArray();
        if (tracked.TryGetValue(mapping, out var previous) && available.Any(window =>
            window.Handle == previous.Handle && window.Pid == previous.Pid && window.Started == previous.Started))
            return previous.Handle;

        tracked.Remove(mapping);
        if (available.Length == 0) return 0;
        var adopted = available.OrderByDescending(window => window.LastFocused).First();
        tracked[mapping] = adopted;
        return adopted.Handle;
    }
}
