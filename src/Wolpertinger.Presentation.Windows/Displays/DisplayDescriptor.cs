using Avalonia;

namespace Wolpertinger.Presentation.Windows.Displays;

public sealed record DisplayDescriptor(
    string Key,
    PixelRect Bounds,
    PixelRect WorkingArea,
    double Scaling,
    bool IsPrimary);
