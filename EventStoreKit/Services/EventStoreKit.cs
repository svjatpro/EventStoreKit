using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using CommonDomain.Persistence;
using EventStoreKit.Aggregates;
using EventStoreKit.CommandBus;
using EventStoreKit.Handler;
using EventStoreKit.Messages;

namespace EventStoreKit.Services
{
    public interface IEventStoreKit
    {
        
    }
    public class EventStoreKit : IEventStoreKit
    {
        private readonly IEventDispatcher Dispatcher;
        private static HashSet<Type> CommandHandlerTypes = new HashSet<Type>();

        protected virtual IRepository ResolveRepository()
        {
            
        }

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

        //public static void RegisterCommandHandler<THandler>(THandler handler)
        //{
            
        //}
        public static void RegisterCommandHandler<THandler>()
        {
            var commandHandlerInterfaceType = typeof(ICommandHandler<,>);
            typeof(THandler)
                .GetInterfaces()
                .Where(i => i.Name == commandHandlerInterfaceType.Name)
                .ToList()
                .ForEach(h => CommandHandlerTypes.Add(h));

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

        private EventStoreKit()
        {
            
        }

        public static EventStoreKit Initialize()
        {
            var service = new EventStoreKit();

            // handlers for Aggregates

            var commandHandlerInterfaceType = typeof(ICommandHandler<,>);
            var registerCommandMehod = service.GetType().GetMethod("RegisterCommandHandler", BindingFlags.NonPublic | BindingFlags.Instance);

            CommandHandlerTypes
                .ToList()
                .ForEach(cmdType =>
                {
                    var genericArgs = cmdType.GetGenericArguments();
                    registerCommandMehod
                        .MakeGenericMethod(genericArgs[0], genericArgs[1])
                        .Invoke(service, new object[] { });
                });

            //builder.RegisterType<MessageDispatcher>()
            //    .As<IEventPublisher>()
            //    .As<IEventDispatcher>()
            //    .As<ICommandBus>()
            //    .SingleInstance();



            return service;
        }
    }
}
