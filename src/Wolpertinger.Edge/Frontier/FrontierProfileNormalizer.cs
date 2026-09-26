using System.Text.Json;
using Wolpertinger.Edge.Contracts;
using Wolpertinger.Edge.Evidence;
using Wolpertinger.Edge.Journal;

namespace Wolpertinger.Edge.Frontier;

public static class FrontierProfileNormalizer
{
    public static ObservationEnvelopeDraft Normalize(
        RawEvidenceReceipt receipt,
        JsonElement root,
        SessionBinding binding)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(binding);
        if (!receipt.IsDurable)
            throw new InvalidOperationException("Frontier profile normalization requires durable raw evidence.");

        var provenance = receipt.SourceKind switch
        {
            RawEvidenceSourceKind.FrontierApi => SourceProvenance.FrontierApi,
            RawEvidenceSourceKind.Sample => SourceProvenance.Sample,
            _ => throw new InvalidDataException("Commander/Vessel normalization requires FrontierApi or explicit Sample evidence."),
        };

        var commander = RequireObject(root, "commander");
        var ship = RequireObject(root, "ship");
        var commanderName = RequireCommanderName(commander, "name");
        var commanderAlive = RequireBoolean(commander, "alive");
        var commanderDocked = RequireBoolean(commander, "docked");
        var commanderOnFoot = RequireBoolean(commander, "onfoot");
        var vesselName = RequireVesselName(ship, "name");
        var vesselAlive = RequireBoolean(ship, "alive");

        return new ObservationEnvelopeDraft(
            receipt.EvidenceDigest,
            binding.SessionId,
            binding.Profile,
            ObservationKind.CommanderVessel,
            SourceTimeUnixMs: null,
            receipt.ObservedUtc.ToUnixTimeMilliseconds(),
            receipt.CommitUtc.ToUnixTimeMilliseconds(),
            MessageCount: 1,
            new CommanderVesselPayload(
                commanderName,
                commanderAlive,
                commanderDocked,
                commanderOnFoot,
                vesselName,
                vesselAlive),
            provenance,
            KernelProtocol.R0Version);
    }

    private static JsonElement RequireObject(JsonElement root, string propertyName)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"Profile field {propertyName} must be an object.");
        }

        return value;
    }

    private static CommanderName RequireCommanderName(JsonElement parent, string propertyName)
    {
        var value = RequireString(parent, propertyName, "commander");
        if (!CommanderName.TryCreate(value, out var name))
            throw new InvalidDataException("commander.name must be valid UTF-8 containing 1..128 bytes.");
        return name;
    }

    private static VesselName RequireVesselName(JsonElement parent, string propertyName)
    {
        var value = RequireString(parent, propertyName, "ship");
        if (!VesselName.TryCreate(value, out var name))
            throw new InvalidDataException("ship.name must be valid UTF-8 containing 1..128 bytes.");
        return name;
    }

    private static string RequireString(JsonElement parent, string propertyName, string section)
    {
        if (!parent.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.String
            || value.GetString() is not { } text)
        {
            throw new InvalidDataException($"Profile field {section}.{propertyName} must be a string.");
        }

        return text;
    }

    private static bool RequireBoolean(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value)
            || value.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
        {
            throw new InvalidDataException($"Profile field {propertyName} must be a boolean.");
        }

        return value.GetBoolean();
    }
}
