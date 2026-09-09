using System.IO.Pipes;
using Wolpertinger.Presentation.Contracts;

namespace Wolpertinger.Edge.Presentation;

public sealed class PresentationPipeServer(IPresentationPublisher publisher, string pipeName = PresentationProtocol.DefaultPipeName)
{
    private readonly Func<NamedPipeServerStream>? _pipeFactory;
    private int _connectedClientCount;
    private ulong _lastSentRevision;
    public int ConnectedClientCount => Volatile.Read(ref _connectedClientCount);
    public ulong LastSentRevision => Volatile.Read(ref _lastSentRevision);

    internal PresentationPipeServer(IPresentationPublisher publisher, string pipeName, Func<NamedPipeServerStream> pipeFactory)
        : this(publisher, pipeName)
    {
        _pipeFactory = pipeFactory ?? throw new ArgumentNullException(nameof(pipeFactory));
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            await using var pipe = _pipeFactory?.Invoke() ?? new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
                PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            try
            {
                await pipe.WaitForConnectionAsync(ct);
            }
            catch (IOException) when (!ct.IsCancellationRequested)
            {
                continue;
            }
            using var session = CancellationTokenSource.CreateLinkedTokenSource(ct);
            Volatile.Write(ref _connectedClientCount, 1);
            var monitor = MonitorClientDisconnectAsync(pipe, session);
            try
            {
                var current = publisher.Current;
                await PresentationFrameCodec.WriteAsync(pipe, current, session.Token);
                Volatile.Write(ref _lastSentRevision, current.Revision);
                var sentRevision = current.Revision;
                await foreach (var snapshot in publisher.ReadUpdatesAsync(session.Token))
                {
                    if (snapshot.Revision <= sentRevision) continue;
                    await PresentationFrameCodec.WriteAsync(pipe, snapshot, session.Token);
                    sentRevision = snapshot.Revision;
                    Volatile.Write(ref _lastSentRevision, sentRevision);
                }
            }
            catch (OperationCanceledException) when (session.IsCancellationRequested) { }
            catch (IOException) { /* A broken client must not stop the accept loop. */ }
            finally
            {
                session.Cancel();
                await monitor;
                Volatile.Write(ref _connectedClientCount, 0);
            }
        }
    }

    private static async Task MonitorClientDisconnectAsync(NamedPipeServerStream pipe, CancellationTokenSource session)
    {
        try
        {
            // EOF is a disconnect; any byte is a protocol violation. Neither is a command.
            _ = await pipe.ReadAsync(new byte[1], session.Token);
        }
        catch (OperationCanceledException) when (session.IsCancellationRequested) { }
        catch (IOException) { }
        finally { session.Cancel(); }
    }
}
