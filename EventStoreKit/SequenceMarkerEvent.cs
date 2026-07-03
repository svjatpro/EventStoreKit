namespace EventStoreKit;

// transient "leaf-on-stream" marker
public record SequenceMarkerEvent
{
    public Guid Identity { get; init; }
}
