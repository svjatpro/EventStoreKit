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
            ICommandBus commandBus,
            Func<ISagaRepository> sagaRepositoryFactory,
            Dictionary<Type, Func<Message, string>> idResolvingMap )
            : base( context )
        {
            var sagaType = typeof(TSaga);
            var interfaceType = typeof(IEventHandler<>);
            var interfaceType2 = typeof(IEventHandlerShort<>);
            var getByIdMethod = typeof(ISagaRepository).GetMethod( "GetById" )?.MakeGenericMethod( sagaType );

            sagaType
                .GetInterfaces()
                .Where( 
                    handlerInterface => handlerInterface.IsGenericType && ( 
                        handlerInterface.GetGenericTypeDefinition() == interfaceType.GetGenericTypeDefinition() ||
                        handlerInterface.GetGenericTypeDefinition() == interfaceType2.GetGenericTypeDefinition() ) )
                .ToList()
                .ForEach( handlerInterface =>
                {
                    var eventType = handlerInterface.GetGenericArguments()[0];
                    var saveSaga = handlerInterface.GetGenericTypeDefinition() == interfaceType.GetGenericTypeDefinition();
                    Register( eventType, message =>
                    {
                        var sagaId = idResolvingMap.GetSagaId( sagaType, message );
                        var sagaRepository = sagaRepositoryFactory();
                        var saga = getByIdMethod?
                            .Invoke( sagaRepository, new[] { Bucket.Default, sagaId } )
                            .OfType<ISaga>();
                        saga?.Transition( message );
                        if ( saveSaga )
                        {
                            Enumerable.OfType<DomainCommand>( saga.GetUndispatchedMessages() )
                                .ToList()
                                .ForEach( commandBus.SendCommand );

                            saga.ClearUndispatchedMessages();
                            sagaRepository.Save( saga, Guid.NewGuid(), a => { } );
                        }
                        else
                        {
                            Enumerable.OfType<DomainCommand>( saga.GetUndispatchedMessages() )
                                .ToList()
                                .ForEach( commandBus.SendCommand );

                            saga.ClearUncommittedEvents();
                            saga.ClearUndispatchedMessages();
                        }
                    } );
                } );
        }
    }
}
