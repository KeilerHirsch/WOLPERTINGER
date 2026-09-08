namespace Wolpertinger.Edge.Evidence;

public readonly record struct EvidenceReference(
    ulong RawOrdinal,
    uint SegmentNumber,
    long ByteOffset,
    int FrameLength);
