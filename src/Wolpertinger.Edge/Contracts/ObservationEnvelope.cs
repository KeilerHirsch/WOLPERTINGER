namespace Wolpertinger.Edge.Contracts;

public readonly record struct FixedBytes16
{
    private FixedBytes16(string hex) => Hex = hex;

    public string Hex { get; }

    public static FixedBytes16 FromHex(string hex)
        => new(NormalizeHex(hex, 16));

    public static FixedBytes16 FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 16)
        {
            throw new ArgumentException("Expected exactly 16 bytes.", nameof(bytes));
        }

        return new FixedBytes16(Convert.ToHexString(bytes));
    }

    public byte[] ToArray() => Convert.FromHexString(Hex);

    private static string NormalizeHex(string hex, int bytes)
    {
        ArgumentNullException.ThrowIfNull(hex);
        if (hex.Length != bytes * 2)
        {
            throw new FormatException($"Expected {bytes * 2} hexadecimal characters.");
        }

        try
        {
            _ = Convert.FromHexString(hex);
        }
        catch (FormatException ex)
        {
            throw new FormatException("Invalid hexadecimal byte string.", ex);
        }

        return hex.ToUpperInvariant();
    }
}

public readonly record struct FixedBytes32
{
    private FixedBytes32(string hex) => Hex = hex;

    public string Hex { get; }

    public static FixedBytes32 FromHex(string hex)
        => new(NormalizeHex(hex, 32));

    public static FixedBytes32 FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 32)
        {
            throw new ArgumentException("Expected exactly 32 bytes.", nameof(bytes));
        }

        return new FixedBytes32(Convert.ToHexString(bytes));
    }

    public byte[] ToArray() => Convert.FromHexString(Hex);

    private static string NormalizeHex(string hex, int bytes)
    {
        ArgumentNullException.ThrowIfNull(hex);
        if (hex.Length != bytes * 2)
        {
            throw new FormatException($"Expected {bytes * 2} hexadecimal characters.");
        }

        try
        {
            _ = Convert.FromHexString(hex);
        }
        catch (FormatException ex)
        {
            throw new FormatException("Invalid hexadecimal byte string.", ex);
        }

        return hex.ToUpperInvariant();
    }
}

public readonly record struct ObservationCursor(ulong EvidenceSequence, uint MessageOrdinal);
public sealed record ProfileKey(string Fid, GalaxyRealm Realm, ulong SaveEpoch);

public abstract record ObservationPayload;

public sealed record SessionBoundPayload : ObservationPayload
{
    private SessionBoundPayload() { }
    public static SessionBoundPayload Instance { get; } = new();
}

public sealed record GalacticPosition(Decimal64 X, Decimal64 Y, Decimal64 Z);

public sealed record FsdJumpPayload(
    string StarSystem,
    ulong SystemAddress,
    GalacticPosition Position,
    Decimal64 JumpDistance,
    Decimal64 FuelUsed,
    Decimal64 FuelLevel) : ObservationPayload;

public sealed record ObservationEnvelope(
    ObservationCursor Cursor,
    FixedBytes32 EvidenceDigest,
    FixedBytes16 SessionId,
    ProfileKey Profile,
    ObservationKind Kind,
    long? SourceTimeUnixMs,
    long ObservedUnixMs,
    long CommitUnixMs,
    ushort MessageCount,
    ObservationPayload Payload,
    SourceProvenance Provenance)
{
    public int ProtocolVersion => KernelProtocol.Version;
}
