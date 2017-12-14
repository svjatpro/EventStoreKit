namespace EventStoreKit.Projections
{
    public interface IProjection : IEventSubscriber
    {
        string Name { get; }
    }
}
