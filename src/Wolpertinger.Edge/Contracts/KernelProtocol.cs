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
