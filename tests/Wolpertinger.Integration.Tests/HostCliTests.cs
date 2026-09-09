using System.Diagnostics;

namespace Wolpertinger.Integration.Tests;

public sealed class HostCliTests
{
    [Fact]
    public async Task IngestAndReplayCliEmitSameJumpAndDigest()
    {
        var root = FsdJumpVerticalSliceTests.RepoRoot(); var data = FsdJumpVerticalSliceTests.TempData();
        var kernel = FsdJumpVerticalSliceTests.Kernel(root); var journal = Path.Combine(root,"fixtures","journal","live-v4-fsdjump-session.jsonl");
        var ingest = await RunHost(root, $"ingest --journal \"{journal}\" --data \"{data}\" --kernel \"{kernel}\"");
        Assert.Equal(0, ingest.ExitCode); Assert.Contains("Jump complete: W. Grantler NX-42", ingest.StdOut);
        Assert.Contains("StateDigest:", ingest.StdErr);
        var replay = await RunHost(root, $"replay --data \"{data}\" --kernel \"{kernel}\"");
        Assert.Equal(0, replay.ExitCode); Assert.Equal(ingest.StdOut.Trim(), replay.StdOut.Trim());
        Assert.Equal(DigestLine(ingest.StdErr), DigestLine(replay.StdErr));
    }

    private static string DigestLine(string text) => text.Split('\n').Single(x => x.Contains("StateDigest:", StringComparison.Ordinal)).Trim();
    private static async Task<(int ExitCode,string StdOut,string StdErr)> RunHost(string root,string args)
    {
        var psi=new ProcessStartInfo("dotnet",$"run --project \"{Path.Combine(root,"src","Wolpertinger.Host")}\" -c Release --no-build -- {args}")
        {UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true,WorkingDirectory=root};
        using var p=Process.Start(psi)!; var o=p.StandardOutput.ReadToEndAsync(); var e=p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync(); return(p.ExitCode,await o,await e);
    }
}
