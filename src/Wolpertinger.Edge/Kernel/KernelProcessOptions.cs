namespace Wolpertinger.Edge.Kernel;

public sealed record KernelProcessOptions
{
    public KernelProcessOptions(string executablePath, TimeSpan requestTimeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        if (requestTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(requestTimeout));
        }

        ExecutablePath = executablePath;
        RequestTimeout = requestTimeout;
    }

    public string ExecutablePath { get; }
    public TimeSpan RequestTimeout { get; }
}
