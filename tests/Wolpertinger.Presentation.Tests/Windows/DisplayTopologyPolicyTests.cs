using Avalonia;
using Wolpertinger.Presentation.Windows.Displays;

namespace Wolpertinger.Presentation.Tests.Windows;

public sealed class DisplayTopologyPolicyTests
{
    [Fact]
    public void ResolveInitialPrefersConfiguredDisplay()
    {
        var displays = new[] { Primary("MONITOR-1"), Secondary("MONITOR-2") };

        var resolved = DisplayTopologyPolicy.ResolveInitial("MONITOR-2", "MONITOR-1", displays);

        Assert.Equal("MONITOR-2", resolved.Key);
    }

    [Fact]
    public void ResolveInitialFallsBackToKnownGameDisplay()
    {
        var displays = new[] { Primary("MONITOR-1"), Secondary("MONITOR-2") };

        var resolved = DisplayTopologyPolicy.ResolveInitial("MISSING", "MONITOR-2", displays);

        Assert.Equal("MONITOR-2", resolved.Key);
    }
    [Fact]
    public void ResolveInitialFallsBackToPrimaryThenFirstAvailable()
    {
        var withPrimary = new[] { Secondary("MONITOR-2"), Primary("MONITOR-1") };
        var withoutPrimary = new[] { Secondary("MONITOR-2"), Secondary("MONITOR-3") };

        Assert.Equal("MONITOR-1", DisplayTopologyPolicy.ResolveInitial(null, null, withPrimary).Key);
        Assert.Equal("MONITOR-2", DisplayTopologyPolicy.ResolveInitial(null, null, withoutPrimary).Key);
    }

    [Fact]
    public void ResolveInitialFailsClosedWhenNoDisplaysExist()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DisplayTopologyPolicy.ResolveInitial("MONITOR-2", "MONITOR-1", Array.Empty<DisplayDescriptor>()));
    }

    [Fact]
    public void ResolveAfterChangeRetainsCurrentDisplayWhenStillPresent()
    {
        var displays = new[] { Primary("MONITOR-1"), Secondary("MONITOR-2") };

        var resolved = DisplayTopologyPolicy.ResolveAfterChange("MONITOR-2", "MONITOR-1", displays);

        Assert.Equal("MONITOR-2", resolved.Key);
    }
    [Fact]
    public void ResolveAfterChangeRecoversToGameThenPrimaryThenFirstAvailable()
    {
        var gameAvailable = new[] { Primary("MONITOR-1"), Secondary("MONITOR-2") };
        var primaryOnly = new[] { Primary("MONITOR-1") };
        var firstOnly = new[] { Secondary("MONITOR-3") };

        Assert.Equal("MONITOR-2", DisplayTopologyPolicy.ResolveAfterChange("REMOVED", "MONITOR-2", gameAvailable).Key);
        Assert.Equal("MONITOR-1", DisplayTopologyPolicy.ResolveAfterChange("REMOVED", "MISSING", primaryOnly).Key);
        Assert.Equal("MONITOR-3", DisplayTopologyPolicy.ResolveAfterChange("REMOVED", null, firstOnly).Key);
    }

    [Fact]
    public void ResolveAfterChangeDoesNotTeleportWhenOldPreferredDisplayReappears()
    {
        var displays = new[] { Primary("MONITOR-1"), Secondary("MONITOR-2") };

        var resolved = DisplayTopologyPolicy.ResolveAfterChange("MONITOR-1", "MONITOR-1", displays);

        Assert.Equal("MONITOR-1", resolved.Key);
    }

    private static DisplayDescriptor Primary(string key) =>
        new(key, new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1040), 1.0, true);

    private static DisplayDescriptor Secondary(string key) =>
        new(key, new PixelRect(1920, 0, 1920, 1080), new PixelRect(1920, 0, 1920, 1040), 1.0, false);
}
