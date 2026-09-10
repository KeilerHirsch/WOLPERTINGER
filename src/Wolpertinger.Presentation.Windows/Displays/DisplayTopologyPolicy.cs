namespace Wolpertinger.Presentation.Windows.Displays;

public static class DisplayTopologyPolicy
{
    public static DisplayDescriptor ResolveInitial(
        string? preferredDisplayKey,
        string? gameDisplayKey,
        IReadOnlyList<DisplayDescriptor> displays)
    {
        ArgumentNullException.ThrowIfNull(displays);
        EnsureAvailable(displays);

        return Find(displays, preferredDisplayKey)
            ?? Find(displays, gameDisplayKey)
            ?? displays.FirstOrDefault(display => display.IsPrimary)
            ?? displays[0];
    }

    public static DisplayDescriptor ResolveAfterChange(
        string currentDisplayKey,
        string? gameDisplayKey,
        IReadOnlyList<DisplayDescriptor> displays)
    {
        ArgumentNullException.ThrowIfNull(currentDisplayKey);
        ArgumentNullException.ThrowIfNull(displays);
        EnsureAvailable(displays);
        return Find(displays, currentDisplayKey)
            ?? Find(displays, gameDisplayKey)
            ?? displays.FirstOrDefault(display => display.IsPrimary)
            ?? displays[0];
    }

    private static DisplayDescriptor? Find(
        IReadOnlyList<DisplayDescriptor> displays,
        string? key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        return displays.FirstOrDefault(display =>
            string.Equals(display.Key, key, StringComparison.Ordinal));
    }

    private static void EnsureAvailable(IReadOnlyList<DisplayDescriptor> displays)
    {
        if (displays.Count == 0)
            throw new InvalidOperationException("No displays are available.");
    }
}
