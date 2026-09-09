namespace Wolpertinger.Presentation.Contracts;

public enum PresentationRealm : byte { Unknown = 0, Live = 1, Legacy = 2, BetaOrPts = 3 }
public enum PresentationProvenance : byte { Unknown = 0, LocalJournal = 1, LocalStatus = 2, FrontierApi = 3, Community = 4, UserEntered = 5 }
public enum PresentationFreshness : byte { Unknown = 0, Current = 1, Stale = 2, Conflicting = 3 }

public readonly record struct PresentationCursor(ulong EvidenceSequence, uint MessageOrdinal);
public sealed record PresentationProfile(string Fid, PresentationRealm Realm, ulong SaveEpoch);
public readonly record struct PresentationEvidenceReference(ulong RawOrdinal, uint SegmentNumber, long ByteOffset, int FrameLength);

public sealed record JumpPresentation(
    PresentationCursor Cursor,
    PresentationProfile Profile,
    PresentationEvidenceReference EvidenceReference,
    string EvidenceDigestHex,
    string StateDigestHex,
    ulong SystemAddress,
    string StarSystem,
    string PositionX,
    string PositionY,
    string PositionZ,
    string JumpDistance,
    string FuelUsed,
    string FuelLevel,
    PresentationProvenance LocationProvenance,
    PresentationFreshness LocationFreshness,
    PresentationProvenance FuelProvenance,
    PresentationFreshness FuelFreshness,
    string ReasonCode);

public sealed record PresentationSnapshot(int ProtocolVersion, ulong Revision, JumpPresentation? Jump)
{
    public static PresentationSnapshot Empty { get; } = new(PresentationProtocol.Version, 0, null);
}
