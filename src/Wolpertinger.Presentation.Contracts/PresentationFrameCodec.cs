using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wolpertinger.Presentation.Contracts;

public static class PresentationFrameCodec
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async ValueTask WriteAsync(Stream stream, PresentationSnapshot snapshot, CancellationToken ct = default)
    {
        PresentationSnapshotValidator.Validate(snapshot);
        var payload = JsonSerializer.SerializeToUtf8Bytes(snapshot, Options);
        if (payload.Length > PresentationProtocol.MaximumFrameBytes)
            throw new InvalidDataException("Presentation frame exceeds maximum size.");
        var prefix = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(prefix, payload.Length);
        await stream.WriteAsync(prefix, ct);
        await stream.WriteAsync(payload, ct);
        await stream.FlushAsync(ct);
    }

    public static async ValueTask<PresentationSnapshot> ReadAsync(Stream stream, CancellationToken ct = default)
    {
        var prefix = new byte[4];
        await FillAsync(stream, prefix, true, ct);
        var length = BinaryPrimitives.ReadInt32BigEndian(prefix);
        if (length <= 0 || length > PresentationProtocol.MaximumFrameBytes)
            throw new InvalidDataException("Invalid presentation frame length.");
        var payload = new byte[length];
        await FillAsync(stream, payload, false, ct);
        var snapshot = JsonSerializer.Deserialize<PresentationSnapshot>(payload, Options)
            ?? throw new InvalidDataException("Missing presentation snapshot.");
        PresentationSnapshotValidator.Validate(snapshot);
        return snapshot;
    }

    private static async ValueTask FillAsync(Stream stream, Memory<byte> buffer, bool prefix, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer[offset..], ct);
            if (count == 0)
            {
                if (prefix && offset == 0) throw new EndOfStreamException();
                throw new InvalidDataException("Truncated presentation frame.");
            }
            offset += count;
        }
    }
}
