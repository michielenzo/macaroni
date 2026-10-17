using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace Macaroni;

internal sealed class AssignmentPalette : Form
{
    private readonly ListBox list;
    private readonly ToolTip tips = new();
    private readonly Action<int> choose;
    private readonly Font headingFont;
    private readonly bool darkTheme;
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint window, uint attribute, ref int value, int size);

    internal AssignmentPalette(string sourceTitle, string[] labels, Screen screen, Action<int> choose, string explanation = "Choose an action for this window. Assignments belong to this key only.")
    {
        this.choose = choose;
        using var theme = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        bool dark = theme?.GetValue("AppsUseLightTheme") is int light && light == 0;
        darkTheme = dark;
        BackColor = dark ? Color.FromArgb(30, 32, 36) : Color.FromArgb(249, 250, 252);
        ForeColor = dark ? Color.WhiteSmoke : Color.FromArgb(30, 35, 42);
        Text = "Macaroni — Assign dedicated window";
        Font = new Font("Segoe UI", 10);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual; KeyPreview = true;
        int width = Math.Min(600, screen.WorkingArea.Width - 32);
        int height = Math.Min(230 + Math.Min(labels.Length, 6) * 58, screen.WorkingArea.Height - 32);
        ClientSize = new Size(width, height);
        headingFont = new Font(Font, FontStyle.Bold);
        var heading = new Label { Text = "Assign this window to…", Font = headingFont, AutoSize = false, Bounds = new Rectangle(20, 16, width - 40, 30) };
        var source = new Label { Text = sourceTitle, AutoEllipsis = true, Bounds = new Rectangle(20, 52, width - 40, 25) };
        tips.SetToolTip(source, sourceTitle);
        var description = new Label { Text = explanation, AutoEllipsis = true, Bounds = new Rectangle(20, 82, width - 40, 60) };
        tips.SetToolTip(description, explanation);
        list = new ListBox { DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 58, BorderStyle = BorderStyle.None, BackColor = BackColor, ForeColor = ForeColor, Bounds = new Rectangle(16, 150, width - 32, height - 195), IntegralHeight = false };
        list.Items.AddRange(labels); list.SelectedIndex = 0;
        list.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using var brush = new SolidBrush(selected ? (dark ? Color.FromArgb(45, 68, 99) : Color.FromArgb(220, 234, 253)) : BackColor);
            e.Graphics.FillRectangle(brush, e.Bounds);
            e.Graphics.DrawIcon(SystemIcons.Application, new Rectangle(e.Bounds.Left + 10, e.Bounds.Top + 17, 24, 24));
            string text = (string)list.Items[e.Index];
            string status = text.EndsWith(" · currently assigned here") ? "Currently assigned here"
                : text.EndsWith(" · transfers window") ? "Transfer from the current action" : "";
            if (status.Length > 0) text = text[..text.LastIndexOf(" · ", StringComparison.Ordinal)];
            TextRenderer.DrawText(e.Graphics, text, Font,
                new Rectangle(e.Bounds.Left + 46, e.Bounds.Top + 5, e.Bounds.Width - 55, status.Length > 0 ? 25 : e.Bounds.Height - 10), ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            if (status.Length > 0)
                TextRenderer.DrawText(e.Graphics, status, Font,
                    new Rectangle(e.Bounds.Left + 46, e.Bounds.Top + 30, e.Bounds.Width - 55, 23),
                    status.StartsWith("Transfer") ? (dark ? Color.FromArgb(255, 202, 128) : Color.FromArgb(125, 73, 0)) : (dark ? Color.LightSkyBlue : Color.FromArgb(20, 75, 135)),
                    TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            if (selected) e.DrawFocusRectangle();
        };
        list.MouseClick += (_, e) => { int index = list.IndexFromPoint(e.Location); if (index >= 0) Complete(index); };
        list.MouseMove += (_, e) => { int index = list.IndexFromPoint(e.Location); string text = index >= 0 ? labels[index] : ""; if (tips.GetToolTip(list) != text) tips.SetToolTip(list, text); };
        var help = new Label { Text = "↑ ↓ Select     Enter Assign     Esc Cancel", Bounds = new Rectangle(20, height - 32, width - 40, 24) };
        Controls.AddRange([heading, source, description, list, help]);
        Shown += (_, _) =>
        {
            // Re-center after Windows applies the destination monitor's DPI.
            Location = new Point(screen.WorkingArea.Left + (screen.WorkingArea.Width - Width) / 2, screen.WorkingArea.Top + (screen.WorkingArea.Height - Height) / 2);
            list.Focus();
        };
        Location = new Point(screen.WorkingArea.Left + (screen.WorkingArea.Width - Width) / 2, screen.WorkingArea.Top + (screen.WorkingArea.Height - Height) / 2);
    }
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int rounded = 2; DwmSetWindowAttribute(Handle, 33, ref rounded, sizeof(int));
        int dark = darkTheme ? 1 : 0; DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
    }
    protected override bool ProcessCmdKey(ref Message message, Keys key)
    {
        if (key == Keys.Escape) { Close(); return true; }
        if (key == Keys.Enter) { if (list.SelectedIndex >= 0) Complete(list.SelectedIndex); return true; }
        return base.ProcessCmdKey(ref message, key);
    }
    private void Complete(int index) { Close(); choose(index); }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { tips.Dispose(); headingFont.Dispose(); Font.Dispose(); }
        base.Dispose(disposing);
    }
}
