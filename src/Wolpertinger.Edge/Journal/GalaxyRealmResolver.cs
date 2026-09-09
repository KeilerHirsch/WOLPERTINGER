using Wolpertinger.Edge.Contracts;

namespace Wolpertinger.Edge.Journal;

public static class GalaxyRealmResolver
{
    public static GalaxyRealm Resolve(string? gameVersion, string? build)
    {
        if (ContainsTestMarker(gameVersion) || ContainsTestMarker(build))
        {
            return GalaxyRealm.BetaOrPts;
        }

        if (string.IsNullOrWhiteSpace(gameVersion))
        {
            return GalaxyRealm.Unknown;
        }

        var parts = gameVersion.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor))
        {
            return GalaxyRealm.Unknown;
        }

        return major switch
        {
            >= 4 => GalaxyRealm.Live,
            3 when minor == 8 => GalaxyRealm.Legacy,
            _ => GalaxyRealm.Unknown,
        };
    }

    private static bool ContainsTestMarker(string? value)
        => value?.Contains("beta", StringComparison.OrdinalIgnoreCase) == true
           || value?.Contains("test", StringComparison.OrdinalIgnoreCase) == true
           || value?.Contains("pts", StringComparison.OrdinalIgnoreCase) == true;
}
