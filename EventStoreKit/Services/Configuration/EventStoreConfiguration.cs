namespace EventStoreKit.Services.Configuration
{
    public class EventStoreConfiguration : ConfigurationService, IEventStoreConfiguration
    {
        public int InsertBufferSize { get { return GetAppSetting( "InsertBufferSize", int.Parse, 10000 ); } }
        public int OnIddleInterval { get { return GetAppSetting( "OnIddleInterval", int.Parse, 500 ); } }
    }
}
