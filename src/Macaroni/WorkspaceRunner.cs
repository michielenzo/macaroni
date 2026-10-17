namespace Macaroni;

internal sealed class WorkspaceRunner
{
    private readonly HashSet<string> running = new();
    internal bool IsRunning(string key) => running.Contains(key);
    internal static string ActionId(string key, Mapping action) => $"{key}/{action.Position}";

    internal async Task<List<string>> Run(string key, KeyBinding binding, Func<string, Mapping, IReadOnlySet<nint>, Task<nint>> execute, Func<nint, Task> focus, IReadOnlyDictionary<string, nint>? reservations = null, Func<nint, bool>? focusWasLost = null, Action<IReadOnlyList<nint>, nint>? present = null)
    {
        var errors = new List<string>();
        if (!running.Add(key)) return errors;
        try
        {
            var claims = reservations == null ? new Dictionary<string, nint>() : new Dictionary<string, nint>(reservations);
            var labels = ActionLabels.For(binding);
            nint focusedWindow = 0;
            var placedWindows = new nint[binding.Actions.Count];
            // Await independently on the caller's STA: never move COM/window work
            // to the thread pool. Window discovery claims candidates immediately,
            // before another awaiting action can discover the same new window.
            var tasks = new List<Task>();
            for (int i = 0; i < binding.Actions.Count; i++)
            {
                Task previous = binding.Execution == "sequential" && tasks.Count > 0 ? tasks[^1] : Task.CompletedTask;
                Task task = RunAction(i, previous);
                tasks.Add(task);
            }
            await Task.WhenAll(tasks);
            // Other programs may activate themselves while launching. Reassert
            // only if focus changed, so an already focused window is untouched.
            if (focusedWindow != 0 && focusWasLost?.Invoke(focusedWindow) == true) await Focus(focusedWindow);
            try { present?.Invoke(placedWindows.Where(w => w != 0).ToArray(), focusedWindow); }
            catch (Exception ex) { errors.Add($"Key {key}: could not bring workspace forward: {ex.Message}"); }

            async Task Focus(nint window)
            {
                try { await focus(window); }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { lock (errors) errors.Add($"Key {key}: focus failed: {ex.Message}"); }
            }

            async Task RunAction(int i, Task previous)
            {
                await previous;
                var action = binding.Actions[i];
                try
                {
                    string id = ActionId(key, action);
                    var excluded = new WindowClaims(claims, id);
                    nint window = await execute(id, action, excluded);
                    if (window != 0 && !excluded.TryClaim(window)) throw new Exception("The window was already selected by another action.");
                    placedWindows[i] = window;
                    if (action.Focus && window != 0)
                    {
                        focusedWindow = window;
                        await Focus(window);
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { lock (errors) errors.Add($"Key {key}, {labels[i]}: {ex.Message}"); }
            }
        }
        finally { running.Remove(key); }
        return errors;
    }
}

// A live view, rather than a snapshot: waiting launchers see windows claimed by
// siblings after they started. Access stays atomic even in pure asynchronous tests.
internal sealed class WindowClaims(Dictionary<string, nint> owners, string actionId) : IReadOnlySet<nint>
{
    private HashSet<nint> Snapshot() { lock (owners) return owners.Where(p => p.Key != actionId).Select(p => p.Value).ToHashSet(); }
    internal bool TryClaim(nint window)
    {
        if (window == 0) return false;
        lock (owners)
        {
            if (owners.Any(p => p.Key != actionId && p.Value == window)) return false;
            owners[actionId] = window;
            return true;
        }
    }
    internal static nint Claim(nint window, IReadOnlySet<nint> excluded) => window == 0 || excluded.Contains(window) ? 0
        : excluded is WindowClaims claims && !claims.TryClaim(window) ? 0 : window;
    public bool Contains(nint item) { lock (owners) return owners.Any(p => p.Key != actionId && p.Value == item); }
    public int Count => Snapshot().Count;
    public IEnumerator<nint> GetEnumerator() => Snapshot().GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    public bool IsProperSubsetOf(IEnumerable<nint> other) => Snapshot().IsProperSubsetOf(other);
    public bool IsProperSupersetOf(IEnumerable<nint> other) => Snapshot().IsProperSupersetOf(other);
    public bool IsSubsetOf(IEnumerable<nint> other) => Snapshot().IsSubsetOf(other);
    public bool IsSupersetOf(IEnumerable<nint> other) => Snapshot().IsSupersetOf(other);
    public bool Overlaps(IEnumerable<nint> other) => Snapshot().Overlaps(other);
    public bool SetEquals(IEnumerable<nint> other) => Snapshot().SetEquals(other);
}
