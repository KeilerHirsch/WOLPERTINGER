using System.Text;

namespace Wolpertinger.Edge.Contracts;

internal static class BoundedNameUtf8
{
    private static readonly UTF8Encoding StrictEncoding = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    internal static bool TryGetByteLength(string? value, out int byteLength)
    {
        byteLength = 0;
        if (value is null)
            return false;

        try
        {
            byteLength = StrictEncoding.GetByteCount(value);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }
}

public readonly record struct CommanderName
{
    private readonly string? _value;

    private CommanderName(string value, int utf8ByteLength)
    {
        _value = value;
        Utf8ByteLength = utf8ByteLength;
    }

    public const int MaximumUtf8Bytes = 128;

    public string Value => _value ?? throw new InvalidOperationException("CommanderName is uninitialized.");

    public int Utf8ByteLength { get; }

    internal bool IsInitialized => _value is not null && Utf8ByteLength is >= 1 and <= MaximumUtf8Bytes;

    public static bool TryCreate(string? value, out CommanderName name)
    {
        name = default;
        if (!BoundedNameUtf8.TryGetByteLength(value, out var byteLength)
            || byteLength is < 1 or > MaximumUtf8Bytes)
        {
            return false;
        }

        name = new CommanderName(value!, byteLength);
        return true;
    }

    public override string ToString() => "[CommanderName]";
}

public readonly record struct VesselName
{
    private readonly string? _value;

    private VesselName(string value, int utf8ByteLength)
    {
        _value = value;
        Utf8ByteLength = utf8ByteLength;
    }

    public const int MaximumUtf8Bytes = 128;

    public string Value => _value ?? throw new InvalidOperationException("VesselName is uninitialized.");

    public int Utf8ByteLength { get; }

    internal bool IsInitialized => _value is not null && Utf8ByteLength is >= 1 and <= MaximumUtf8Bytes;

    public static bool TryCreate(string? value, out VesselName name)
    {
        name = default;
        if (!BoundedNameUtf8.TryGetByteLength(value, out var byteLength)
            || byteLength is < 1 or > MaximumUtf8Bytes)
        {
            return false;
        }

        name = new VesselName(value!, byteLength);
        return true;
    }

    public override string ToString() => "[VesselName]";
}
