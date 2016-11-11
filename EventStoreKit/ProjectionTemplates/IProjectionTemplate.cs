using EventStoreKit.Messages;

namespace EventStoreKit.ProjectionTemplates
{
    public interface IProjectionTemplate
    {
        void CleanUp( SystemCleanedUpEvent msg );
        void PreprocessEvent( Message @event );
        void Flush( );
    }
}