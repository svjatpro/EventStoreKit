
namespace EventStoreKit.Services.Configuration
{
    public interface IEventStoreConfiguration
    {
        /// <summary>
        /// Buffer size, for new entities, added through the ProjectionTemplate
        /// </summary>
        int InsertBufferSize { get; }

        /// <summary>
        /// Time interval ( in milliseconds ), between the last active message, processed by a QueueSubscriber and OnIddle message
        /// </summary>
        int OnIddleInterval { get; }
    }
}
