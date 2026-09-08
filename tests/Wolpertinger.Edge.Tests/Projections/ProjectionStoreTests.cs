using Microsoft.Data.Sqlite;
using Wolpertinger.Edge.Context;
using Wolpertinger.Edge.Output;
using Wolpertinger.Edge.Projections;
using Wolpertinger.Edge.Tests.TestSupport;

namespace Wolpertinger.Edge.Tests.Projections;

public sealed class ProjectionStoreTests
{
    [Fact]
    public async Task ApplyIsIdempotentAndPreservesProfileAndDigests()
    {
        var path = TempDb();
        var fact = TestFacts.Jump();
        var output = new CopilotOutputFormatter().Format(fact, new ContextDecisionEngine().Decide(fact));
        Assert.Equal("F100", output.Profile.Fid);

        await using (var store = await ProjectionStore.OpenAsync(path))
        {
            await store.ApplyAsync(output);
            await store.ApplyAsync(output);
        }

        await using var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync();
        Assert.Equal(1L, await ScalarAsync(connection, "select count(*) from copilot_output"));
        Assert.Equal(1L, await ScalarAsync(connection, "select count(*) from latest_jump"));
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "select profile_fid, system_address, state_digest, evidence_digest from latest_jump";
        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("F100", reader.GetString(0));
        Assert.Equal(fact.SystemAddress.ToString(System.Globalization.CultureInfo.InvariantCulture), reader.GetString(1));
        Assert.Equal(fact.StateDigest.ToArray(), (byte[])reader[2]);
        Assert.Equal(fact.EvidenceDigest.ToArray(), (byte[])reader[3]);
    }

    private static async Task<long> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var cmd = connection.CreateCommand(); cmd.CommandText = sql;
        return Convert.ToInt64(await cmd.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string TempDb()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wolpertinger-projection-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir); return Path.Combine(dir, "projections.db");
    }
}
