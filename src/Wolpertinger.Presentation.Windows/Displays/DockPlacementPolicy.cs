using Avalonia;
using Wolpertinger.Presentation.ViewModels;

namespace Wolpertinger.Presentation.Windows.Displays;

public static class DockPlacementPolicy
{
    private const int LogicalInset = 16;

    public static PixelRect Resolve(
        DisplayDescriptor display,
        DockPreset dock,
        DensityPreset density)
    {
        ArgumentNullException.ThrowIfNull(display);

        var (logicalWidth, logicalHeight) = LogicalSize(dock, density);
        var width = Scale(logicalWidth, display.Scaling);
        var height = Scale(logicalHeight, display.Scaling);
        var inset = Scale(LogicalInset, display.Scaling);
        var area = display.WorkingArea;

        width = Math.Min(width, area.Width);
        height = Math.Min(height, area.Height);

        var x = area.X + ((area.Width - width) / 2);
        var y = area.Y + ((area.Height - height) / 2);
        switch (dock)
        {
            case DockPreset.Left:
                x = area.X + inset;
                break;
            case DockPreset.Right:
                x = area.Right - inset - width;
                break;
            case DockPreset.Top:
                y = area.Y + inset;
                break;
            case DockPreset.Bottom:
                y = area.Bottom - inset - height;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(dock), dock, null);
        }

        x = Math.Clamp(x, area.X, area.Right - width);
        y = Math.Clamp(y, area.Y, area.Bottom - height);
        return new PixelRect(x, y, width, height);
    }

    private static int Scale(int logicalPixels, double scaling) =>
        checked((int)Math.Round(logicalPixels * scaling, MidpointRounding.AwayFromZero));
    private static (int Width, int Height) LogicalSize(DockPreset dock, DensityPreset density)
    {
        var vertical = dock is DockPreset.Left or DockPreset.Right;

        return (vertical, density) switch
        {
            (true, DensityPreset.Compact) => (320, 160),
            (true, DensityPreset.Standard) => (380, 220),
            (true, DensityPreset.Expanded) => (460, 320),
            (false, DensityPreset.Compact) => (560, 104),
            (false, DensityPreset.Standard) => (720, 144),
            (false, DensityPreset.Expanded) => (880, 200),
            _ => throw new ArgumentOutOfRangeException(nameof(density), density, null)
        };
    }
}
