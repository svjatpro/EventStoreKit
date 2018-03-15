using System;
using System.Linq;
using System.Reflection;
using CommonDomain.Core;
using EventStoreKit.Handler;
using EventStoreKit.Messages;

namespace EventStoreKit.Core.Sagas
{
    public abstract class SagaBase : SagaBase<Message>
    {
        protected SagaBase()
        {
            var handlerTypeEvent = typeof( IEventHandler<> );
            var handlerTypeTransient = typeof( IEventHandlerTransient<> );
            var handlerTypeCommand = typeof( ICommandHandler<> );
            var sagaType = GetType();
            sagaType
                .GetInterfaces()
                .Where(
                    handlerInterface => handlerInterface.IsGenericType && (
                        handlerInterface.GetGenericTypeDefinition() == handlerTypeEvent.GetGenericTypeDefinition() ||
                        handlerInterface.GetGenericTypeDefinition() == handlerTypeTransient.GetGenericTypeDefinition() ||
                        handlerInterface.GetGenericTypeDefinition() == handlerTypeCommand.GetGenericTypeDefinition() ) )
                .ToList()
                .ForEach( handlerIntefrace =>
                {
                    var genericArgs = handlerIntefrace.GetGenericArguments();
                    var registerMethod = sagaType
                        .GetMethod( "Register", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy )
                        ?.MakeGenericMethod( genericArgs[0] );
                    var handleDelegate = Delegate.CreateDelegate( typeof( Action<> ).MakeGenericType( genericArgs[0] ), this, "Handle" );
                    registerMethod?.Invoke( this, new object[] { handleDelegate } );
                } );
        }
    }
}