using System.Collections.Concurrent;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reflection;

namespace EventStoreKit.NEventStore.Projections
{
    public abstract class EventQueueSubscriber : IEventSubscriber
    {
        #region internall classes

        public class EventInfo
        {
            public object Event = null!;
        }

        #endregion

        #region Private fields

        private readonly BlockingCollection<EventInfo> MessageQueue;
        private readonly Dictionary<Type, List<Action<object>>> Handlers;
        
        #endregion

        #region Private methods

        private Action<object> CreateHandler<TEvent>() where TEvent : class
        {
            var handler = (IEventHandler<TEvent>)this;
            return e => handler.Handle((TEvent)e);
        }
        
        private void ProcessMessages( object message)
        {
            var msgType = message.GetType();
            try
            {
                // process static handlers
                if ( Handlers.TryGetValue(msgType, out var handlers) )
                {
                    foreach ( var handler in handlers )
                    {
                        handler( message );
                    }
                }
            }
            catch (Exception ex)
            {
                // log error and continue processing other messages
            }
        }

        #endregion

        #region Protected methods

        protected void Register<TEvent>(Action<TEvent> action, bool singleAction = true) where TEvent : class
        {
            Register(typeof(TEvent), DelegateAdjuster.CastArgument<object, TEvent>(action), singleAction );
        }
        protected void Register(Type eventType, Action<object> action, bool singleAction = true)
        {
            if ( Handlers.TryGetValue( eventType, out var handler ) )
            {
                if ( singleAction && handler.Count > 1 )
                {
                    throw new InvalidOperationException( $"Event type '{eventType.Name}' already registered." );
                }
                handler.Add( action );
            }
            else
            {
                Handlers.Add( eventType, [action] );
            }
        }

        protected void Handle<TEvent>( TEvent e, bool isRebuild ) where TEvent : class
        {
            var eventType = e.GetType();
            if( !Handlers.ContainsKey( eventType ) )
                return;
            MessageQueue.Add( new EventInfo{ Event = e } );
        }

        #endregion

        protected EventQueueSubscriber()
        {
            Handlers = new Dictionary<Type, List<Action<object>>>();

            MessageQueue = new BlockingCollection<EventInfo>();
            MessageQueue.GetConsumingEnumerable()
                .ToObservable( new NewThreadScheduler( a => new Thread(a){ IsBackground = true }) )
                .Subscribe( ProcessMessages );

            var handlerTypes = GetType()
                .GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>));

            foreach (var handlerType in handlerTypes)
            {
                var eventType = handlerType.GetGenericArguments()[0];

                var handler = typeof(EventQueueSubscriber)
                    .GetMethod(nameof(CreateHandler), BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(eventType)
                    .Invoke(this, []) as Action<object>;
                Register(eventType, handler!, true);
            }
        }

        public void Handle<TEvent>(TEvent @event) where TEvent : class
        {
            Handle( @event, false );
        }

        public IEnumerable<Type> HandledEventTypes => Handlers.Keys;
    }
}
