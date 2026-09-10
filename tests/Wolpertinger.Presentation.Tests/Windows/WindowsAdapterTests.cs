using Avalonia;
using Avalonia.Platform;
using Wolpertinger.Presentation.ViewModels;
using Wolpertinger.Presentation.Windows;
using Wolpertinger.Presentation.Windows.Displays;
using Wolpertinger.Presentation.Windows.Interop;

namespace Wolpertinger.Presentation.Tests.Windows;

public sealed class WindowsAdapterTests
{
    [Theory]
    [InlineData(null, 1)]
    [InlineData("hwnd", 1)]
    [InlineData("XID", 1)]
    [InlineData("HWND", 0)]
    public void InvalidHandleFailsClosedBeforeAnyNativeCall(string? descriptor, int value)
    {
        var surface = new FakeSurface { Handle = descriptor is null ? null : new PlatformHandle(value, descriptor) };
        var native = new FakeNative();
        using var attachment = Attach(surface, native);
        surface.Open();
        Assert.Empty(native.Calls);
        Assert.NotNull(attachment.LastDiagnostic);
        Assert.True(surface.Hidden);
    }

    [Fact]
    public void OverlaySetsStylesAndTopmostWithoutActivationBeforePlacement()
    {
        var surface = new FakeSurface();
        var native = new FakeNative();
        using var attachment = Attach(surface, native);
        Assert.True(surface.Passive);
        Assert.Empty(native.Calls);
        surface.Open();
        Assert.Equal(new[] { "read", "style", "position", "position" }, native.Calls);
        Assert.Equal((nint)0x080400A0, native.Style);
        Assert.Equal((nint)(-1), native.Positions[0].After);
        Assert.Equal(0x0033u, native.Positions[0].Flags);
        Assert.Equal(0x0010u, native.Positions[1].Flags);
        Assert.Equal(DockPlacementPolicy.Resolve(surface.Displays[1], DockPreset.Right, DensityPreset.Standard), native.Positions[1].Bounds);
        Assert.Null(attachment.LastDiagnostic);
    }

