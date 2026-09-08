using Wolpertinger.Edge.Contracts;

namespace Wolpertinger.Edge.Persistence;

public interface IObservationReplaySource
{
    IAsyncEnumerable<ObservationEnvelope> ReadObservationsAsync(
        ObservationCursor? after,
        CancellationToken cancellationToken = default);
}
