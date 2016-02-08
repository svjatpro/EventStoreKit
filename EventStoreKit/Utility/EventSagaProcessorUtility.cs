using System;
using System.Linq;
using CommonDomain;
using CommonDomain.Persistence;
using EventStoreKit.CommandBus;
using EventStoreKit.Messages;

namespace EventStoreKit.Utility
{
    /// <summary>
    /// Contains extension methods, which can resolve Saga id based on Event contend
    /// </summary>
    public static class EventSagaProcessorUtility
    {
        /// <summary>
        /// Resolve ( if null ) New Saga instance, process Message with Saga and save Saga
        /// </summary>
        /// <typeparam name="TSaga">Saga Type</typeparam>
        /// <param name="message">Message to process</param>
        /// <param name="repository">SagaRepository</param>
        /// <param name="saga">Saga instance ( if exist )</param>
        /// <returns>Saga instance</returns>
        public static TSaga ProcessAndSaveSaga<TSaga>( this Message message, ISagaRepository repository, TSaga saga = null )
            where TSaga : class, ISaga, new()
        {
            saga = saga ?? Activator.CreateInstance<TSaga>();
            message.ProcessAndSaveSaga( saga, repository );
            return saga;
        }

        /// <summary>
        /// Resolve ( if null ) Saga instance by Id, process Message with Saga and save Saga
        /// </summary>
        /// <typeparam name="TSaga">Saga Type</typeparam>
        /// <param name="message">Message to process</param>
        /// <param name="sagaId">Unique Saga identity</param>
        /// <param name="repository">SagaRepository</param>
        /// <param name="saga">Saga instance ( if exist )</param>
        /// <returns>Saga instance</returns>
        public static TSaga ProcessAndSaveSaga<TSaga>( this Message message, string sagaId, ISagaRepository repository, TSaga saga = null )
            where TSaga : class, ISaga, new()
        {
            saga = saga ?? repository.GetById<TSaga>( sagaId );
            message.ProcessAndSaveSaga( saga, repository );
            return saga;
        }

        /// <summary>
        /// Resolve ( if null ) Saga instance by custom resolve Func, process Message with Saga and save Saga
        /// </summary>
        /// <typeparam name="TSaga">Saga Type</typeparam>
        /// <param name="message">Message to process</param>
        /// <param name="resolveSaga">Custom Func to resolve / create Saga</param>
        /// <param name="repository">SagaRepository</param>
        /// <param name="saga">Saga instance ( if exist )</param>
        /// <returns>Saga instance</returns>
        public static TSaga ProcessAndSaveSaga<TSaga>( this Message message, Func<TSaga> resolveSaga, ISagaRepository repository, TSaga saga = null )
            where TSaga : class, ISaga
        {
            saga = saga ?? resolveSaga();
            message.ProcessAndSaveSaga( saga, repository );
            return saga;
        }

        /// <summary>
        /// Process Message with Saga instance, and then save saga
        /// </summary>
        /// <typeparam name="TSaga">Saga Type</typeparam>
        /// <param name="message">Messag to process</param>
        /// <param name="saga">Instance of Saga</param>
        /// <param name="repository">SagaRepository to save Saga after process message</param>
        /// <returns>Saga instance</returns>
        public static TSaga ProcessAndSaveSaga<TSaga>( this Message message, TSaga saga, ISagaRepository repository )
            where TSaga : class, ISaga
        {
            saga.Transition( message );

            foreach ( var msg in saga.GetUncommittedEvents() )
            {
                msg.OfType<DomainEvent>().Do( @event =>
                {
                    @event.Created = DateTime.Now;
                    @event.CreatedBy = message.CreatedBy;
                } );
            }
            foreach ( var msg in saga.GetUndispatchedMessages() )
            {
                msg.OfType<DomainCommand>().Do( cmd =>
                {
                    cmd.Created = DateTime.Now;
                    cmd.CreatedBy = message.CreatedBy;
                } );
            }
            repository.Save( saga, Guid.NewGuid(), a => { } );
            return saga;
        }

        /// <summary>
        /// Resolve and process Saga message, but don't save it
        /// </summary>
        /// <typeparam name="TSaga">Saga Type</typeparam>
        /// <param name="message">Message to process</param>
        /// <param name="sagaId">Unique Saga Identity</param>
        /// <param name="repository">SagaRepository to resolve empty saga</param>
        /// <param name="commandBus">command bus</param>
        /// <param name="saga">Instance of saga ( If already resolved )</param>
        /// <returns>Saga instance</returns>
        public static TSaga ProcessSaga<TSaga>( this Message message, string sagaId, ISagaRepository repository, ICommandBus commandBus, TSaga saga = null )
            where TSaga : class, ISaga
        {
            saga = saga ?? repository.GetById<TSaga>( sagaId );
            saga.Transition( message );
            
            Enumerable.OfType<DomainCommand>( saga.GetUndispatchedMessages() )
                .ToList()
                .ForEach( commandBus.Send );

            saga.ClearUncommittedEvents();
            saga.ClearUndispatchedMessages();

            return saga;
        }
        public static TSaga ProcessSaga<TSaga>( this Message message, ISagaRepository repository, ICommandBus commandBus ) where TSaga : class, ISaga, new()
        {
            return message.ProcessSaga( string.Empty, repository, commandBus, new TSaga() );
        }
    }
}
