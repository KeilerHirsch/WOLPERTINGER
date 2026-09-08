using Wolpertinger.Edge.Output;

namespace Wolpertinger.Edge.Projections;

public sealed class ProjectionRebuilder
{
    public async Task RebuildAsync(string path, IEnumerable<CopilotOutput> outputs, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path); ArgumentNullException.ThrowIfNull(outputs);
        var full = Path.GetFullPath(path);
        if (File.Exists(full)) File.Delete(full);
        await using var store = await ProjectionStore.OpenAsync(full, cancellationToken).ConfigureAwait(false);
        foreach (var output in outputs)
            await store.ApplyAsync(output, cancellationToken).ConfigureAwait(false);
    }
}
