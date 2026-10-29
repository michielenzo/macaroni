namespace Macaroni;

internal static class TaskbarPreviewPolicy
{
    internal static bool IsPreviewClass(string name) => name is "TaskListThumbnailWnd" or "XamlExplorerHostIslandWindow";

    internal static bool Matches(string name, uint process, uint thread, uint taskbarProcess, uint taskbarThread) =>
        IsPreviewClass(name) && process != 0 && thread != 0 && process == taskbarProcess && thread == taskbarThread;
}
