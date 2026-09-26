using System.Formats.Cbor;
using System.Text;
using Wolpertinger.Edge.Contracts;

namespace Wolpertinger.Edge.Tests.Contracts;

public sealed class CommanderVesselCborContractTests
{
    [Theory]
    [InlineData(SourceProvenance.FrontierApi)]
    [InlineData(SourceProvenance.Sample)]
    public void VersionTwoCommanderVesselRoundTripsOnlyTheSixSelectedFields(SourceProvenance provenance)
    {
        var observation = Observation(2, provenance);

        var encoded = CborContractCodec.Encode(observation);
        Assert.Equal(encoded, CborContractCodec.Encode(observation));
        var decoded = CborContractCodec.DecodeObservation(encoded);
        var payload = Assert.IsType<CommanderVesselPayload>(decoded.Payload);

        Assert.Equal(observation, decoded);
        Assert.Equal(ObservationKind.CommanderVessel, decoded.Kind);
        Assert.Equal(KernelProtocol.R0Version, decoded.ProtocolVersion);
        Assert.Equal(provenance, decoded.Provenance);
        Assert.Equal(
            ["CommanderAlive", "CommanderDocked", "CommanderName", "CommanderOnFoot", "ShipAlive", "VesselName"],
            typeof(CommanderVesselPayload).GetProperties().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal("Test commander", payload.CommanderName.Value);
        Assert.Equal("Test vessel", payload.VesselName.Value);
    }

    [Fact]
    public void VersionOneRejectsCommanderVesselPayload()
    {
        Assert.Throws<InvalidDataException>(
            () => CborContractCodec.Encode(Observation(KernelProtocol.Stage1Version, SourceProvenance.FrontierApi)));
    }

    [Fact]
    public void VersionTwoRejectsNonFrontierAndNonSampleCommanderVesselEvidence()
    {
        Assert.Throws<InvalidDataException>(
            () => CborContractCodec.Encode(Observation(KernelProtocol.R0Version, SourceProvenance.LocalJournal)));
    }

    [Fact]
    public void VersionTwoMatchesTheCanonicalCommanderVesselGoldenVector()
    {
        var encoded = CborContractCodec.Encode(Observation(KernelProtocol.R0Version, SourceProvenance.Sample));

        Assert.Equal(GoldenVector(), encoded);
        Assert.Equal(GoldenVector(), CborContractCodec.Encode(Observation(KernelProtocol.R0Version, SourceProvenance.Sample)));
    }

    [Theory]
    [InlineData("Test commander")]
    [InlineData("Test vessel")]
    public void DecodeRejectsMalformedUtf8InEitherName(string name)
    {
        var encoded = CborContractCodec.Encode(Observation(KernelProtocol.R0Version, SourceProvenance.Sample));
        var marker = Encoding.UTF8.GetBytes(name);
        var offset = encoded.AsSpan().IndexOf(marker);
        Assert.True(offset >= 1);
        encoded[offset] = 0xFF;

        Assert.Throws<InvalidDataException>(() => CborContractCodec.DecodeObservation(encoded));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DecodeRejectsA129ByteName(bool commander)
    {
        var text = new string(commander ? 'c' : 'v', CommanderName.MaximumUtf8Bytes);
        var nameBytes = Encoding.UTF8.GetBytes(text);
        var commanderName = commander ? text : "Commander";
        var vesselName = commander ? "Vessel" : text;
        var encoded = CborContractCodec.Encode(
            Observation(KernelProtocol.R0Version, SourceProvenance.Sample, commanderName, vesselName));
        var marker = new byte[2 + nameBytes.Length];
        marker[0] = 0x78;
        marker[1] = (byte)CommanderName.MaximumUtf8Bytes;
        nameBytes.CopyTo(marker, 2);
        var offset = encoded.AsSpan().IndexOf(marker);
        Assert.True(offset >= 0);

        var malformed = new byte[encoded.Length + 1];
        encoded.AsSpan(0, offset).CopyTo(malformed);
        malformed[offset] = 0x78;
        malformed[offset + 1] = 0x81;
        encoded.AsSpan(offset + 2, nameBytes.Length).CopyTo(malformed.AsSpan(offset + 2));
        malformed[offset + 2 + nameBytes.Length] = (byte)'c';
        encoded.AsSpan(offset + 2 + nameBytes.Length).CopyTo(malformed.AsSpan(offset + 3 + nameBytes.Length));

        Assert.Throws<InvalidDataException>(() => CborContractCodec.DecodeObservation(malformed));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DecodeRejectsAnEmptyName(bool commander)
    {
        const string name = "EmptyProbe";
        var textBytes = Encoding.UTF8.GetBytes(name);
        var marker = new byte[textBytes.Length + 1];
        marker[0] = (byte)(0x60 | textBytes.Length);
        textBytes.CopyTo(marker, 1);
        var encoded = CborContractCodec.Encode(
            Observation(
                KernelProtocol.R0Version,
                SourceProvenance.Sample,
                commander ? name : "Commander",
                commander ? "Vessel" : name));
        var offset = encoded.AsSpan().IndexOf(marker);
        Assert.True(offset >= 0);

        var malformed = new byte[encoded.Length - textBytes.Length];
        encoded.AsSpan(0, offset).CopyTo(malformed);
        malformed[offset] = 0x60;
        encoded.AsSpan(offset + marker.Length).CopyTo(malformed.AsSpan(offset + 1));

        Assert.Throws<InvalidDataException>(() => CborContractCodec.DecodeObservation(malformed));
    }

    [Theory]
    [InlineData((int)SourceProvenance.FrontierApi, (int)FreshnessState.Current, true)]
    [InlineData((int)SourceProvenance.Sample, (int)FreshnessState.Current, true)]
    [InlineData((int)SourceProvenance.LocalJournal, (int)FreshnessState.Current, false)]
    [InlineData((int)SourceProvenance.FrontierApi, (int)FreshnessState.Stale, false)]
    public void DecodeAcceptsOnlyCurrentFrontierOrSampleCommanderVesselFacts(
        int provenance,
        int freshness,
        bool accepted)
    {
        var encoded = KernelResponseV2(provenance, freshness);

        if (accepted)
        {
            var decoded = CborContractCodec.DecodeKernelResponse(encoded);
            Assert.NotNull(decoded.CommanderVesselFact);
        }
        else
        {
            Assert.Throws<InvalidDataException>(() => CborContractCodec.DecodeKernelResponse(encoded));
        }
    }

    private static ObservationEnvelope Observation(
        int version,
        SourceProvenance provenance,
        string commanderName = "Test commander",
        string vesselName = "Test vessel")
    {
        Assert.True(CommanderName.TryCreate(commanderName, out var commander));
        Assert.True(VesselName.TryCreate(vesselName, out var vessel));
        return new ObservationEnvelope(
            new ObservationCursor(2, 0),
            FixedBytes32.FromHex("202122232425262728292a2b2c2d2e2f303132333435363738393a3b3c3d3e3f"),
            FixedBytes16.FromHex("a0a1a2a3a4a5a6a7a8a9aaabacadaeaf"),
            new ProfileKey("FTEST0001", GalaxyRealm.Live, 17),
            ObservationKind.CommanderVessel,
            null,
            1_700_000_001_100,
            1_700_000_001_200,
            1,
            new CommanderVesselPayload(commander, true, false, true, vessel, true),
            provenance,
            version);
    }

    private static byte[] GoldenVector() => Convert.FromHexString(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "contracts", "v2", "commander-vessel.hex")).Trim());

    private static byte[] KernelResponseV2(int provenance, int freshness)
    {
        var writer = new CborWriter(CborConformanceMode.Canonical);
        writer.WriteStartMap(8);
        writer.WriteInt32(0); writer.WriteInt32((int)KernelResponseKind.Apply);
        writer.WriteInt32(1); writer.WriteInt32((int)KernelResponseStatus.Ok);
        writer.WriteInt32(2); writer.WriteUInt64(1);
        writer.WriteInt32(3);
        writer.WriteStartArray(2);
        writer.WriteUInt64(2);
        writer.WriteUInt32(0);
        writer.WriteEndArray();
        writer.WriteInt32(4); writer.WriteByteString(new byte[32]);
        writer.WriteInt32(5); writer.WriteNull();
        writer.WriteInt32(6); writer.WriteInt32((int)KernelRole.Active);
        writer.WriteInt32(7);
        writer.WriteStartArray(9);
        writer.WriteStartArray(2);
        writer.WriteUInt64(2);
        writer.WriteUInt32(0);
        writer.WriteEndArray();
        writer.WriteTextString("Test commander");
        writer.WriteBoolean(true);
        writer.WriteBoolean(false);
        writer.WriteBoolean(true);
        writer.WriteTextString("Test vessel");
        writer.WriteBoolean(true);
        writer.WriteInt32(provenance);
        writer.WriteInt32(freshness);
        writer.WriteEndArray();
        writer.WriteEndMap();
        return writer.Encode();
    }
}
