using System;
using System.Linq;
using System.Reflection;
using CommonDomain.Core;
using EventStoreKit.Handler;
using EventStoreKit.Messages;

namespace EventStoreKit.Services
{
    public class SagaBase : SagaBase<Message>
    {
        public SagaBase()
        {
            var interfaceType = typeof( IEventHandler<> );
            var sagaType = GetType();
            sagaType
                .GetInterfaces()
                .Where( handlerIntefrace => handlerIntefrace.IsGenericType && handlerIntefrace.GetGenericTypeDefinition() == interfaceType.GetGenericTypeDefinition() )
                .ToList()
                .ForEach( handlerIntefrace =>
                {
                    // 

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