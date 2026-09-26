using System.Text.Json;
using System.Text.Json.Nodes;
using Wolpertinger.Edge.Contracts;
using Wolpertinger.Edge.Evidence;
using Wolpertinger.Edge.Frontier;
using Wolpertinger.Edge.Journal;

namespace Wolpertinger.Edge.Tests.Frontier;

public sealed class CommanderVesselNormalizerTests
{
    public static IEnumerable<object[]> NameBoundaryCases
    {
        get
        {
            yield return ["one UTF-8 byte", "a", true, 1];
            yield return ["exactly 128 ASCII bytes", new string('a', 128), true, 128];
            yield return ["129 ASCII bytes", new string('a', 129), false, 0];
            yield return ["multibyte within bound", new string('é', 64), true, 128];
            yield return ["multibyte over bound", new string('é', 65), false, 0];
            yield return ["empty", string.Empty, false, 0];
        }
    }

    [Theory]
    [MemberData(nameof(NameBoundaryCases))]
    public void CommanderNameEnforcesUtf8ByteBoundaries(
        string caseName,
        string input,
        bool accepted,
        int? byteLength)
    {
        Assert.NotEmpty(caseName);
        Assert.Equal(accepted, CommanderName.TryCreate(input, out var name));
        if (accepted)
            Assert.Equal(byteLength, name.Utf8ByteLength);
    }

    [Theory]
    [MemberData(nameof(NameBoundaryCases))]
    public void VesselNameEnforcesUtf8ByteBoundaries(
        string caseName,
        string input,
        bool accepted,
        int? byteLength)
    {
        Assert.NotEmpty(caseName);
        Assert.Equal(accepted, VesselName.TryCreate(input, out var name));
        if (accepted)
            Assert.Equal(byteLength, name.Utf8ByteLength);
    }

    [Fact]
    public void CommanderNameRejectsAnUnpairedSurrogate()
    {
        var malformed = new string((char)0xD800, 1);

        Assert.False(CommanderName.TryCreate(malformed, out _));
    }

    [Fact]
    public void VesselNameRejectsAnUnpairedSurrogate()
    {
        var malformed = new string((char)0xD800, 1);

        Assert.False(VesselName.TryCreate(malformed, out _));
    }

    [Fact]
    public void CommanderAndVesselNamesRemainDistinctDomainTypes()
        => Assert.NotEqual(typeof(CommanderName), typeof(VesselName));

