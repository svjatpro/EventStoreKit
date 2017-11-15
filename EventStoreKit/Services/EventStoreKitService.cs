using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using CommonDomain.Persistence;
using EventStoreKit.Aggregates;
using EventStoreKit.CommandBus;
using EventStoreKit.Handler;
using EventStoreKit.Messages;

namespace EventStoreKit.Services
{
    public class EventStoreKitService
    {
        private readonly IEventDispatcher Dispatcher;

        private void RegisterCommandHandler<TCommand, TEntity>()
            // ReSharper restore UnusedMember.Local
            where TCommand : DomainCommand
            where TEntity : class, ITrackableAggregate
        {
            // register Action as handler to dispatcher
            //var repositoryFactory = Container.Resolve<Owned<Func<IRepository>>>();
            //var handlerFactory = Container.Resolve<Owned<Func<ICommandHandler<TCommand, TEntity>>>>();

            //var handleAction = new Action<TCommand>( cmd =>
            //{
            //    var repository = repositoryFactory.Value();
            //    var handler = handlerFactory.Value();

            //    if ( cmd.Created == default( DateTime ) )
            //        cmd.Created = DateTime.Now;
            //    if ( cmd.CreatedBy == Guid.Empty && CurrentUserProvider.CurrentUserId != null )
            //        cmd.CreatedBy = CurrentUserProvider.CurrentUserId.Value;
            //    var context = new CommandHandlerContext<TEntity>
            //    {
            //        Entity = repository.GetById<TEntity>( cmd.Id )
            //    };
            //    if ( cmd.CreatedBy != Guid.Empty )
            //        context.Entity.IssuedBy = cmd.CreatedBy;
            //    else
            //        CurrentUserProvider.CurrentUserId.Do( userId => context.Entity.IssuedBy = userId.GetValueOrDefault() );

            //    handler.Handle( cmd, context );
            //    Logger.Info( "{0} processed; version = {1}", cmd.GetType().Name, cmd.Version );
            //    repository.Save( context.Entity, IdGenerator.NewGuid() );
            //} );
            //Dispatcher.RegisterHandler( handleAction );
        }

        public static void RegisterCommandHandler<THandler>()
        {


            //var commandHandlerInterfaceType = typeof(ICommandHandler<,>);
            //var registerCommandMehod = GetType().GetMethod("RegisterCommandHandler", BindingFlags.NonPublic | BindingFlags.Instance);
            //CommandHandlers
            //    .ToList()
            //    .ForEach(handler =>
            //    {
            //        var cmdHandlerTypes = handler
            //            .GetType()
            //            .GetInterfaces()
            //            .Where(i => i.Name == commandHandlerInterfaceType.Name);
            //        cmdHandlerTypes
            //            .ToList()
            //            .ForEach(cmdType =>
            //            {
            //                var genericArgs = cmdType.GetGenericArguments();
            //                registerCommandMehod
            //                    .MakeGenericMethod(genericArgs[0], genericArgs[1])
            //                    .Invoke(this, new object[] { });
            //            });
            //    });
        }

        private EventStoreKitService()
        {
            
        }

        public static EventStoreKitService Initialize()
        {
            var service = new EventStoreKitService();
            // handlers for Aggregates

            //builder.RegisterType<MessageDispatcher>()
            //    .As<IEventPublisher>()
            //    .As<IEventDispatcher>()
            //    .As<ICommandBus>()
            //    .SingleInstance();

            

            return service;
        }
    }
}
