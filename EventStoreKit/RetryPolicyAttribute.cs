namespace EventStoreKit;

// Opt-in optimistic-concurrency retry, controlled by the library client.
//
// On a ConcurrencyException the framework re-runs the unit of work and retries, up to MaxAttempts:
//   - Command handlers: re-invoke Handle (which reloads the aggregate from the latest stream) and
//     re-append. Place on the Handle method (fallback: the handler class, then the command class).
//   - Sagas (EventQueueSubscriber): re-run the event handler. Place on the subscriber subclass.
//
// IMPORTANT: only mark units that are idempotent under replay — re-running must recompute the same
// (or correctly-adjusted) result from the reloaded state, not blindly duplicate side effects. A
// handler that appends to several streams and partially commits before the conflict is NOT safe.
// Without this attribute the conflict propagates as before (commands throw; saga handlers log-and-continue).
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RetryPolicyAttribute(int maxAttempts = 3) : Attribute
{
    public int MaxAttempts { get; } = maxAttempts;
}
