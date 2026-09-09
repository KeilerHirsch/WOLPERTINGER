using System.IO.Pipes;
using Wolpertinger.Presentation.Contracts;
using Wolpertinger.Presentation.Transport;
using Wolpertinger.Presentation.Tests.Contracts;

namespace Wolpertinger.Presentation.Tests.Transport;

public sealed class PresentationPipeClientTests
{
    [Fact]
    public async Task RealPublisherServerClientReconnectStartsWithLatestWithoutPublication()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var name = $"wolpertinger.presentation.test.{Guid.NewGuid():N}";
        var publisher = new Wolpertinger.Edge.Presentation.PresentationStatePublisher();
        var server = new Wolpertinger.Edge.Presentation.PresentationPipeServer(publisher, name);
        var running = server.RunAsync(timeout.Token);
        var client = new PresentationPipeClient(name);
        try
        {
            await using (var reader = client.ReadSnapshotsAsync(timeout.Token).GetAsyncEnumerator())
            {
                Assert.True(await reader.MoveNextAsync());
                Assert.Equal(PresentationSnapshot.Empty, reader.Current);
                var fact = new Wolpertinger.Edge.Facts.JumpFact(
                    new(2, 0), new("F100", Wolpertinger.Edge.Contracts.GalaxyRealm.Live, 7), new(4, 0, 400, 100),
                    Wolpertinger.Edge.Contracts.FixedBytes32.FromHex(new string('a', 64)),
                    Wolpertinger.Edge.Contracts.FixedBytes32.FromHex(new string('b', 64)),
                    42, "Sol", new(new(0, 0), new(0, 0), new(0, 0)), new(1, 0), new(1, 0), new(12, 0),
                    Wolpertinger.Edge.Contracts.SourceProvenance.LocalJournal, Wolpertinger.Edge.Contracts.FreshnessState.Current,
                    Wolpertinger.Edge.Contracts.SourceProvenance.LocalJournal, Wolpertinger.Edge.Contracts.FreshnessState.Current);
                await publisher.PublishJumpAsync(fact, new(true, "JumpCompleted", Wolpertinger.Edge.Context.OutputChannel.Display));
                Assert.True(await reader.MoveNextAsync());
                Assert.Equal(publisher.Current, reader.Current);
            }
            await using var reconnect = client.ReadSnapshotsAsync(timeout.Token).GetAsyncEnumerator();
            Assert.True(await reconnect.MoveNextAsync());
            Assert.Equal(publisher.Current, reconnect.Current);
            Assert.Equal(1UL, reconnect.Current.Revision);
        }
        finally
        {
            timeout.Cancel();
            try { await running; }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested) { }
        }
    }

    [Fact]
    public async Task PartialPrefixIsNotTreatedAsCleanDisconnect()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var name = $"wolpertinger.presentation.test.{Guid.NewGuid():N}";
        await using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await using var reader = new PresentationPipeClient(name).ReadSnapshotsAsync(timeout.Token).GetAsyncEnumerator();
        var pending = reader.MoveNextAsync().AsTask();
        await server.WaitForConnectionAsync(timeout.Token);
        await server.WriteAsync(new byte[] { 0 }, timeout.Token);
        await server.DisposeAsync();
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => pending);
        Assert.Equal("Truncated presentation frame.", error.Message);
    }
    [Fact]
    public async Task ReadsFullSnapshotsNeverWritesAndEndsOnEof()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var name = $"wolpertinger.presentation.test.{Guid.NewGuid():N}";
        await using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await using var reader = new PresentationPipeClient(name).ReadSnapshotsAsync(timeout.Token).GetAsyncEnumerator();
        var first = reader.MoveNextAsync().AsTask();
        await server.WaitForConnectionAsync(timeout.Token);
        var incoming = server.ReadAsync(new byte[1], timeout.Token).AsTask();
        await PresentationFrameCodec.WriteAsync(server, PresentationSnapshot.Empty, timeout.Token);
        Assert.True(await first);
        Assert.Equal(PresentationSnapshot.Empty, reader.Current);
        var next = reader.MoveNextAsync().AsTask();
        await PresentationFrameCodec.WriteAsync(server, TestSnapshots.Jump(1), timeout.Token);
        Assert.True(await next);
        Assert.Equal(TestSnapshots.Jump(1), reader.Current);
        Assert.False(incoming.IsCompleted);
        await server.DisposeAsync();
        Assert.False(await reader.MoveNextAsync());
        try { await incoming; }
        catch (IOException) { }
        catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task CancellationStopsIdleRead()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var session = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        var name = $"wolpertinger.presentation.test.{Guid.NewGuid():N}";
        await using var server = new NamedPipeServerStream(name, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await using var reader = new PresentationPipeClient(name).ReadSnapshotsAsync(session.Token).GetAsyncEnumerator();
        var pending = reader.MoveNextAsync().AsTask();
        await server.WaitForConnectionAsync(timeout.Token);
        session.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }
}
