namespace Wolpertinger.Edge.Persistence;

public enum NormalizationDisposition : byte
{
    Ignored = 0,
    Dispatchable = 1,
    Rejected = 2,
}
