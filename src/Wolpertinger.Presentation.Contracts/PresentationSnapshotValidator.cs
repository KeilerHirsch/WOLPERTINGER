namespace Wolpertinger.Presentation.Contracts;

public static class PresentationSnapshotValidator
{
    public static void Validate(PresentationSnapshot snapshot)
    {
        if (snapshot.ProtocolVersion != PresentationProtocol.Version)
            throw new InvalidDataException("Unsupported presentation protocol version.");

        if (snapshot.Jump is not { } jump)
            return;

        if (snapshot.Revision == 0)
            throw new InvalidDataException("A jump snapshot requires a nonzero revision.");

        if (jump.Profile is null)
            throw new InvalidDataException("A jump requires a profile.");

        RequireText(jump.Profile.Fid, nameof(PresentationProfile.Fid));
        RequireText(jump.StarSystem, nameof(jump.StarSystem));
        RequireText(jump.ReasonCode, nameof(jump.ReasonCode));
        RequireDigest(jump.EvidenceDigestHex, nameof(jump.EvidenceDigestHex));
        RequireDigest(jump.StateDigestHex, nameof(jump.StateDigestHex));

        if (jump.EvidenceReference.ByteOffset < 0 || jump.EvidenceReference.FrameLength < 0)
            throw new InvalidDataException("Evidence offset and frame length must be nonnegative.");

        RequireText(jump.PositionX, nameof(jump.PositionX));
        RequireText(jump.PositionY, nameof(jump.PositionY));
        RequireText(jump.PositionZ, nameof(jump.PositionZ));
        RequireText(jump.JumpDistance, nameof(jump.JumpDistance));
        RequireText(jump.FuelUsed, nameof(jump.FuelUsed));
        RequireText(jump.FuelLevel, nameof(jump.FuelLevel));
    }

    private static void RequireText(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"{field} must not be blank.");
    }

    private static void RequireDigest(string? value, string field)
    {
        if (value is null || value.Length != 64 || !value.All(char.IsAsciiHexDigit))
            throw new InvalidDataException($"{field} must contain exactly 64 hexadecimal characters.");
    }
}
