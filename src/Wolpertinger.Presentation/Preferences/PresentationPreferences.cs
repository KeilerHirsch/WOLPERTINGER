using Wolpertinger.Presentation.ViewModels;

namespace Wolpertinger.Presentation.Preferences;

public sealed record PresentationPreferences(
    bool OverlayVisible,
    bool HubVisible,
    DockPreset Dock,
    DensityPreset Density,
    string? PreferredDisplayKey)
{
    public static PresentationPreferences Default { get; } =
        new(true, false, DockPreset.Right, DensityPreset.Standard, null);
}
