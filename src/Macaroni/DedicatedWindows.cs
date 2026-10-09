namespace Macaroni;

internal readonly record struct WindowCandidate(nint Handle, uint Pid, long Started, long LastFocused);

internal sealed class DedicatedWindows
{
    private readonly Dictionary<string, WindowCandidate> tracked = new();

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
