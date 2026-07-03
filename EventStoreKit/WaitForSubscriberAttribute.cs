namespace EventStoreKit;

// Declares which subscriber types a command handler depends on for the next read.
// The framework command wrapper appends events first, then calls EventSequence.Wait(targets)
// so the caller's follow-up read sees those subscribers caught up.
//
// Placed on the Handle method (for ICommandHandler<T> / ICommandHandler<T,TCtx> implementations
// and aggregate Handle methods), or as a fallback on the command class itself.
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class WaitForSubscriberAttribute(params Type[] subscriberTypes) : Attribute
{
    public Type[] SubscriberTypes { get; } = subscriberTypes;
}
