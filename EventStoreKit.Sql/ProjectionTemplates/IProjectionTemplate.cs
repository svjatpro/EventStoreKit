
using EventStoreKit.Messages;

namespace EventStoreKit.Sql.ProjectionTemplates
{
    public interface IProjectionTemplate
    {
        void CleanUp( SystemCleanedUpEvent msg );
        void PreprocessEvent( Message @event );
        void Flush( );
    }
}