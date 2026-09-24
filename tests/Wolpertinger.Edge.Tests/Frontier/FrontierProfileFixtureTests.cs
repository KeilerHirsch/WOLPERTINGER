using System.Text.Json;
using System.Text.Json.Nodes;

namespace Wolpertinger.Edge.Tests.Frontier;

public sealed class FrontierProfileFixtureTests
{
    private static readonly string[] ExpectedTopLevelFields =
    [
        "commander",
        "ship",
        "suit",
        "lastSystem",
        "lastStarport",
        "squadron",
        "ships",
        "loadouts",
        "loadout",
        "suits",
    ];

    private static readonly (string Path, ObservedType Type)[] ObservedR0Fields =
    [
        ("commander.id", ObservedType.Number),
        ("commander.name", ObservedType.String),
        ("commander.currentShipId", ObservedType.Number),
        ("commander.alive", ObservedType.Boolean),
        ("commander.docked", ObservedType.Boolean),
        ("commander.onfoot", ObservedType.Boolean),
        ("ship.id", ObservedType.Number),
        ("ship.name", ObservedType.String),
        ("ship.shipID", ObservedType.String),
        ("ship.shipName", ObservedType.String),
        ("ship.alive", ObservedType.Boolean),
        ("ship.starsystem.id", ObservedType.Number),
        ("ship.starsystem.name", ObservedType.String),
        ("ship.station.id", ObservedType.Number),
        ("ship.station.name", ObservedType.String),
    ];

    private static readonly (string Path, JsonValueKind Kind)[] ObservedContainers =
    [
        ("ship.starsystem", JsonValueKind.Object),
        ("ship.station", JsonValueKind.Object),
        ("ship.modules", JsonValueKind.Object),
        ("ship.launchBays", JsonValueKind.Object),
        ("suit.slots", JsonValueKind.Array),
        ("loadout.slots.PrimaryWeapon1.slots.Magazine.modifications", JsonValueKind.Array),
    ];

    [Fact]
    public void SanitizedProfileFixtureParsesAndRetainsObservedShape()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(FixturePath()));
        var root = document.RootElement;

        Assert.True(HasObservedShape(root));
        Assert.Equal(
            ExpectedTopLevelFields.Order(StringComparer.Ordinal),
            root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));

        AssertPrivacyRedacted(root);
        Assert.Equal(0, CountValues(root, JsonValueKind.Null));
    }

    [Fact]
    public void TypeChangedFixtureVariantIsRejectedByObservedShapeContract()
    {
        var node = JsonNode.Parse(File.ReadAllBytes(FixturePath()))!.AsObject();
        node["commander"]!["name"] = JsonValue.Create(123);

        using var malformed = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(node));

        Assert.False(HasObservedShape(malformed.RootElement));
    }

    private static string FixturePath() => Path.Combine(
        AppContext.BaseDirectory,
        "fixtures",
        "capi",
        "profile-r0-sanitized.json");

    private static bool HasObservedShape(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return false;

        var fields = root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);
        if (!ExpectedTopLevelFields.Order(StringComparer.Ordinal).SequenceEqual(fields, StringComparer.Ordinal))
            return false;

        foreach (var (path, expectedType) in ObservedR0Fields)
        {
            var value = Resolve(root, path);
            if (value is null || !MatchesType(value.Value.ValueKind, expectedType))
                return false;
        }

        foreach (var (path, expectedKind) in ObservedContainers)
        {
            var value = Resolve(root, path);
            if (value is null || value.Value.ValueKind != expectedKind)
                return false;
        }

        return true;
    }

    private static bool MatchesType(JsonValueKind actual, ObservedType expected) => expected switch
    {
        ObservedType.String => actual == JsonValueKind.String,
        ObservedType.Number => actual == JsonValueKind.Number,
        ObservedType.Boolean => actual is JsonValueKind.True or JsonValueKind.False,
        _ => false,
    };

    private static JsonElement? Resolve(JsonElement root, string path)
    {
        var value = root;
        foreach (var part in path.Split('.'))
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(part, out value))
                return null;
        }

        return value;
    }

    private static void AssertPrivacyRedacted(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Assert.False(LooksLikePrivateKey(property.Name));
                    AssertPrivacyRedacted(property.Value);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    AssertPrivacyRedacted(item);
                break;
            case JsonValueKind.String:
                Assert.Equal("REDACTED", element.GetString());
                break;
            case JsonValueKind.Number:
                Assert.True(element.TryGetDecimal(out var number));
                Assert.Equal(0m, number);
                break;
            case JsonValueKind.True:
                Assert.Fail("A true scalar was not sanitized.");
                break;
        }
    }

    private static bool LooksLikePrivateKey(string key)
    {
        if (IsRedactedPlaceholder(key))
            return false;

        var normalized = key.Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal);
        return key.Contains('@')
            || key.Contains("://", StringComparison.Ordinal)
            || key.Any(char.IsWhiteSpace)
            || key.All(char.IsDigit)
            || Guid.TryParse(key, out _)
            || (key.Length >= 16 && key.All(char.IsAsciiHexDigit))
            || normalized.Contains("token", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("password", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("authorizationcode", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("codeverifier", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("sharedkey", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRedactedPlaceholder(string key) =>
        key == "REDACTED_KEY"
        || HasNumberedSuffix(key, "REDACTED_KEY_")
        || HasNumberedSuffix(key, "REDACTED_FIELD_KEY_");

    private static bool HasNumberedSuffix(string value, string prefix) =>
        value.StartsWith(prefix, StringComparison.Ordinal)
        && value.Length > prefix.Length
        && value[prefix.Length..].All(char.IsAsciiDigit);

    private static int CountValues(JsonElement element, JsonValueKind kind)
    {
        if (element.ValueKind == JsonValueKind.Object)
            return element.EnumerateObject().Sum(property => CountValues(property.Value, kind));
        if (element.ValueKind == JsonValueKind.Array)
            return element.EnumerateArray().Sum(item => CountValues(item, kind));
        return element.ValueKind == kind ? 1 : 0;
    }

    private enum ObservedType
    {
        String,
        Number,
        Boolean,
    }
}