    [Fact]
    public void NormalizesOnlySelectedFieldsAndBindsDurableEvidenceToProfile()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllBytes(FixturePath()));
        var receipt = Receipt(RawEvidenceSourceKind.FrontierApi, isDurable: true);
        var binding = Binding();

        var draft = FrontierProfileNormalizer.Normalize(receipt, fixture.RootElement, binding);
        var repeatedDraft = FrontierProfileNormalizer.Normalize(receipt, fixture.RootElement, binding);
        var payload = Assert.IsType<CommanderVesselPayload>(draft.Payload);
        var commander = fixture.RootElement.GetProperty("commander");
        var ship = fixture.RootElement.GetProperty("ship");

        Assert.Equal(draft, repeatedDraft);
        Assert.Equal(ObservationKind.CommanderVessel, draft.Kind);
        Assert.Equal(KernelProtocol.R0Version, draft.ProtocolVersion);
        Assert.Equal(SourceProvenance.FrontierApi, draft.Provenance);
        Assert.Equal(receipt.EvidenceDigest, draft.EvidenceDigest);
        Assert.Equal(binding.SessionId, draft.SessionId);
        Assert.Equal(binding.Profile, draft.Profile);
        Assert.InRange(payload.CommanderName.Utf8ByteLength, 1, CommanderName.MaximumUtf8Bytes);
        Assert.InRange(payload.VesselName.Utf8ByteLength, 1, VesselName.MaximumUtf8Bytes);
        Assert.Equal(commander.GetProperty("name").GetString(), payload.CommanderName.Value);
        Assert.Equal(commander.GetProperty("alive").GetBoolean(), payload.CommanderAlive);
        Assert.Equal(commander.GetProperty("docked").GetBoolean(), payload.CommanderDocked);
        Assert.Equal(commander.GetProperty("onfoot").GetBoolean(), payload.CommanderOnFoot);
        Assert.Equal(ship.GetProperty("name").GetString(), payload.VesselName.Value);
        Assert.Equal(ship.GetProperty("alive").GetBoolean(), payload.ShipAlive);
        Assert.Equal(
            ["CommanderAlive", "CommanderDocked", "CommanderName", "CommanderOnFoot", "ShipAlive", "VesselName"],
            typeof(CommanderVesselPayload).GetProperties().Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void NormalizerAcceptsSampleOnlyWhenExplicitlyDurable()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllBytes(FixturePath()));

        var draft = FrontierProfileNormalizer.Normalize(
            Receipt(RawEvidenceSourceKind.Sample, isDurable: true),
            fixture.RootElement,
            Binding());

        Assert.Equal(SourceProvenance.Sample, draft.Provenance);
        Assert.Equal(KernelProtocol.R0Version, draft.ProtocolVersion);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NormalizerRejectsNonDurableEvidence(bool sample)
    {
        using var fixture = JsonDocument.Parse(File.ReadAllBytes(FixturePath()));
        var source = sample ? RawEvidenceSourceKind.Sample : RawEvidenceSourceKind.FrontierApi;

        Assert.Throws<InvalidOperationException>(
            () => FrontierProfileNormalizer.Normalize(Receipt(source, isDurable: false), fixture.RootElement, Binding()));
    }

    [Fact]
    public void NormalizerRejectsUnapprovedEvidenceSource()
    {
        using var fixture = JsonDocument.Parse(File.ReadAllBytes(FixturePath()));

        Assert.Throws<InvalidDataException>(
            () => FrontierProfileNormalizer.Normalize(Receipt(RawEvidenceSourceKind.LocalJournal, isDurable: true), fixture.RootElement, Binding()));
    }

    [Theory]
    [InlineData("commander")]
    [InlineData("ship")]
    public void NormalizerRejectsOverlongNameWithoutProducingDraft(string section)
    {
        var root = JsonNode.Parse(File.ReadAllBytes(FixturePath()))!.AsObject();
        root[section]!["name"] = new string('a', 129);
        using var malformed = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(root));

        Assert.Throws<InvalidDataException>(
            () => FrontierProfileNormalizer.Normalize(Receipt(RawEvidenceSourceKind.Sample, true), malformed.RootElement, Binding()));
    }

    [Theory]
    [InlineData("commander")]
    [InlineData("ship")]
    public void NormalizerRejectsEmptyNameWithoutProducingDraft(string section)
    {
        var root = JsonNode.Parse(File.ReadAllBytes(FixturePath()))!.AsObject();
        root[section]!["name"] = string.Empty;
        using var malformed = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(root));

        Assert.Throws<InvalidDataException>(
            () => FrontierProfileNormalizer.Normalize(Receipt(RawEvidenceSourceKind.Sample, true), malformed.RootElement, Binding()));
    }

    private static string FixturePath() => Path.Combine(
        AppContext.BaseDirectory,
        "fixtures",
        "capi",
        "profile-r0-sanitized.json");

    private static SessionBinding Binding()
        => new(
            FixedBytes16.FromHex("a0a1a2a3a4a5a6a7a8a9aaabacadaeaf"),
            new ProfileKey("FTEST0001", GalaxyRealm.Live, 17));

    private static RawEvidenceReceipt Receipt(RawEvidenceSourceKind source, bool isDurable)
        => new(
            new EvidenceReference(8, 0, 800, 128),
            source,
            FixedBytes32.FromHex("303132333435363738393a3b3c3d3e3f404142434445464748494a4b4c4d4e4f"),
            DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_001_000),
            DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_001_200),
            isDurable);
}
