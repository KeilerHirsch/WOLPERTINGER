using Microsoft.Data.Sqlite;
using Wolpertinger.Edge.Context;
using Wolpertinger.Edge.Output;
using Wolpertinger.Edge.Projections;
using Wolpertinger.Edge.Tests.TestSupport;

namespace Wolpertinger.Edge.Tests.Projections;

public sealed class ProjectionRebuilderTests
{
    [Fact]
    public async Task DeleteAndRebuildRecreatesSameLogicalRows()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wolpertinger-rebuild-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "projections.db");
        var fact = TestFacts.Jump();
        var output = new CopilotOutputFormatter().Format(fact, new ContextDecisionEngine().Decide(fact));

        await using (var store = await ProjectionStore.OpenAsync(path))
            await store.ApplyAsync(output);
        var before = await SnapshotAsync(path);
        File.Delete(path);

        await new ProjectionRebuilder().RebuildAsync(path, new[] { output });
        var after = await SnapshotAsync(path);
        Assert.Equal(before, after);
    }

    private static async Task<string> SnapshotAsync(string path)
    {
        await using var c = new SqliteConnection($"Data Source={path};Pooling=False"); await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = "select profile_fid || '|' || star_system || '|' || hex(state_digest) || '|' || hex(evidence_digest) from latest_jump";
        return Convert.ToString(await cmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture)!;
    }
}
