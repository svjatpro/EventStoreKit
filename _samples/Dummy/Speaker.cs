using System;
using EventStoreKit.Aggregates;

namespace Dummy
{
    /// <summary>
    /// Aggregate / Domain class
    /// </summary>
    public class Speaker : TrackableAggregateBase
    {
        private void Apply( GreetedEvent message ){}

        public Speaker( Guid id )
        {
            Id = id;
            Register<GreetedEvent>( Apply );
        }

        public void Greet( string objectName )
        {
            RaiseEvent( new GreetedEvent
            {
                Id = Id,
                HelloMessage = $"Hello, {objectName}!"
            } );
        }
    }
}