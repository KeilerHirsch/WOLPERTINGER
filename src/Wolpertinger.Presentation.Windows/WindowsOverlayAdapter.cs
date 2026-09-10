using Avalonia.Controls;
using Wolpertinger.Presentation.ViewModels;
using Wolpertinger.Presentation.Windows.Interop;

namespace Wolpertinger.Presentation.Windows;

public static class WindowsOverlayAdapter
{
    /// <summary>Attach on the UI thread before showing a passive overlay.</summary>
    public static WindowsSurfaceAttachment Attach(Window window, DockPreset dock, DensityPreset density,
        string? preferredDisplayKey, string? gameDisplayKey)
    {
        ArgumentNullException.ThrowIfNull(window);
        return new WindowsSurfaceAttachment(new AvaloniaWindowsSurface(window), new WindowsNativeMethods(),
            true, dock, density, preferredDisplayKey, gameDisplayKey);
    }
}
