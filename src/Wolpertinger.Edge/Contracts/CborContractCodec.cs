using System.Formats.Cbor;
using System.Text;

namespace Wolpertinger.Edge.Contracts;

public static class CborContractCodec
{
    private const int EnvelopeFieldCount = 13;

    public static byte[] Encode(ObservationEnvelope observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ValidateEnvelope(observation);

        var writer = new CborWriter(CborConformanceMode.Canonical);
        writer.WriteStartMap(EnvelopeFieldCount);
        WriteKey(writer, 0); writer.WriteInt32(KernelProtocol.ApplyObservationMessageKind);
        WriteKey(writer, 1); writer.WriteInt32(KernelProtocol.Version);
        WriteKey(writer, 2); WriteCursor(writer, observation.Cursor);
        WriteKey(writer, 3); writer.WriteByteString(observation.EvidenceDigest.ToArray());
        WriteKey(writer, 4); writer.WriteByteString(observation.SessionId.ToArray());
        WriteKey(writer, 5); WriteProfile(writer, observation.Profile);
        WriteKey(writer, 6); writer.WriteInt32((int)observation.Kind);
        WriteKey(writer, 7); WriteNullableInt64(writer, observation.SourceTimeUnixMs);
        WriteKey(writer, 8); writer.WriteInt64(observation.ObservedUnixMs);
        WriteKey(writer, 9); writer.WriteInt64(observation.CommitUnixMs);
        WriteKey(writer, 10); writer.WriteUInt64(observation.MessageCount);
        WriteKey(writer, 11); WritePayload(writer, observation);
        WriteKey(writer, 12); writer.WriteInt32((int)observation.Provenance);
        writer.WriteEndMap();
        return writer.Encode();
    }

    public static byte[] EncodeSetRole(ulong epoch, KernelRole role)
    {
        if (role is < KernelRole.Shadow or > KernelRole.Active)
        {
            throw new ArgumentOutOfRangeException(nameof(role));
        }

        var writer = new CborWriter(CborConformanceMode.Canonical);
        writer.WriteStartMap(3);
        WriteKey(writer, 0); writer.WriteInt32(KernelProtocol.SetRoleMessageKind);
        WriteKey(writer, 1); writer.WriteUInt64(epoch);
        WriteKey(writer, 2); writer.WriteInt32((int)role);
        writer.WriteEndMap();
        return writer.Encode();
    }

