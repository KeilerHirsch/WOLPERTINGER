using Wolpertinger.Presentation.Contracts;
using Wolpertinger.Presentation.State;
using Wolpertinger.Presentation.Tests.Contracts;

namespace Wolpertinger.Presentation.Tests.State;

public sealed class PresentationStoreTests
{
    [Fact]
    public void HigherRevisionReplacesCurrentAndRaisesOneEvent()
    {
        var store = new PresentationStore();
        Assert.True(store.ApplySnapshot(TestSnapshots.Jump(2)));
        var changes = 0;
        store.StateChanged += (_, _) => changes++;

        Assert.True(store.ApplySnapshot(TestSnapshots.Jump(3)));

        Assert.Equal(PresentationConnectionState.Live, store.State.Connection);
        Assert.Equal(3UL, store.State.Snapshot!.Revision);
        Assert.Equal(1, changes);
    }

    [Fact]
    public void IdenticalSameRevisionIsIdempotent()
    {
        var store = new PresentationStore();
        var snapshot = TestSnapshots.Jump(3);
        Assert.True(store.ApplySnapshot(snapshot));

        var changes = 0;
        store.StateChanged += (_, _) => changes++;

        Assert.False(store.ApplySnapshot(snapshot with { }));
        Assert.Equal(snapshot, store.State.Snapshot);
        Assert.Equal(0, changes);
    }

    [Fact]
    public void SameRevisionWithDifferentContentFailsIntegrity()
    {
        var store = new PresentationStore();
        var snapshot = TestSnapshots.Jump(3);
        Assert.True(store.ApplySnapshot(snapshot));
        var conflicting = snapshot with
        {
            Jump = snapshot.Jump! with { StarSystem = "Other System" }
        };

        Assert.Throws<InvalidDataException>(() => store.ApplySnapshot(conflicting));
        Assert.Equal(snapshot, store.State.Snapshot);
        Assert.Equal(PresentationConnectionState.Incompatible, store.State.Connection);
    }

    [Fact]
    public void LowerRevisionIsIgnored()
    {
        var store = new PresentationStore();
        Assert.True(store.ApplySnapshot(TestSnapshots.Jump(3)));
        Assert.False(store.ApplySnapshot(TestSnapshots.Jump(2)));
        Assert.Equal(3UL, store.State.Snapshot!.Revision);
    }

    [Fact]
    public void DisconnectRetainsLastSnapshotAndChangesOnlyConnection()
    {
        var store = new PresentationStore();
        var snapshot = TestSnapshots.Jump(3);
        Assert.True(store.ApplySnapshot(snapshot));

        store.MarkDisconnected();

        Assert.Equal(PresentationConnectionState.Disconnected, store.State.Connection);
        Assert.Equal(snapshot, store.State.Snapshot);
    }

    [Fact]
    public void IncompatibleRetainsLastSnapshotAndChangesOnlyConnection()
    {
        var store = new PresentationStore();
        var snapshot = TestSnapshots.Jump(3);
        Assert.True(store.ApplySnapshot(snapshot));

        store.MarkIncompatible();

        Assert.Equal(PresentationConnectionState.Incompatible, store.State.Connection);
        Assert.Equal(snapshot, store.State.Snapshot);
    }

    [Fact]
    public void InvalidSnapshotRetainsLastValidSnapshotButBecomesIncompatible()
    {
        var store = new PresentationStore();
        var valid = TestSnapshots.Jump(3);
        Assert.True(store.ApplySnapshot(valid));
        var invalid = TestSnapshots.Jump(4) with { ProtocolVersion = 99 };

        Assert.Throws<InvalidDataException>(() => store.ApplySnapshot(invalid));
        Assert.Equal(valid, store.State.Snapshot);
        Assert.Equal(PresentationConnectionState.Incompatible, store.State.Connection);
    }

    [Fact]
    public void InvalidLowerRevisionIsValidatedBeforeRevisionComparison()
    {
        var store = new PresentationStore();
        var valid = TestSnapshots.Jump(3);
        Assert.True(store.ApplySnapshot(valid));
        var invalidOlder = TestSnapshots.Jump(2) with { ProtocolVersion = 99 };

        Assert.Throws<InvalidDataException>(() => store.ApplySnapshot(invalidOlder));
        Assert.Equal(valid, store.State.Snapshot);
        Assert.Equal(PresentationConnectionState.Incompatible, store.State.Connection);
    }
}

// Reconnect regression stays in this file so it remains under the Task-4 store filter.
public sealed class PresentationStoreReconnectTests
{
    [Fact]
    public void IdenticalCurrentSnapshotAfterDisconnectRestoresLiveConnection()
    {
        var store = new PresentationStore();
        var snapshot = TestSnapshots.Jump(3);
        Assert.True(store.ApplySnapshot(snapshot));
        store.MarkDisconnected();

        Assert.True(store.ApplySnapshot(snapshot with { }));
        Assert.Equal(PresentationConnectionState.Live, store.State.Connection);
        Assert.Equal(snapshot, store.State.Snapshot);
    }
}
