using System.Collections.Generic;
using System.Linq;
using EventStoreKit.DbProviders;
using EventStoreKit.Handler;
using EventStoreKit.Projections;
using EventStoreKit.Services;

namespace Dummy
{
    /// <summary>
    /// Projection class
    /// </summary>
    public class GreetingsProjection : SqlProjectionBase<GreetingReadModel>,
        IEventHandler<GreetedEvent>
    {
        public GreetingsProjection(IEventStoreSubscriberContext context) : base(context)
        {
        }

        public void Handle( GreetedEvent message )
        {
            DbProviderFactory.Run( db =>
                db.Insert(
                    new GreetingReadModel
                    {
                        SpeakerId = message.Id,
                        Message = message.HelloMessage
                    })
            );
        }
        
        public List<GreetingReadModel> GetAllMessages()
        {
            return DbProviderFactory.Run( db => db.Query<GreetingReadModel>().ToList() );
        }
    }
}