    public static KernelResponse DecodeKernelResponse(ReadOnlyMemory<byte> encoded)
    {
        if (encoded.Length > KernelProtocol.MaximumPayloadBytes)
        {
            throw Violation($"Kernel response exceeds maximum payload of {KernelProtocol.MaximumPayloadBytes} bytes.");
        }

        try
        {
            var reader = new CborReader(encoded, CborConformanceMode.Canonical);
            RequireLength(reader.ReadStartMap(), 7, "kernel response");
            RequireKey(reader, 0); var kind = ReadKernelResponseKind(reader);
            RequireKey(reader, 1); var status = ReadKernelResponseStatus(reader);
            RequireKey(reader, 2); var epoch = reader.ReadUInt64();
            RequireKey(reader, 3); var cursor = ReadNullableCursor(reader);
            RequireKey(reader, 4); var stateDigest = ReadFixed32(reader);
            RequireKey(reader, 5); var jumpFact = ReadNullableJumpFact(reader);
            RequireKey(reader, 6); var role = ReadKernelRole(reader);
            reader.ReadEndMap();
            if (reader.BytesRemaining != 0)
            {
                throw Violation("Trailing bytes after kernel response.");
            }

            return new KernelResponse(kind, status, epoch, role, cursor, stateDigest, jumpFact);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex) when (ex is CborContentException or InvalidOperationException or ArgumentException or OverflowException)
        {
            throw new InvalidDataException("Invalid trusted CBOR kernel response.", ex);
        }
    }
    public static ObservationEnvelope DecodeObservation(ReadOnlyMemory<byte> encoded)
    {
        if (encoded.Length > KernelProtocol.MaximumPayloadBytes)
        {
            throw Violation($"Trusted CBOR observation exceeds maximum payload of {KernelProtocol.MaximumPayloadBytes} bytes.");
        }
        try
        {
            var reader = new CborReader(encoded, CborConformanceMode.Canonical);
            RequireLength(reader.ReadStartMap(), EnvelopeFieldCount, "observation envelope");

            RequireKey(reader, 0);
            RequireValue(reader.ReadInt32(), KernelProtocol.ApplyObservationMessageKind, "host message kind");
            RequireKey(reader, 1);
            RequireValue(reader.ReadInt32(), KernelProtocol.Version, "protocol version");
            RequireKey(reader, 2);
            var cursor = ReadCursor(reader);
            RequireKey(reader, 3);
            var evidenceDigest = ReadFixed32(reader);
            RequireKey(reader, 4);
            var sessionId = ReadFixed16(reader);
            RequireKey(reader, 5);
            var profile = ReadProfile(reader);
            RequireKey(reader, 6);
            var kind = ReadObservationKind(reader);
            RequireKey(reader, 7);
            var sourceTime = ReadNullableInt64(reader);
            RequireKey(reader, 8);
            var observed = reader.ReadInt64();
            RequireKey(reader, 9);
            var commit = reader.ReadInt64();
            RequireKey(reader, 10);
            var messageCount = ReadMessageCount(reader);
            RequireKey(reader, 11);
            var payload = ReadPayload(reader, kind);
            RequireKey(reader, 12);
            var provenance = ReadProvenance(reader);
            reader.ReadEndMap();

            if (reader.BytesRemaining != 0)
            {
                throw Violation("Trailing bytes after observation envelope.");
            }

            var observation = new ObservationEnvelope(
                cursor,
                evidenceDigest,
                sessionId,
                profile,
                kind,
                sourceTime,
                observed,
                commit,
                messageCount,
                payload,
                provenance);
            ValidateEnvelope(observation);
            return observation;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex) when (ex is CborContentException or InvalidOperationException or ArgumentException or OverflowException)
        {
            throw new InvalidDataException("Invalid trusted CBOR observation.", ex);
        }
    }

    private static void WriteCursor(CborWriter writer, ObservationCursor cursor)
    {
        writer.WriteStartArray(2);
        writer.WriteUInt64(cursor.EvidenceSequence);
        writer.WriteUInt64(cursor.MessageOrdinal);
        writer.WriteEndArray();
    }

    private static void WriteProfile(CborWriter writer, ProfileKey profile)
    {
        writer.WriteStartArray(3);
        writer.WriteTextString(profile.Fid);
        writer.WriteInt32((int)profile.Realm);
        writer.WriteUInt64(profile.SaveEpoch);
        writer.WriteEndArray();
    }

    private static void WriteNullableInt64(CborWriter writer, long? value)
    {
        if (value.HasValue)
        {
            writer.WriteInt64(value.Value);
        }
        else
        {
            writer.WriteNull();
        }
    }

    private static void WritePayload(CborWriter writer, ObservationEnvelope observation)
    {
        switch (observation.Payload)
        {
            case SessionBoundPayload when observation.Kind == ObservationKind.SessionBound:
                writer.WriteNull();
                break;
            case FsdJumpPayload jump when observation.Kind == ObservationKind.FsdJump:
                WriteFsdJump(writer, jump);
                break;
            default:
                throw new InvalidDataException("Observation kind and payload disagree.");
        }
    }