    [Fact]
    public void HubUsesFullBoundsAndNeverReadsOrWritesPassiveStyles()
    {
        var surface = new FakeSurface();
        var native = new FakeNative();
        using var attachment = Attach(surface, native, passive: false);
        Assert.False(surface.Passive);
        surface.Open();
        Assert.Equal(new[] { "position" }, native.Calls);
        Assert.Equal(surface.Displays[1].Bounds, native.Positions[0].Bounds);
        Assert.Equal(0x0014u, native.Positions[0].Flags);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ScreenRemovalRecoversAndReappearanceDoesNotTeleport(bool passive)
    {
        var surface = new FakeSurface();
        var native = new FakeNative();
        using var attachment = Attach(surface, native, passive);
        surface.Open();
        var original = surface.Displays;
        surface.Displays = [original[0]];
        surface.ChangeScreens();
        Assert.Equal("primary", attachment.CurrentDisplayKey);
        surface.Displays = original;
        surface.ChangeScreens();
        Assert.Equal("primary", attachment.CurrentDisplayKey);
        Assert.Equal(passive ? DockPlacementPolicy.Resolve(original[0], DockPreset.Right, DensityPreset.Standard) : original[0].Bounds,
            native.Positions[^1].Bounds);
    }

    [Fact]
    public void ScalingEventRecalculatesFromCurrentScreenData()
    {
        var surface = new FakeSurface();
        var native = new FakeNative();
        using var attachment = Attach(surface, native);
        surface.Open();
        surface.Displays = [surface.Displays[1] with { Scaling = 1.5 }];
        surface.ChangeScaling();
        Assert.Equal(DockPlacementPolicy.Resolve(surface.Displays[0], DockPreset.Right, DensityPreset.Standard), native.Positions[^1].Bounds);
    }

    [Fact]
    public void MissingDisplaysAndNativeFailureProduceDiagnosticAndHide()
    {
        var surface = new FakeSurface { Displays = [] };
        var native = new FakeNative();
        using var attachment = Attach(surface, native);
        surface.Open();
        Assert.Empty(native.Calls);
        Assert.True(surface.Hidden);
        Assert.Contains("No displays", attachment.LastDiagnostic!);

        var failingSurface = new FakeSurface();
        using var failing = Attach(failingSurface, new FakeNative { Fail = true });
        failingSurface.Open();
        Assert.True(failingSurface.Hidden);
        Assert.Contains("native failure", failing.LastDiagnostic!);
    }

    [Fact]
    public void HiddenSurfaceReevaluatesPlacementWhenDisplayReturns()
    {
        var surface = new FakeSurface();
        var native = new FakeNative();
        using var attachment = Attach(surface, native);
        surface.Open();
        var primary = surface.Displays[0];

        surface.Displays = [];
        surface.ChangeScreens();
        Assert.True(surface.Hidden);
        Assert.NotNull(attachment.LastDiagnostic);

        surface.Displays = [primary];
        surface.ChangeScreens();

        Assert.Equal("primary", attachment.CurrentDisplayKey);
        Assert.Null(attachment.LastDiagnostic);
        Assert.Equal(DockPlacementPolicy.Resolve(primary, DockPreset.Right, DensityPreset.Standard), native.Positions[^1].Bounds);
    }

    [Fact]
    public void ClosedAndDisposedAttachmentsStopRespondingToEvents()
    {
        var surface = new FakeSurface();
        var native = new FakeNative();
        var attachment = Attach(surface, native);
        surface.Open();
        surface.Close();
        var count = native.Calls.Count;
        surface.ChangeScreens();
        surface.ChangeScaling();
        surface.Open();
        attachment.Dispose();
        Assert.Equal(count, native.Calls.Count);
    }

    private static WindowsSurfaceAttachment Attach(FakeSurface surface, FakeNative native, bool passive = true) =>
        new(surface, native, passive, DockPreset.Right, DensityPreset.Standard, "secondary", "primary");

    private sealed class FakeSurface : IWindowsSurface
    {
        public event EventHandler? Opened;
        public event EventHandler? Closed;
        public event EventHandler? ScreensChanged;
        public event EventHandler? ScalingChanged;
        public IPlatformHandle? Handle { get; set; } = new PlatformHandle(42, "HWND");
        public IReadOnlyList<DisplayDescriptor> Displays { get; set; } =
        [
            new("primary", new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1040), 1, true),
            new("secondary", new PixelRect(-1920, 0, 1920, 1080), new PixelRect(-1920, 0, 1920, 1000), 1, false)
        ];
        public bool IsVisible { get; private set; }
        public bool Passive { get; private set; }
        public bool Hidden { get; private set; }
        public void Configure(bool passive) => Passive = passive;
        public void Hide() { Hidden = true; IsVisible = false; }
        public void Open() { IsVisible = true; Opened?.Invoke(this, EventArgs.Empty); }
        public void Close() { IsVisible = false; Closed?.Invoke(this, EventArgs.Empty); }
        public void ChangeScreens() => ScreensChanged?.Invoke(this, EventArgs.Empty);
        public void ChangeScaling() => ScalingChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class FakeNative : IWindowsNativeApi
    {
        public List<string> Calls { get; } = [];
        public List<(nint After, PixelRect Bounds, uint Flags)> Positions { get; } = [];
        public nint Style { get; private set; } = 0x00040000;
        public bool Fail { get; init; }
        public nint ReadExtendedStyle(nint handle) { Calls.Add("read"); return Style; }
        public void WriteExtendedStyle(nint handle, nint style) { Calls.Add("style"); Style = style; }
        public void Position(nint handle, nint after, PixelRect bounds, uint flags)
        {
            Calls.Add("position");
            if (Fail) throw new System.ComponentModel.Win32Exception("native failure");
            Positions.Add((after, bounds, flags));
        }
    }
}
