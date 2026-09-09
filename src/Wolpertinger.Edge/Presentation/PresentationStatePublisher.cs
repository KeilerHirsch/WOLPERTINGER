using System.Threading.Channels;
using Wolpertinger.Edge.Context;
using Wolpertinger.Edge.Facts;
using Wolpertinger.Presentation.Contracts;

namespace Wolpertinger.Edge.Presentation;

public sealed class PresentationStatePublisher : IPresentationPublisher
{
    private readonly object _gate = new();
    private readonly Channel<PresentationSnapshot> _updates = Channel.CreateBounded<PresentationSnapshot>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });
    private PresentationSnapshot _current = PresentationSnapshot.Empty;

    public PresentationSnapshot Current => Volatile.Read(ref _current);

    public ValueTask PublishJumpAsync(JumpFact fact, ContextDecision decision, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Serialize revision assignment and enqueueing so concurrent writers cannot publish out of order.
        // TryWrite never waits for the presentation consumer; intermediate redraws are disposable.
        lock (_gate)
        {
            var snapshot = JumpPresentationProjector.Project(fact, decision, checked(_current.Revision + 1));
            Volatile.Write(ref _current, snapshot);
            _updates.Writer.TryWrite(snapshot);
        }
        return ValueTask.CompletedTask;
    }

    public IAsyncEnumerable<PresentationSnapshot> ReadUpdatesAsync(CancellationToken cancellationToken = default)
        => _updates.Reader.ReadAllAsync(cancellationToken);
}