    private static void WriteFsdJump(CborWriter writer, FsdJumpPayload jump)
    {
        writer.WriteStartMap(6);
        WriteKey(writer, 0); writer.WriteTextString(jump.StarSystem);
        WriteKey(writer, 1); writer.WriteUInt64(jump.SystemAddress);
        WriteKey(writer, 2);
        writer.WriteStartArray(3);
        WriteDecimal64(writer, jump.Position.X);
        WriteDecimal64(writer, jump.Position.Y);
        WriteDecimal64(writer, jump.Position.Z);
        writer.WriteEndArray();
        WriteKey(writer, 3); WriteDecimal64(writer, jump.JumpDistance);
        WriteKey(writer, 4); WriteDecimal64(writer, jump.FuelUsed);
        WriteKey(writer, 5); WriteDecimal64(writer, jump.FuelLevel);
        writer.WriteEndMap();
    }

    private static void WriteDecimal64(CborWriter writer, Decimal64 value)
    {
        ValidateDecimal64(value);
        writer.WriteStartArray(2);
        writer.WriteInt64(value.Coefficient);
        writer.WriteInt64(value.Exponent10);
        writer.WriteEndArray();
    }

    private static void WriteKey(CborWriter writer, int key) => writer.WriteInt32(key);

    private static ObservationCursor ReadCursor(CborReader reader)
    {
        RequireLength(reader.ReadStartArray(), 2, "cursor");
        var sequence = reader.ReadUInt64();
        var ordinal = reader.ReadUInt64();
        if (ordinal > uint.MaxValue)
        {
            throw Violation("MessageOrdinal exceeds UInt32.");
        }

        reader.ReadEndArray();
        return new ObservationCursor(sequence, (uint)ordinal);
    }

    private static ObservationCursor? ReadNullableCursor(CborReader reader)
    {
        if (reader.PeekState() == CborReaderState.Null)
        {
            reader.ReadNull();
            return null;
        }

        return ReadCursor(reader);
    }

    private static KernelResponseKind ReadKernelResponseKind(CborReader reader)
    {
        var value = reader.ReadInt32();
        return value switch
        {
            1 => KernelResponseKind.Role,
            2 => KernelResponseKind.Apply,
            _ => throw Violation("Unknown kernel response kind."),
        };
    }

    private static KernelRole ReadKernelRole(CborReader reader)
    {
        var value = reader.ReadInt32();
        return value switch
        {
            0 => KernelRole.Shadow,
            1 => KernelRole.Active,
            _ => throw Violation("Unknown kernel role."),
        };
    }

    private static KernelResponseStatus ReadKernelResponseStatus(CborReader reader)
    {
        var value = reader.ReadInt32();
        return value switch
        {
            0 => KernelResponseStatus.Ok,
            1 => KernelResponseStatus.Idempotent,
            2 => KernelResponseStatus.SequenceGap,
            3 => KernelResponseStatus.IntegrityFault,
            4 => KernelResponseStatus.IdentityConflict,
            5 => KernelResponseStatus.InvalidMessage,
            6 => KernelResponseStatus.StaleEpoch,
            _ => throw Violation("Unknown kernel response status."),
        };
    }
    private static ProfileKey ReadProfile(CborReader reader)
    {
        RequireLength(reader.ReadStartArray(), 3, "profile key");
        var fid = reader.ReadTextString();
        RequireUtf8Length(fid, 1, 64, "FID");
        var realmValue = reader.ReadInt32();
        if (realmValue is < 0 or > 3)
        {
            throw Violation("Unknown galaxy realm.");
        }

        var saveEpoch = reader.ReadUInt64();
        reader.ReadEndArray();
        return new ProfileKey(fid, (GalaxyRealm)realmValue, saveEpoch);
    }

    private static FixedBytes32 ReadFixed32(CborReader reader)
    {
        var bytes = reader.ReadByteString();
        if (bytes.Length != 32)
        {
            throw Violation("Evidence digest must contain 32 bytes.");
        }

        return FixedBytes32.FromBytes(bytes);
    }

    private static FixedBytes16 ReadFixed16(CborReader reader)
    {
        var bytes = reader.ReadByteString();
        if (bytes.Length != 16)
        {
            throw Violation("SessionId must contain 16 bytes.");
        }

        return FixedBytes16.FromBytes(bytes);
    }

