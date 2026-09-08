using Microsoft.Data.Sqlite;
using Wolpertinger.Edge.Output;

namespace Wolpertinger.Edge.Projections;

public sealed class ProjectionStore : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private ProjectionStore(SqliteConnection connection) => _connection = connection;

    public static async Task<ProjectionStore> OpenAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var connection = new SqliteConnection($"Data Source={full};Pooling=False");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        var store = new ProjectionStore(connection);
        await store.EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);
        return store;
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            create table if not exists projection_meta(schema_version integer not null);
            insert into projection_meta(schema_version) select 1 where not exists(select 1 from projection_meta);
            create table if not exists latest_jump(
              profile_fid text not null, profile_realm integer not null, profile_save_epoch text not null,
              evidence_sequence text not null, message_ordinal integer not null, system_address text not null,
              star_system text not null, jump_distance text not null, fuel_used text not null, fuel_level text not null,
              state_digest blob not null, evidence_digest blob not null, reason_code text not null, output_text text not null,
              primary key(profile_fid, profile_realm, profile_save_epoch));
            create table if not exists copilot_output(
              evidence_sequence text not null, message_ordinal integer not null,
              profile_fid text not null, profile_realm integer not null, profile_save_epoch text not null,
              state_digest blob not null, evidence_digest blob not null, reason_code text not null, output_text text not null,
              primary key(evidence_sequence, message_ordinal));
            """;
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task ApplyAsync(CopilotOutput output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(output);
        await using var tx = await _connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await InsertOutputAsync(output, tx, cancellationToken).ConfigureAwait(false);
        await UpsertLatestAsync(output, tx, cancellationToken).ConfigureAwait(false);
        await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task InsertOutputAsync(CopilotOutput o, System.Data.Common.DbTransaction tx, CancellationToken ct)
    {
        await using var cmd = _connection.CreateCommand(); cmd.Transaction = (SqliteTransaction)tx;
        cmd.CommandText = "insert or ignore into copilot_output values($seq,$ord,$fid,$realm,$epoch,$sd,$ed,$reason,$text)";
        AddCommon(cmd, o); await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private async Task UpsertLatestAsync(CopilotOutput o, System.Data.Common.DbTransaction tx, CancellationToken ct)
    {
        await using var cmd = _connection.CreateCommand(); cmd.Transaction = (SqliteTransaction)tx;
        cmd.CommandText = """
            insert into latest_jump values($fid,$realm,$epoch,$seq,$ord,$addr,$star,$jump,$used,$level,$sd,$ed,$reason,$text)
            on conflict(profile_fid,profile_realm,profile_save_epoch) do update set
              evidence_sequence=excluded.evidence_sequence,message_ordinal=excluded.message_ordinal,
              system_address=excluded.system_address,star_system=excluded.star_system,jump_distance=excluded.jump_distance,
              fuel_used=excluded.fuel_used,fuel_level=excluded.fuel_level,state_digest=excluded.state_digest,
              evidence_digest=excluded.evidence_digest,reason_code=excluded.reason_code,output_text=excluded.output_text;
            """;
        AddCommon(cmd, o);
        cmd.Parameters.AddWithValue("$addr", o.SystemAddress.ToString(System.Globalization.CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$star", o.StarSystem); cmd.Parameters.AddWithValue("$jump", o.JumpDistance.ToString());
        cmd.Parameters.AddWithValue("$used", o.FuelUsed.ToString()); cmd.Parameters.AddWithValue("$level", o.FuelLevel.ToString());
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static void AddCommon(SqliteCommand cmd, CopilotOutput o)
    {
        cmd.Parameters.AddWithValue("$seq", o.Cursor.EvidenceSequence.ToString(System.Globalization.CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$ord", checked((long)o.Cursor.MessageOrdinal)); cmd.Parameters.AddWithValue("$fid", o.Profile.Fid);
        cmd.Parameters.AddWithValue("$realm", (int)o.Profile.Realm); cmd.Parameters.AddWithValue("$epoch", o.Profile.SaveEpoch.ToString(System.Globalization.CultureInfo.InvariantCulture));
        cmd.Parameters.Add("$sd", SqliteType.Blob).Value = o.StateDigest.ToArray(); cmd.Parameters.Add("$ed", SqliteType.Blob).Value = o.EvidenceDigest.ToArray();
        cmd.Parameters.AddWithValue("$reason", o.ReasonCode); cmd.Parameters.AddWithValue("$text", o.Text);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync().ConfigureAwait(false);
}
