using Wolpertinger.Presentation.Windows.Interop;

namespace Wolpertinger.Presentation.Tests.Windows;

public sealed class WindowsExtendedStylePolicyTests
{
    [Theory]
    [InlineData(0, 0x080000A0)]
    [InlineData(0x00040000, 0x080400A0)]
    [InlineData(0x00080008, 0x080800A8)]
    public void AddsOnlyPassiveBitsAndPreservesExistingBits(long existing, long expected)
    {
        Assert.Equal((nint)expected, WindowsExtendedStylePolicy.ForPassiveOverlay((nint)existing));
    }

    [Fact]
    public void DoesNotAddAppWindowAndIsIdempotent()
    {
        var result = WindowsExtendedStylePolicy.ForPassiveOverlay(0);
        Assert.Equal((nint)0, result & 0x00040000);
        Assert.Equal(result, WindowsExtendedStylePolicy.ForPassiveOverlay(result));
    }
}