    private static ObservationKind ReadObservationKind(CborReader reader)
    {
        var value = reader.ReadInt32();
        return value switch
        {
            1 => ObservationKind.SessionBound,
            2 => ObservationKind.FsdJump,
            _ => throw Violation("Unknown observation kind."),
        };
    }

    private static long? ReadNullableInt64(CborReader reader)
    {
        if (reader.PeekState() == CborReaderState.Null)
        {
            reader.ReadNull();
            return null;
        }

        return reader.ReadInt64();
    }

    private static ushort ReadMessageCount(CborReader reader)
    {
        var value = reader.ReadUInt64();
        if (value is 0 or > ushort.MaxValue)
        {
            throw Violation("MessageCount must be in 1..65535.");
        }

        return (ushort)value;
    }

    private static SourceProvenance ReadProvenance(CborReader reader)
    {
        var value = reader.ReadInt32();
        if (value is < 0 or > 5)
        {
            throw Violation("Unknown source provenance.");
        }

        return (SourceProvenance)value;
    }

    private static ObservationPayload ReadPayload(CborReader reader, ObservationKind kind)
    {
        if (kind == ObservationKind.SessionBound)
        {
            if (reader.PeekState() != CborReaderState.Null)
            {
                throw Violation("SessionBound payload must be null.");
            }

            reader.ReadNull();
            return SessionBoundPayload.Instance;
        }

        return ReadFsdJump(reader);
    }

    private static FsdJumpPayload ReadFsdJump(CborReader reader)
    {
        RequireLength(reader.ReadStartMap(), 6, "FSDJump payload");
        RequireKey(reader, 0);
        var starSystem = reader.ReadTextString();
        RequireUtf8Length(starSystem, 1, 128, "StarSystem");
        RequireKey(reader, 1);
        var systemAddress = reader.ReadUInt64();
        RequireKey(reader, 2);
        RequireLength(reader.ReadStartArray(), 3, "StarPos");
        var x = ReadDecimal64(reader);
        var y = ReadDecimal64(reader);
        var z = ReadDecimal64(reader);
        reader.ReadEndArray();
        RequireKey(reader, 3);
        var jumpDistance = ReadDecimal64(reader);
        RequireKey(reader, 4);
        var fuelUsed = ReadDecimal64(reader);
        RequireKey(reader, 5);
        var fuelLevel = ReadDecimal64(reader);
        reader.ReadEndMap();

        return new FsdJumpPayload(
            starSystem,
            systemAddress,
            new GalacticPosition(x, y, z),
            jumpDistance,
            fuelUsed,
            fuelLevel);
    }

    private static KernelJumpFact? ReadNullableJumpFact(CborReader reader)
    {
        if (reader.PeekState() == CborReaderState.Null)
        {
            reader.ReadNull();
            return null;
        }

        RequireLength(reader.ReadStartArray(), 11, "JumpFact");
        var cursor = ReadCursor(reader);
        var systemAddress = reader.ReadUInt64();
        var starSystem = reader.ReadTextString();
        RequireUtf8Length(starSystem, 1, 128, "JumpFact StarSystem");
        RequireLength(reader.ReadStartArray(), 3, "JumpFact position");
        var x = ReadDecimal64(reader);
        var y = ReadDecimal64(reader);
        var z = ReadDecimal64(reader);
        reader.ReadEndArray();
        var jumpDistance = ReadDecimal64(reader);
        var fuelUsed = ReadDecimal64(reader);
        var fuelLevel = ReadDecimal64(reader);
        var locationProvenance = ReadProvenance(reader);
        var locationFreshness = ReadFreshness(reader);
        var fuelProvenance = ReadProvenance(reader);
        var fuelFreshness = ReadFreshness(reader);
        reader.ReadEndArray();

        return new KernelJumpFact(
            cursor,
            systemAddress,
            starSystem,
            new GalacticPosition(x, y, z),
            jumpDistance,
            fuelUsed,
            fuelLevel,
            locationProvenance,
            locationFreshness,
            fuelProvenance,
            fuelFreshness);
    }

