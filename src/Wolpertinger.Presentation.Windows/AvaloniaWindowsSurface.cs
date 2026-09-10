using Avalonia.Controls;
using Avalonia.Platform;
using Wolpertinger.Presentation.Windows.Displays;

namespace Wolpertinger.Presentation.Windows;

// A Windows-only seam for exercising the real event handler path without opening desktop windows.
internal interface IWindowsSurface
{
    event EventHandler? Opened;
    event EventHandler? Closed;
    event EventHandler? ScreensChanged;
    event EventHandler? ScalingChanged;
    bool IsVisible { get; }
    IPlatformHandle? Handle { get; }
    IReadOnlyList<DisplayDescriptor> Displays { get; }
    void Configure(bool passive);
    void Hide();
}

internal sealed class AvaloniaWindowsSurface(Window window) : IWindowsSurface
{
    public event EventHandler? Opened { add => window.Opened += value; remove => window.Opened -= value; }
    public event EventHandler? Closed { add => window.Closed += value; remove => window.Closed -= value; }
    public event EventHandler? ScreensChanged { add => window.Screens.Changed += value; remove => window.Screens.Changed -= value; }
    public event EventHandler? ScalingChanged { add => window.ScalingChanged += value; remove => window.ScalingChanged -= value; }
    public bool IsVisible => window.IsVisible;
    public IPlatformHandle? Handle => window.TryGetPlatformHandle();
    public IReadOnlyList<DisplayDescriptor> Displays
    {
        get
        {
            var displays = window.Screens.All.Select(screen => new DisplayDescriptor(
                screen.DisplayName ?? throw new InvalidOperationException("Screen has no display device name."),
                screen.Bounds, screen.WorkingArea, screen.Scaling, screen.IsPrimary)).ToArray();
            if (displays.Any(display => string.IsNullOrEmpty(display.Key)) ||
                displays.Select(display => display.Key).Distinct(StringComparer.Ordinal).Count() != displays.Length)
                throw new InvalidOperationException("Screen device names are missing or ambiguous.");
            return displays;
        }
    }

    public void Configure(bool passive)
    {
        // Apply before Show as well as on Opened: Opened alone is too late to prevent initial activation.
        window.WindowDecorations = WindowDecorations.None;
        window.CanResize = false;
        window.WindowState = WindowState.Normal;
        window.SizeToContent = SizeToContent.Manual;
        if (passive)
        {
            window.ShowActivated = false;
            window.ShowInTaskbar = false;
            window.Topmost = true;
        }
    }

    public void Hide() => window.Hide();
}
