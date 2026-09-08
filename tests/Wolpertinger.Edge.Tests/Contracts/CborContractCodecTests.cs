using Wolpertinger.Edge.Contracts;

namespace Wolpertinger.Edge.Tests.Contracts;

public sealed class CborContractCodecTests
{
    [Fact]
    public void SessionBoundMatchesGoldenVectorAndRoundTrips()
    {
        var observation = new ObservationEnvelope(
            new ObservationCursor(1, 0),
            FixedBytes32.FromHex("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f"),
            FixedBytes16.FromHex("a0a1a2a3a4a5a6a7a8a9aaabacadaeaf"),
            new ProfileKey("FTEST0001", GalaxyRealm.Live, 0),
            ObservationKind.SessionBound,
            1_700_000_000_000,
            1_700_000_000_100,
            1_700_000_000_200,
            1,
            SessionBoundPayload.Instance,
            SourceProvenance.LocalJournal);

        AssertGoldenRoundTrip("session-bound.hex", observation);
    }

    [Fact]
    public void FsdJumpMatchesGoldenVectorAndRoundTrips()
    {
        var observation = new ObservationEnvelope(
            new ObservationCursor(2, 0),
            FixedBytes32.FromHex("202122232425262728292a2b2c2d2e2f303132333435363738393a3b3c3d3e3f"),
            FixedBytes16.FromHex("a0a1a2a3a4a5a6a7a8a9aaabacadaeaf"),
            new ProfileKey("FTEST0001", GalaxyRealm.Live, 0),
            ObservationKind.FsdJump,
            1_700_000_001_000,
            1_700_000_001_100,
            1_700_000_001_200,
            1,
            new FsdJumpPayload(
                "W. Grantler NX-42",
                1_234_567_890_123_456_789UL,
                new GalacticPosition(new(12345, -3), new(-6789, -2), new(42, 0)),
                new Decimal64(55359, -3),
                new Decimal64(4843642, -6),
                new Decimal64(27123, -3)),
            SourceProvenance.LocalJournal);

        AssertGoldenRoundTrip("fsdjump.hex", observation);
    }

    [Fact]
    public void DecodeRejectsTrailingBytes()
    {
        var valid = ReadVector("session-bound.hex");
        var malformed = valid.Concat(new byte[] { 0x00 }).ToArray();

        Assert.Throws<InvalidDataException>(() => CborContractCodec.DecodeObservation(malformed));
    }

    [Fact]
    public void DecodeRejectsUnknownEnvelopeKey()
    {
        var valid = ReadVector("session-bound.hex");
        var malformed = new byte[valid.Length + 2];
        valid.CopyTo(malformed, 0);
        malformed[0] = 0xAE; // canonical map(14), original vector is map(13)
        malformed[^2] = 0x0D;
        malformed[^1] = 0x00;

        Assert.Throws<InvalidDataException>(() => CborContractCodec.DecodeObservation(malformed));
    }

    [Fact]
    public void DecodeRejectsIndefiniteContainerAndTags()
    {
        Assert.Throws<InvalidDataException>(() => CborContractCodec.DecodeObservation(new byte[] { 0xBF, 0xFF }));

        var tagged = new byte[] { 0xC0 }.Concat(ReadVector("session-bound.hex")).ToArray();
        Assert.Throws<InvalidDataException>(() => CborContractCodec.DecodeObservation(tagged));
    }

    [Fact]
    public void DecodeRejectsInvalidUtf8()
    {
        var malformed = ReadVector("fsdjump.hex");
        var marker = System.Text.Encoding.UTF8.GetBytes("W. Grantler NX-42");
        var offset = malformed.AsSpan().IndexOf(marker);
        Assert.True(offset >= 0);
        malformed[offset] = 0xFF;

        Assert.Throws<InvalidDataException>(() => CborContractCodec.DecodeObservation(malformed));
    }

    [Fact]
    public void DecodeRejectsPayloadAboveMaximumBeforeParsing()
    {
        var oversized = new byte[KernelProtocol.MaximumPayloadBytes + 1];
        oversized[0] = 0xAD;

        var error = Assert.Throws<InvalidDataException>(() => CborContractCodec.DecodeObservation(oversized));
        Assert.Contains("maximum", error.Message, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public void SetRoleMatchesGoldenVector()
    {
        var encoded = CborContractCodec.EncodeSetRole(1, KernelRole.Active);
        Assert.Equal(ReadVector("response-role-accepted.hex").Length > 0, encoded.Length > 0);
        Assert.Equal(Convert.FromHexString("a3000101010201"), encoded);
    }

    [Fact]
    public void DecodeRoleResponseMatchesAdaGoldenVector()
    {
        var response = CborContractCodec.DecodeKernelResponse(ReadVector("response-role-accepted.hex"));

        Assert.Equal(KernelResponseKind.Role, response.Kind);
        Assert.Equal(KernelResponseStatus.Ok, response.Status);
        Assert.Equal(1UL, response.Epoch);
        Assert.Equal(KernelRole.Active, response.Role);
        Assert.Null(response.Cursor);
        Assert.Equal(FixedBytes32.FromHex("52094d19b4d413c54be6f0d0e8169453f397a785fbcdf7de2f07c86dbe9f79b9"), response.StateDigest);
        Assert.Null(response.JumpFact);
    }

    [Fact]
    public void DecodeFsdJumpResponseMatchesAdaGoldenVector()
    {
        var response = CborContractCodec.DecodeKernelResponse(ReadVector("response-fsdjump-applied.hex"));

        Assert.Equal(KernelResponseKind.Apply, response.Kind);
        Assert.Equal(KernelResponseStatus.Ok, response.Status);
        Assert.Equal(1UL, response.Epoch);
        Assert.Equal(KernelRole.Active, response.Role);
        Assert.Equal(new ObservationCursor(2, 0), response.Cursor);
        Assert.Equal(FixedBytes32.FromHex("6a7b08d0f7bb2a7614719bf4644989015e1d66735a3fe2e42cdc7a5dd2dd1938"), response.StateDigest);
        Assert.NotNull(response.JumpFact);
        Assert.Equal("W. Grantler NX-42", response.JumpFact!.StarSystem);
        Assert.Equal(1_234_567_890_123_456_789UL, response.JumpFact.SystemAddress);
        Assert.Equal(new Decimal64(55359, -3), response.JumpFact.JumpDistance);
        Assert.Equal(new Decimal64(27123, -3), response.JumpFact.FuelLevel);
        Assert.Equal(SourceProvenance.LocalJournal, response.JumpFact.LocationProvenance);
        Assert.Equal(FreshnessState.Current, response.JumpFact.LocationFreshness);
    }
    private static void AssertGoldenRoundTrip(string name, ObservationEnvelope observation)
    {
        var expected = ReadVector(name);
        var encoded = CborContractCodec.Encode(observation);

        Assert.Equal(expected, encoded);
        Assert.Equal(observation, CborContractCodec.DecodeObservation(encoded));
    }

    private static byte[] ReadVector(string name)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "fixtures",
            "contracts",
            "v1",
            name);

        return Convert.FromHexString(File.ReadAllText(path).Trim());
    }
}
