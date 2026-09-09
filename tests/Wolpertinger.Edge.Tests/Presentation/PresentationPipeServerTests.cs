using System.IO.Pipes;
using Wolpertinger.Edge.Context;
using Wolpertinger.Edge.Presentation;
using Wolpertinger.Edge.Tests.TestSupport;
using Wolpertinger.Presentation.Contracts;

namespace Wolpertinger.Edge.Tests.Presentation;

public sealed class PresentationPipeServerTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendsCurrentUpdatesAndReacceptsIdleDisconnectOrProtocolViolation(bool writeByte)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var name = $"wolpertinger.presentation.test.{Guid.NewGuid():N}";
        var publisher = new PresentationStatePublisher();
        var server = new PresentationPipeServer(publisher, name);
        var running = server.RunAsync(timeout.Token);
        try
        {
            await using (var client = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous))
            {
                await client.ConnectAsync(timeout.Token);
                Assert.Equal(PresentationSnapshot.Empty, await PresentationFrameCodec.ReadAsync(client, timeout.Token));
                Assert.Equal(1, server.ConnectedClientCount);
                await publisher.PublishJumpAsync(TestFacts.Jump(), new ContextDecision(true, "JumpCompleted", OutputChannel.Display));
                Assert.Equal(publisher.Current, await PresentationFrameCodec.ReadAsync(client, timeout.Token));
                await WaitUntilAsync(() => server.LastSentRevision == 1, timeout.Token);
                await using var second = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
                using var secondTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => second.ConnectAsync(secondTimeout.Token));
                Assert.Equal(1, server.ConnectedClientCount);
                if (writeByte)
                {
                    await client.WriteAsync(new byte[] { 42 }, timeout.Token);
                    await client.FlushAsync(timeout.Token);
                    var buffer = new byte[1];
                    try { Assert.Equal(0, await client.ReadAsync(buffer, timeout.Token)); }
                    catch (IOException) { }
                    await WaitUntilAsync(() => server.ConnectedClientCount == 0, timeout.Token);
                }
            }
            await WaitUntilAsync(() => server.ConnectedClientCount == 0, timeout.Token);
            await using var reconnect = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
            await reconnect.ConnectAsync(timeout.Token);
            Assert.Equal(publisher.Current, await PresentationFrameCodec.ReadAsync(reconnect, timeout.Token));
            await publisher.PublishJumpAsync(TestFacts.Jump() with { StarSystem = "Second" }, new ContextDecision(true, "JumpCompleted", OutputChannel.Display));
            Assert.Equal(publisher.Current, await PresentationFrameCodec.ReadAsync(reconnect, timeout.Token));
        }
        finally
        {
            timeout.Cancel();
            try { await running; }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested) { }
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken ct)
    {
        while (!condition()) await Task.Delay(10, ct);
    }
}
