namespace Macaroni;

internal sealed class KeyPolicy
{
    private readonly Dictionary<uint, bool> pressed = new();
    internal (bool Suppress, bool Trigger) Handle(uint scan, bool down, bool numLock, bool mapped)
    {
        if (!down) return (pressed.Remove(scan, out bool suppressed) && suppressed, false);
        if (pressed.TryGetValue(scan, out bool captured)) return (captured, false);
        bool capture = !numLock && mapped;
        pressed[scan] = capture;
        return (capture, capture);
    }
}
