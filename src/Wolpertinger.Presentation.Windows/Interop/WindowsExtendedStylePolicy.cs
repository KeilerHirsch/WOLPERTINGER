namespace Wolpertinger.Presentation.Windows.Interop;

public static class WindowsExtendedStylePolicy
{
    private const nint Transparent = 0x00000020;
    private const nint ToolWindow = 0x00000080;
    private const nint NoActivate = 0x08000000;

    public static nint ForPassiveOverlay(nint existing) => existing | Transparent | ToolWindow | NoActivate;
}
