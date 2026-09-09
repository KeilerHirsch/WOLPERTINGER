namespace Wolpertinger.Edge.Contracts;

public static class KernelProtocol
{
    public const int Version = 1;
    public const int SetRoleMessageKind = 1;
    public const int ApplyObservationMessageKind = 2;
    public const int MaximumPayloadBytes = 65_536;
}

public enum GalaxyRealm : byte { Unknown = 0, Live = 1, Legacy = 2, BetaOrPts = 3 }
public enum KernelRole : byte { Shadow = 0, Active = 1 }
public enum ObservationKind : byte { SessionBound = 1, FsdJump = 2 }
public enum SourceProvenance : byte
{
    Unknown = 0,
    LocalJournal = 1,
    LocalStatus = 2,
    FrontierApi = 3,
    Community = 4,
    UserEntered = 5,
}

public enum KernelResponseKind : byte { Role = 1, Apply = 2 }
public enum KernelResponseStatus : byte
{
    Ok = 0,
    Idempotent = 1,
    SequenceGap = 2,
    IntegrityFault = 3,
    IdentityConflict = 4,
    InvalidMessage = 5,
    StaleEpoch = 6,
}

public enum FreshnessState : byte
{
    Unknown = 0,
    Current = 1,
    Stale = 2,
    Conflicting = 3,
}

public sealed record KernelJumpFact(
    ObservationCursor Cursor,
    ulong SystemAddress,
    string StarSystem,
    GalacticPosition Position,
    Decimal64 JumpDistance,
    Decimal64 FuelUsed,
    Decimal64 FuelLevel,
    SourceProvenance LocationProvenance,
    FreshnessState LocationFreshness,
    SourceProvenance FuelProvenance,
    FreshnessState FuelFreshness);

public sealed record KernelResponse(
    KernelResponseKind Kind,
    KernelResponseStatus Status,
    ulong Epoch,
    KernelRole Role,
    ObservationCursor? Cursor,
    FixedBytes32 StateDigest,
    KernelJumpFact? JumpFact);
