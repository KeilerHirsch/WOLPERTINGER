using System.ComponentModel;
using Wolpertinger.Presentation.ViewModels;
using Wolpertinger.Presentation.Windows.Displays;
using Wolpertinger.Presentation.Windows.Interop;

namespace Wolpertinger.Presentation.Windows;

/// <summary>Owns one surface's subscriptions. Attach before Show; dispose on the UI thread.</summary>
public sealed class WindowsSurfaceAttachment : IDisposable
{
    private readonly IWindowsSurface surface;
    private readonly IWindowsNativeApi native;
    private readonly bool passive;
    private readonly DockPreset dock;
    private readonly DensityPreset density;
    private readonly string? preferredDisplayKey;
    private readonly string? gameDisplayKey;
    private bool disposed;
    private bool refreshing;

    public string? LastDiagnostic { get; private set; }
    public string? CurrentDisplayKey { get; private set; }

    internal WindowsSurfaceAttachment(IWindowsSurface surface, IWindowsNativeApi native, bool passive,
        DockPreset dock, DensityPreset density, string? preferredDisplayKey, string? gameDisplayKey)
    {
        this.surface = surface;
        this.native = native;
        this.passive = passive;
        this.dock = dock;
        this.density = density;
        this.preferredDisplayKey = preferredDisplayKey;
        this.gameDisplayKey = gameDisplayKey;
        surface.Configure(passive);
        surface.Opened += OnOpened;
        surface.Closed += OnClosed;
        surface.ScreensChanged += OnChanged;
        surface.ScalingChanged += OnChanged;
        if (surface.IsVisible)
            Refresh();
    }

    private void OnOpened(object? sender, EventArgs args) => Refresh();
    private void OnChanged(object? sender, EventArgs args) => Refresh();
    private void OnClosed(object? sender, EventArgs args) => Dispose();

    private void Refresh()
    {
        // SetWindowPos can synchronously cause a ScalingChanged callback.
        if (disposed || refreshing)
            return;
        refreshing = true;
        try
        {
            surface.Configure(passive);
            var handle = surface.Handle;
            if (handle is null || handle.HandleDescriptor != "HWND" || handle.Handle == 0)
                throw new InvalidOperationException("Windows presentation requires a nonzero HWND platform handle.");

            var displays = surface.Displays;
            var display = CurrentDisplayKey is null
                ? DisplayTopologyPolicy.ResolveInitial(preferredDisplayKey, gameDisplayKey, displays)
                : DisplayTopologyPolicy.ResolveAfterChange(CurrentDisplayKey, gameDisplayKey, displays);
            var bounds = passive ? DockPlacementPolicy.Resolve(display, dock, density) : display.Bounds;
            if (passive)
            {
                var existing = native.ReadExtendedStyle(handle.Handle);
                native.WriteExtendedStyle(handle.Handle, WindowsExtendedStylePolicy.ForPassiveOverlay(existing));
                native.Position(handle.Handle, WindowsNativeMethods.Topmost, default,
                    WindowsNativeMethods.NoMove | WindowsNativeMethods.NoSize |
                    WindowsNativeMethods.NoActivate | WindowsNativeMethods.FrameChanged);
            }
            native.Position(handle.Handle, passive ? WindowsNativeMethods.Topmost : 0, bounds,
                WindowsNativeMethods.NoActivate | (passive ? 0u : WindowsNativeMethods.NoZOrder));
            CurrentDisplayKey = display.Key;
            LastDiagnostic = null;
        }
        catch (Exception error) when (error is Win32Exception or InvalidOperationException or
            ArgumentException or OverflowException or DllNotFoundException or EntryPointNotFoundException)
        {
            LastDiagnostic = $"Windows presentation surface failed: {error.Message}";
            surface.Hide();
        }
        finally
        {
            refreshing = false;
        }
    }

    public bool TryRecover()
    {
        if (disposed)
            return false;
        Refresh();
        return LastDiagnostic is null;
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        surface.Opened -= OnOpened;
        surface.Closed -= OnClosed;
        surface.ScreensChanged -= OnChanged;
        surface.ScalingChanged -= OnChanged;
    }
}
