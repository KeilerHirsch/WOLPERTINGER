using Avalonia.Controls;
using Wolpertinger.Presentation.ViewModels;
using Wolpertinger.Presentation.Windows.Interop;

namespace Wolpertinger.Presentation.Windows;

public static class WindowsHubAdapter
{
    /// <summary>Attach on the UI thread before showing a dedicated interactive Hub window.</summary>
    public static WindowsSurfaceAttachment Attach(Window window, string? preferredDisplayKey, string? gameDisplayKey)
    {
        ArgumentNullException.ThrowIfNull(window);
        return new WindowsSurfaceAttachment(new AvaloniaWindowsSurface(window), new WindowsNativeMethods(),
            false, DockPreset.Right, DensityPreset.Standard, preferredDisplayKey, gameDisplayKey);
    }
}
