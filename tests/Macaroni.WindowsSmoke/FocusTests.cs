using Macaroni;

internal static class FocusTests
{
    internal static void Run(bool taskbar)
    {
        using var target = new Form { Text = "Macaroni focus test", Width = 420, Height = 160 };
        target.Controls.Add(new Label { Dock = DockStyle.Fill, Text = taskbar
            ? "Open a grouped taskbar preview. The test will detect it and focus this window automatically."
            : "Testing ordinary-window and superseded focus requests." });
        target.Shown += async (_, _) =>
        {
            try
            {
                if (taskbar)
                {
                    Console.WriteLine("WATCHING: open a grouped taskbar preview within 60 seconds; no reply is needed.");
                    nint preview = 0;
                    for (int i = 0; i < 600; i++)
                    {
                        nint foreground = Native.GetForegroundWindow();
                        if (TaskbarPreview.IsForegroundPreview(foreground)) { preview = foreground; break; }
                        await Task.Delay(100);
                    }
                    if (preview == 0) throw new Exception("No foreground taskbar preview observed within 60 seconds.");
                    Console.WriteLine("Observed preview: " + ForegroundActivation.Describe(preview));
                    await ForegroundActivation.Focus(target.Handle, CancellationToken.None, Console.WriteLine, () => false);
                    if (Native.GetForegroundWindow() != preview) throw new Exception("A superseded request changed focus.");
                    Console.WriteLine("PASS: superseded request leaves taskbar preview untouched");
                    await ForegroundActivation.Focus(target.Handle, CancellationToken.None, Console.WriteLine);
                    await Task.Delay(300);
                    if (Native.GetForegroundWindow() != target.Handle) throw new Exception("Target did not retain focus after dismissing taskbar preview.");
                    if (Native.IsWindowVisible(preview)) throw new Exception("Taskbar preview is still visible.");
                    Console.WriteLine("PASS: real taskbar preview dismissed and target focus confirmed");
                }
                else
                {
                    using var dialog = new EscapeProbe { Text = "Ordinary application dialog", ShowInTaskbar = false };
                    dialog.Show(); dialog.Activate();
                    await ForegroundActivation.Focus(dialog.Handle, CancellationToken.None, Console.WriteLine);
                    if (Native.GetForegroundWindow() != dialog.Handle) throw new Exception("Could not establish test dialog focus.");
                    dialog.Escapes = 0;
                    if (TaskbarPreview.IsForegroundPreview(dialog.Handle)) throw new Exception("Ordinary dialog recognized as a taskbar preview.");
                    await ForegroundActivation.Focus(target.Handle, CancellationToken.None, Console.WriteLine, () => false);
                    if (Native.GetForegroundWindow() != dialog.Handle) throw new Exception("Superseded request stole focus.");
                    bool current = true;
                    int recoveryCalls = 0;
                    // An invalid target forces the initial confirmation to wait.
                    // Supersede it before any recovery or synthetic input occurs.
                    var pending = ForegroundActivation.Focus(0, CancellationToken.None, _ => recoveryCalls++, () => current);
                    current = false;
                    await pending;
                    if (recoveryCalls != 0 || Native.GetForegroundWindow() != dialog.Handle)
                        throw new Exception("Superseded in-flight request continued recovery.");
                    await ForegroundActivation.Focus(target.Handle, CancellationToken.None, Console.WriteLine);
                    if (Native.GetForegroundWindow() != target.Handle || !dialog.Visible || dialog.Escapes != 0)
                        throw new Exception("Ordinary activation dismissed a dialog or sent Escape.");
                    Console.WriteLine("PASS: ordinary dialogs stay open and receive no Escape; superseded and in-flight requests are inert");
                }
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
            finally { target.Close(); }
        };
        Application.Run(target);
    }

    private sealed class EscapeProbe : Form
    {
        internal int Escapes;
        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            if (keyData == Keys.Escape) Escapes++;
            return base.ProcessCmdKey(ref message, keyData);
        }
    }
}
