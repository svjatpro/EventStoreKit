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
    public class SagaEventHandlerEmbedded<TSaga> : EventQueueSubscriber
        where TSaga : ISaga
    {
        private readonly Dictionary<string, ISaga> SagaCache = new Dictionary<string, ISaga>();

        public SagaEventHandlerEmbedded(
            IEventStoreSubscriberContext context,
            ICommandBus commandBus,
            Func<ISagaRepository> sagaRepositoryFactory,
            Dictionary<Type, Func<Message, string>> idResolvingMap,
            bool cached )
            : base( context )
        {
            var sagaType = typeof(TSaga);
            var handlerTypeEvent = typeof(IEventHandler<>);
            var handlerTypeTransient = typeof(IEventHandlerTransient<>);
            var handlerTypeCommand = typeof(ICommandHandler<>);
            var getByIdMethod = typeof(ISagaRepository).GetMethod( "GetById" )?.MakeGenericMethod( sagaType );

            sagaType
                .GetInterfaces()
                .Where( 
                    handlerInterface => handlerInterface.IsGenericType && ( 
                        handlerInterface.GetGenericTypeDefinition() == handlerTypeEvent.GetGenericTypeDefinition() ||
                        handlerInterface.GetGenericTypeDefinition() == handlerTypeTransient.GetGenericTypeDefinition() ||
                        handlerInterface.GetGenericTypeDefinition() == handlerTypeCommand.GetGenericTypeDefinition() ) )
                .ToList()
                .ForEach( handlerInterface =>
                {
                    var eventType = handlerInterface.GetGenericArguments()[0];
                    var saveSaga = handlerInterface.GetGenericTypeDefinition() == handlerTypeEvent.GetGenericTypeDefinition();
                    Register( eventType, message =>
                    {
                        // restore saga
                        var sagaId = idResolvingMap.GetSagaId( sagaType, message );
                        var sagaRepository = sagaRepositoryFactory();

                        ISaga saga;
                        if ( !cached || !SagaCache.ContainsKey( sagaId ) )
                        {
                            saga = getByIdMethod?
                                .Invoke( sagaRepository, new object[] {Bucket.Default, sagaId} )
                                .OfType<ISaga>();
                            if( cached )
                                SagaCache[sagaId] = saga;
                        }
                        else
                        {
                            saga = SagaCache[sagaId];
                        }

                        // handle message
                        saga?.Transition( message );

                        // process undispatched messages and save saga
                        if ( saveSaga )
                        {
                            saga?.GetUndispatchedMessages()
                                .OfType<DomainCommand>()
                                .ToList()
                                .ForEach( commandBus.SendCommand );

                            saga?.ClearUndispatchedMessages();
                            sagaRepository.Save( saga, Guid.NewGuid(), a => { } );
                        }
                        else
                        {
                            saga?.GetUndispatchedMessages()
                                .OfType<DomainCommand>()
                                .ToList()
                                .ForEach( commandBus.SendCommand );

                            saga?.ClearUncommittedEvents();
                            saga?.ClearUndispatchedMessages();
                        }
                    } );
                } );
        }
    }
}
