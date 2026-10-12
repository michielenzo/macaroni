using System.Drawing;

namespace Macaroni;

internal static class Layouts
{
    internal static readonly string[] Names = ["maximize", "preserve", "left", "right", "top", "bottom", "top-left", "top-right", "bottom-left", "bottom-right"];

    internal static Rectangle Bounds(string layout, Rectangle area, Size current)
    {
        int leftWidth = area.Width / 2, topHeight = area.Height / 2;
        int splitX = area.Left + leftWidth, splitY = area.Top + topHeight;
        return layout switch
        {
            "maximize" => area,
            "preserve" => Center(area, current),
            "left" => new(area.Left, area.Top, leftWidth, area.Height),
            "right" => new(splitX, area.Top, area.Width - leftWidth, area.Height),
            "top" => new(area.Left, area.Top, area.Width, topHeight),
            "bottom" => new(area.Left, splitY, area.Width, area.Height - topHeight),
            "top-left" => new(area.Left, area.Top, leftWidth, topHeight),
            "top-right" => new(splitX, area.Top, area.Width - leftWidth, topHeight),
            "bottom-left" => new(area.Left, splitY, leftWidth, area.Height - topHeight),
            "bottom-right" => new(splitX, splitY, area.Width - leftWidth, area.Height - topHeight),
            _ => throw new ArgumentException($"Unknown layout: {layout}")
        };
    }
    private static Rectangle Center(Rectangle area, Size current)
    {
        int width = Math.Clamp(current.Width, 1, area.Width), height = Math.Clamp(current.Height, 1, area.Height);
        return new(area.Left + (area.Width - width) / 2, area.Top + (area.Height - height) / 2, width, height);
    }
    internal static bool Matches(Rectangle actual, Rectangle wanted) =>
        Math.Abs(actual.Left - wanted.Left) <= 2 && Math.Abs(actual.Top - wanted.Top) <= 2 &&
        Math.Abs(actual.Right - wanted.Right) <= 2 && Math.Abs(actual.Bottom - wanted.Bottom) <= 2;
}
