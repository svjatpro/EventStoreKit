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
        #region Private methods

        private static void PrepareMessages<TSaga>( this TSaga saga, Message message )
            where TSaga : class, ISaga
        {
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
        }

        #endregion

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
            saga.Transition( message );
            saga.PrepareMessages( message );
            repository.Save( saga, Guid.NewGuid(), a => { } );

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
            saga.Transition( message );
            saga.PrepareMessages( message );
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
            saga.PrepareMessages( message );

            Enumerable.OfType<DomainCommand>( saga.GetUndispatchedMessages() )
                .ToList()
                .ForEach( commandBus.Send );

            saga.ClearUncommittedEvents();
            saga.ClearUndispatchedMessages();

            return saga;
        }

        /// <summary>
        /// Resolve and process Saga message, then dispatch commands and save saga, headers doesn't contain commands!
        /// </summary>
        /// <typeparam name="TSaga">Saga Type</typeparam>
        /// <param name="message">Message to process</param>
        /// <param name="sagaId">Unique Saga Identity</param>
        /// <param name="repository">SagaRepository to resolve empty saga</param>
        /// <param name="commandBus">command bus</param>
        /// <param name="saga">Instance of saga ( If already resolved )</param>
        /// <returns>Saga instance</returns>
        public static TSaga ProcessAndSaveSaga<TSaga>( this Message message, string sagaId, ISagaRepository repository, ICommandBus commandBus, TSaga saga = null )
            where TSaga : class, ISaga
        {
            saga = saga ?? repository.GetById<TSaga>( sagaId );
            saga.Transition( message );
            saga.PrepareMessages( message );

            Enumerable.OfType<DomainCommand>( saga.GetUndispatchedMessages() )
                .ToList()
                .ForEach( commandBus.Send );

            saga.ClearUndispatchedMessages();
            repository.Save( saga, Guid.NewGuid(), a => { } );

            return saga;
        }
    }
}
