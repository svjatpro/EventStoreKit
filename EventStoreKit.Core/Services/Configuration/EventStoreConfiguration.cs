namespace EventStoreKit.Services.Configuration
{
    public class EventStoreConfiguration : ConfigurationService, IEventStoreConfiguration
    {          
        public int InsertBufferSize { get; set; }
        public int OnIddleInterval { get; set; }
    }
}
