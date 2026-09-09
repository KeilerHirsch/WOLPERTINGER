using System.Text;
using Wolpertinger.Edge.Runtime;

namespace Wolpertinger.Integration.Tests;

public sealed class FsdJumpVerticalSliceTests
{
    [Fact]
    public async Task RealFixtureProducesOneAuthoritativeJumpOutput()
    {
        var root = RepoRoot(); var data = TempData(); var kernel = Kernel(root);
        await using var runner = await VerticalSliceRunner.OpenAsync(data, kernel);
        foreach (var line in File.ReadLines(Path.Combine(root, "fixtures", "journal", "live-v4-fsdjump-session.jsonl")))
            await runner.ProcessJournalLineAsync(Encoding.UTF8.GetBytes(line));

        var output = Assert.Single(runner.Outputs);
        Assert.Equal("Jump complete: W. Grantler NX-42 - 55.359 ly, fuel 27.123 t.", output.Text);
        Assert.Equal(2UL, output.Cursor.EvidenceSequence);
        Assert.NotNull(runner.FinalStateDigest);
        Assert.Equal(runner.FinalStateDigest, output.StateDigest);
    }

    internal static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "WOLPERTINGER.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
    internal static string Kernel(string root)
    {
        var path = Path.Combine(root, "kernel", "bin", "wolpertinger_kernel_main.exe");
        return File.Exists(path) ? path : throw new FileNotFoundException("Build trusted kernel before integration tests.", path);
    }
    internal static string TempData() { var p=Path.Combine(Path.GetTempPath(),"wolpertinger-int",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(p); return p; }
}
