namespace Wolpertinger.Edge.Diagnostics;

public sealed record DiagnosticEvent(
    string Code,
    ulong RawOrdinal,
    string Message);
