using Wolpertinger.Edge.Contracts;
using Wolpertinger.Edge.Kernel;

namespace Wolpertinger.Edge.Tests.Kernel;

public sealed class KernelProcessClientTests
{
    [Fact]
    public async Task RealKernelRoundTripsRoleSessionAndJump()
    {
        var root = FindRepositoryRoot();
        var executable = Path.Combine(
            root,
            "kernel",
            "bin",
            OperatingSystem.IsWindows()
                ? "wolpertinger_kernel_main.exe"
                : "wolpertinger_kernel_main");
        Assert.True(File.Exists(executable), $"Build the Ada kernel first: {executable}");

        await using var client = new KernelProcessClient(
            new KernelProcessOptions(executable, TimeSpan.FromSeconds(5)));
        await client.StartAsync();
        await client.SetRoleAsync(1, KernelRole.Active);

        var session = CborContractCodec.DecodeObservation(ReadVector("session-bound.hex"));
        var sessionResult = await client.ApplyAsync(session);
        Assert.Equal(1UL, sessionResult.Epoch);
        Assert.Equal(KernelRole.Active, sessionResult.Role);
        Assert.Equal(KernelRole.Active, sessionResult.Role);
        Assert.Equal(new ObservationCursor(1, 0), sessionResult.Cursor);
        Assert.Equal(KernelResponseStatus.Ok, sessionResult.Status);
        var jump = CborContractCodec.DecodeObservation(ReadVector("fsdjump.hex"));
        var jumpResult = await client.ApplyAsync(jump);
        Assert.Equal(1UL, jumpResult.Epoch);
        Assert.Equal(KernelRole.Active, jumpResult.Role);
        Assert.Equal(KernelRole.Active, jumpResult.Role);
        Assert.Equal(new ObservationCursor(2, 0), jumpResult.Cursor);
        Assert.Equal(KernelResponseStatus.Ok, jumpResult.Status);
        Assert.NotNull(jumpResult.JumpFact);

        await client.StopAsync();
    }

    private static byte[] ReadVector(string name)
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "fixtures",
            "contracts",
            "v1",
            name);
        return Convert.FromHexString(File.ReadAllText(path).Trim());
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WOLPERTINGER.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