    private static FreshnessState ReadFreshness(CborReader reader)
    {
        var value = reader.ReadInt32();
        return value switch
        {
            0 => FreshnessState.Unknown,
            1 => FreshnessState.Current,
            2 => FreshnessState.Stale,
            3 => FreshnessState.Conflicting,
            _ => throw Violation("Unknown freshness state."),
        };
    }
    private static Decimal64 ReadDecimal64(CborReader reader)
    {
        RequireLength(reader.ReadStartArray(), 2, "Decimal64");
        var coefficient = reader.ReadInt64();
        var exponent = reader.ReadInt64();
        reader.ReadEndArray();
        if (exponent is < Decimal64.MinExponent10 or > Decimal64.MaxExponent10)
        {
            throw Violation("Decimal64 exponent is outside -18..18.");
        }

        var value = new Decimal64(coefficient, (sbyte)exponent);
        ValidateDecimal64(value);
        return value;
    }

    private static void ValidateEnvelope(ObservationEnvelope observation)
    {
        RequireUtf8Length(observation.Profile.Fid, 1, 64, "FID");
        if (observation.Profile.Realm is < GalaxyRealm.Unknown or > GalaxyRealm.BetaOrPts)
        {
            throw Violation("Unknown galaxy realm.");
        }

        if (observation.MessageCount == 0)
        {
            throw Violation("MessageCount must be non-zero.");
        }

        if (observation.Provenance is < SourceProvenance.Unknown or > SourceProvenance.UserEntered)
        {
            throw Violation("Unknown source provenance.");
        }

        switch (observation.Payload)
        {
            case SessionBoundPayload when observation.Kind == ObservationKind.SessionBound:
                break;
            case FsdJumpPayload jump when observation.Kind == ObservationKind.FsdJump:
                RequireUtf8Length(jump.StarSystem, 1, 128, "StarSystem");
                ValidateDecimal64(jump.Position.X);
                ValidateDecimal64(jump.Position.Y);
                ValidateDecimal64(jump.Position.Z);
                ValidateDecimal64(jump.JumpDistance);
                ValidateDecimal64(jump.FuelUsed);
                ValidateDecimal64(jump.FuelLevel);
                break;
            default:
                throw Violation("Observation kind and payload disagree.");
        }
    }

    private static void ValidateDecimal64(Decimal64 value)
    {
        if (value.Exponent10 is < Decimal64.MinExponent10 or > Decimal64.MaxExponent10)
        {
            throw Violation("Decimal64 exponent is outside -18..18.");
        }

        if (value.Coefficient == 0)
        {
            if (value.Exponent10 != 0)
            {
                throw Violation("Zero Decimal64 must use exponent 0.");
            }

            return;
        }

        if (value.Coefficient % 10 == 0)
        {
            throw Violation("Decimal64 coefficient is not canonical.");
        }
    }

    private static void RequireUtf8Length(string value, int min, int max, string name)
    {
        ArgumentNullException.ThrowIfNull(value);
        var length = Encoding.UTF8.GetByteCount(value);
        if (length < min || length > max)
        {
            throw Violation($"{name} UTF-8 length must be in {min}..{max}.");
        }
    }

    private static void RequireLength(int? actual, int expected, string name)
    {
        if (actual != expected)
        {
            throw Violation($"{name} must contain exactly {expected} items.");
        }
    }

    private static void RequireKey(CborReader reader, int expected)
    {
        var actual = reader.ReadInt32();
        if (actual != expected)
        {
            throw Violation($"Expected field key {expected}, got {actual}.");
        }
    }

    private static void RequireValue(int actual, int expected, string name)
    {
        if (actual != expected)
        {
            throw Violation($"Unexpected {name}: {actual}.");
        }
    }

    private static InvalidDataException Violation(string message) => new(message);
}
