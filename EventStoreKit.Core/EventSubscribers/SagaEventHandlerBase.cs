using System;
using System.Collections.Generic;
using System.Linq;
using CommonDomain;
using CommonDomain.Persistence;
using EventStoreKit.Handler;
using EventStoreKit.Messages;
using EventStoreKit.Services;
using EventStoreKit.Utility;
using NEventStore;

namespace EventStoreKit.Projections
{
    public abstract class SagaEventHandlerBase : EventQueueSubscriber
    {
        protected readonly ISagaRepository Repository;

        protected SagaEventHandlerBase(
            IEventStoreSubscriberContext context,
            ISagaRepository repository )
            : base( context )
        {
            Repository = repository;
        }
    }

    public class SagaEventHandlerEmbedded<TSaga> : EventQueueSubscriber
        where TSaga : ISaga
    {
        public SagaEventHandlerEmbedded( 
            IEventStoreSubscriberContext context,
            Func<ISagaRepository> sagaRepositoryFactory,
            Dictionary<Type, Func<Message, string>> idResolvingMap )
            : base( context )
        {
            var sagaType = typeof(TSaga);
            var interfaceType = typeof(IEventHandler<>);
            var getByIdMethod = typeof(ISagaRepository).GetMethod( "GetById" )?.MakeGenericMethod( sagaType );

            sagaType
                .GetInterfaces()
                .Where( handlerInterface => handlerInterface.IsGenericType && handlerInterface.GetGenericTypeDefinition() == interfaceType.GetGenericTypeDefinition() )
                .Select(handlerInterface => handlerInterface.GetGenericArguments()[0] )
                .ToList()
                .ForEach( eventType =>
                {
                    Register( eventType, message =>
                    {
                        var sagaId = idResolvingMap.GetSagaId( sagaType, message );
                        var sagaRepository = sagaRepositoryFactory();
                        var saga = getByIdMethod?
                            .Invoke( sagaRepository, new[] { Bucket.Default, sagaId } )
                            .OfType<ISaga>();
                        saga?.Transition( message );
                        sagaRepository.Save( saga, Guid.NewGuid(), a => { } );
                    } );
                } );
        }
    }
}
