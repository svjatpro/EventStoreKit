using EventStoreKit.NEventStore.Projections;
using FluentAssertions;
using NUnit.Framework;

namespace EventStoreKit.Tests.Subscriptions;

// Verifies the leaf-on-stream wait engine at the subscriber level: a marker pushed via
// EnqueueMarker arrives at the *tail* of the subscriber's queue, so SequenceFinished only
// fires after every event queued before it has been processed.
[TestFixture]
public class EventQueueSubscriberTests
{
    private sealed record TestEventA(string Id);
    private sealed record TestEventB(string Id);

    private sealed class TestSubscriber : EventQueueSubscriber,
        IEventHandler<TestEventA>,
        IEventHandler<TestEventB>
    {
        public List<object> Processed { get; } = [];

        public void Handle(TestEventA e)
        {
            lock (Processed) { Processed.Add(e); }
        }

        public void Handle(TestEventB e)
        {
            lock (Processed) { Processed.Add(e); }
        }
    }

    [Test]
    public void Marker_fires_only_after_prior_events_are_processed()
    {
        var sub = new TestSubscriber();
        using var done = new ManualResetEventSlim();
        Guid? receivedMarker = null;
        IReadOnlyList<object> snapshotAtMarker = null!;
        sub.SequenceFinished += (_, id) =>
        {
            receivedMarker = id;
            lock (sub.Processed) { snapshotAtMarker = sub.Processed.ToArray(); }
            done.Set();
        };

        sub.HandleEvent(new TestEventA("1"));
        sub.HandleEvent(new TestEventB("2"));
        sub.HandleEvent(new TestEventA("3"));

        var markerId = Guid.NewGuid();
        sub.EnqueueMarker(markerId);

        done.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue("marker should have fired");
        receivedMarker.Should().Be(markerId);
        snapshotAtMarker.Should().HaveCount(3);
    }

    [Test]
    public void Marker_fires_in_correct_order_when_events_are_enqueued_after_it()
    {
        var sub = new TestSubscriber();
        using var done = new ManualResetEventSlim();
        Guid? receivedMarker = null;
        IReadOnlyList<object> snapshotAtMarker = null!;
        sub.SequenceFinished += (_, id) =>
        {
            receivedMarker = id;
            lock (sub.Processed) { snapshotAtMarker = sub.Processed.ToArray(); }
            done.Set();
        };

        // Two events before the marker; one after. The marker should fire after the first two
        // are processed but before the third is.
        sub.HandleEvent(new TestEventA("1"));
        sub.HandleEvent(new TestEventA("2"));
        var markerId = Guid.NewGuid();
        sub.EnqueueMarker(markerId);
        sub.HandleEvent(new TestEventA("3"));

        done.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue();
        receivedMarker.Should().Be(markerId);
        // Exactly the two events queued before the marker should be in Processed at fire time.
        snapshotAtMarker.Should().HaveCount(2);
        ((TestEventA)snapshotAtMarker[0]).Id.Should().Be("1");
        ((TestEventA)snapshotAtMarker[1]).Id.Should().Be("2");
    }

    [Test]
    public void Marker_waits_for_slow_handler_to_finish()
    {
        var sub = new SlowSubscriber();
        using var done = new ManualResetEventSlim();
        IReadOnlyList<object> snapshotAtMarker = null!;
        sub.SequenceFinished += (_, _) =>
        {
            lock (sub.Processed) { snapshotAtMarker = sub.Processed.ToArray(); }
            done.Set();
        };

        sub.HandleEvent(new TestEventA("slow-1"));
        sub.EnqueueMarker(Guid.NewGuid());

        done.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue();
        // The marker must NOT fire before the slow event finishes processing.
        snapshotAtMarker.Should().HaveCount(1);
    }

    private sealed class SlowSubscriber : EventQueueSubscriber, IEventHandler<TestEventA>
    {
        public List<object> Processed { get; } = [];

        public void Handle(TestEventA e)
        {
            Thread.Sleep(200);
            lock (Processed) { Processed.Add(e); }
        }
    }

    [Test]
    public void Distinct_markers_each_fire_at_their_position()
    {
        var sub = new TestSubscriber();
        var fired = new List<Guid>();
        var firedAt = new Dictionary<Guid, int>();
        using var bothDone = new CountdownEvent(2);
        sub.SequenceFinished += (_, id) =>
        {
            lock (fired)
            {
                fired.Add(id);
                lock (sub.Processed) { firedAt[id] = sub.Processed.Count; }
            }
            bothDone.Signal();
        };

        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        sub.HandleEvent(new TestEventA("1"));
        sub.EnqueueMarker(first);
        sub.HandleEvent(new TestEventA("2"));
        sub.HandleEvent(new TestEventA("3"));
        sub.EnqueueMarker(second);

        bothDone.Wait(TimeSpan.FromSeconds(2)).Should().BeTrue();
        fired.Should().ContainInOrder(first, second);
        firedAt[first].Should().Be(1);
        firedAt[second].Should().Be(3);
    }
}
