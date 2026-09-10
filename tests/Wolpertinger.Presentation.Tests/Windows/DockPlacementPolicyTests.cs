using Avalonia;
using Wolpertinger.Presentation.ViewModels;
using Wolpertinger.Presentation.Windows.Displays;

namespace Wolpertinger.Presentation.Tests.Windows;

public sealed class DockPlacementPolicyTests
{
    [Theory]
    [InlineData(DockPreset.Left, DensityPreset.Compact, 1.0, 116, 570, 320, 160)]
    [InlineData(DockPreset.Right, DensityPreset.Expanded, 1.25, 1505, 450, 575, 400)]
    [InlineData(DockPreset.Top, DensityPreset.Compact, 1.5, 680, 74, 840, 156)]
    [InlineData(DockPreset.Bottom, DensityPreset.Standard, 2.0, 380, 930, 1440, 288)]
    public void ResolveUsesScaledLogicalSizeAndDockAlignment(
        DockPreset dock,
        DensityPreset density,
        double scaling,
        int x,
        int y,
        int width,
        int height)
    {
        var display = Display(scaling, new PixelRect(100, 50, 2000, 1200));

        var actual = DockPlacementPolicy.Resolve(display, dock, density);

        Assert.Equal(new PixelRect(x, y, width, height), actual);
    }
    [Fact]
    public void ResolveClampsOversizedSurfaceToWorkingArea()
    {
        var workingArea = new PixelRect(10, 20, 200, 100);
        var display = Display(2.0, workingArea);

        var actual = DockPlacementPolicy.Resolve(display, DockPreset.Right, DensityPreset.Expanded);

        Assert.Equal(workingArea, actual);
    }

    [Fact]
    public void ResolveRecomputesFromLogicalDimensionsAfterDpiChange()
    {
        var workingArea = new PixelRect(100, 50, 2000, 1200);
        var atOne = DockPlacementPolicy.Resolve(Display(1.0, workingArea), DockPreset.Right, DensityPreset.Standard);
        var atOnePointFive = DockPlacementPolicy.Resolve(Display(1.5, workingArea), DockPreset.Right, DensityPreset.Standard);

        Assert.Equal(new PixelRect(1704, 540, 380, 220), atOne);
        Assert.Equal(new PixelRect(1506, 485, 570, 330), atOnePointFive);
    }

    private static DisplayDescriptor Display(double scaling, PixelRect workingArea) =>
        new("MONITOR-1", workingArea, workingArea, scaling, true);
}
